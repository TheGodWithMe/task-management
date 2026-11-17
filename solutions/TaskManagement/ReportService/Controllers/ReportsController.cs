using System.Text.Json;
using log4net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Contracts.Models;

namespace TaskManagement.ReportService.Controllers;

/// <summary>
/// Композитный микросервис отчётов: обращается к TaskService и IdentityService
/// по HTTP, получает транспортные модели и вычисляет отчёт по статусам задач проекта.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Руководитель проекта")]
public class ReportsController : ControllerBase
{
    private readonly HttpClient _taskClient;
    private readonly HttpClient _identityClient;
    private readonly ILog _logger;

    public ReportsController(IHttpClientFactory httpFactory, ILog logger)
    {
        _taskClient = httpFactory.CreateClient("TaskService");
        _identityClient = httpFactory.CreateClient("IdentityService");
        _logger = logger;
    }

    /// <summary>Проверка работоспособности композитного сервиса.</summary>
    [HttpGet("start")]
    [AllowAnonymous]
    public ActionResult<string> Start() => "ReportService is run!";

    /// <summary>
    /// Отчёт по статусам задач проекта за период (прецедент
    /// «Формировать отчёт по статусам задач проекта»).
    /// </summary>
    [HttpGet("project/{projectId:long}")]
    public async Task<ActionResult<ProjectReportResponse>> GetProjectReport(
        long projectId, [FromQuery] DateTime start, [FromQuery] DateTime end)
    {
        if (start == default || end == default || start.Date > end.Date || end.Date == DateTime.MaxValue.Date)
            return BadRequest(new { message = "Укажите корректный период отчета" });
        start = DateTime.SpecifyKind(start.Date, DateTimeKind.Utc);
        end = DateTime.SpecifyKind(end.Date, DateTimeKind.Utc);
        var reportTime = DateTime.UtcNow;

        _logger.Info($"Отчёт по проекту {projectId} за период {start:d} — {end:d}");

        var projectResponse = await SendAsync(_taskClient, $"api/projects/{projectId}");
        if (!projectResponse.IsSuccessStatusCode)
            return StatusCode((int)projectResponse.StatusCode, new { message = "Не удалось получить проект" });

        var projectJson = await projectResponse.Content.ReadAsStringAsync();
        var projectName = JsonDocument.Parse(projectJson).RootElement.GetProperty("name").GetString() ?? string.Empty;

        // Транспортная модель задач, полученная из TaskService
        var tasksResponse = await SendAsync(_taskClient, $"api/tasks?projectId={projectId}");
        if (!tasksResponse.IsSuccessStatusCode)
            return StatusCode((int)tasksResponse.StatusCode, new { message = "Не удалось получить задачи" });
        var tasksJson = tasksResponse.IsSuccessStatusCode
            ? await tasksResponse.Content.ReadAsStringAsync()
            : "[]";
        var tasks = JsonSerializer.Deserialize<List<TaskResponse>>(tasksJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

        var statusesResponse = await SendAsync(_taskClient, "api/catalogs/statuses");
        if (!statusesResponse.IsSuccessStatusCode)
            return StatusCode((int)statusesResponse.StatusCode, new { message = "Не удалось получить статусы" });
        var statusesJson = statusesResponse.IsSuccessStatusCode
            ? await statusesResponse.Content.ReadAsStringAsync()
            : "[]";
        var statusNames = JsonDocument.Parse(statusesJson).RootElement.EnumerateArray()
            .Select(e => e.GetProperty("name").GetString() ?? string.Empty)
            .ToList();

        var periodTasks = tasks
            .Where(t => t.CreatedAt >= start && t.CreatedAt < end.AddDays(1))
            .ToList();

        var reports = new List<TaskStatusReport>();

        foreach (var status in statusNames)
        {
            var inStatus = periodTasks.Where(t => t.Status == status).ToList();

            var totalHours = 0.0;
            foreach (var task in inStatus)
            {
                var historyResponse = await SendAsync(_taskClient, $"api/catalogs/history/{task.Id}");
                if (!historyResponse.IsSuccessStatusCode)
                    continue;

                var historyJson = await historyResponse.Content.ReadAsStringAsync();
                var lastEntry = JsonDocument.Parse(historyJson).RootElement.EnumerateArray()
                    .LastOrDefault(e => e.GetProperty("toStatus").GetString() == status);

                if (lastEntry.ValueKind == JsonValueKind.Undefined)
                    continue;

                var changedAt = lastEntry.GetProperty("changedAt").GetDateTime();
                totalHours += Math.Max(0, (reportTime - changedAt).TotalHours);
            }

            reports.Add(new TaskStatusReport
            {
                StatusName = status,
                TaskCount = inStatus.Count,
                OverdueCount = inStatus.Count(t => t.Overdue),
                Percentage = periodTasks.Count > 0 ? Math.Round(100.0 * inStatus.Count / periodTasks.Count, 2) : 0,
                AverageTimeInStatusHours = inStatus.Count > 0 ? Math.Round(totalHours / inStatus.Count, 2) : 0
            });
        }

        // ФИО исполнителей уточняется в IdentityService (второй микросервис)
        var usersResponse = await SendAsync(_identityClient, "api/users");
        var executors = usersResponse.IsSuccessStatusCode
            ? JsonSerializer.Deserialize<List<UserResponse>>(await usersResponse.Content.ReadAsStringAsync(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new()
            : new();
        _logger.Info($"IdentityService вернул пользователей: {executors.Count}");

        var result = new ProjectReportResponse
        {
            ProjectName = projectName,
            PeriodStart = start,
            PeriodEnd = end,
            TotalTasks = periodTasks.Count,
            StatusReports = reports
        };

        _logger.Info($"Отчёт по проекту {projectId} сформирован: {result.TotalTasks} задач");
        return Ok(result);
    }

    // Запросы к другим микросервисам выполняются с токеном вызывающего,
    // чтобы права и роль пользователя сохранялись по всей цепочке.
    private async Task<HttpResponseMessage> SendAsync(HttpClient client, string relativeUrl)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        if (Request.Headers.Authorization.ToString() is { Length: > 0 } header)
            request.Headers.TryAddWithoutValidation("Authorization", header);

        return await client.SendAsync(request);
    }
}
