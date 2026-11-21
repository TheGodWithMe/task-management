using System.Net.Http.Headers;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Contracts.Models;
using TaskManagement.WebClient.Models;

namespace TaskManagement.WebClient.Controllers;

[Authorize]
public class WorkspaceController(IHttpClientFactory factory) : Controller
{
    private HttpClient Client(string name)
    {
        var client = factory.CreateClient(name);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", User.FindFirst("access_token")?.Value);
        return client;
    }

    private async Task<T> Load<T>(string service, string path) where T : new()
    {
        using var response = await Client(service).GetAsync(path);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>() ?? new T();
    }

    [HttpGet("/")]
    public async Task<IActionResult> Index(long? projectId, string? status, string? priority,
        long? taskId, bool report = false, DateTime? start = null, DateTime? end = null)
    {
        var model = new WorkspaceModel
        {
            ProjectId = projectId, Status = status ?? "", Priority = priority ?? "",
            Start = start ?? DateTime.Today.AddMonths(-1), End = end ?? DateTime.Today
        };
        try
        {
            model.Projects = await Load<List<ProjectResponse>>("TaskService", "api/projects");
            if (taskId.HasValue)
            {
                try
                {
                    model.Details = await Load<TaskDetails>("TaskService", $"api/tasks/{taskId}");
                    projectId = model.Details.Task.ProjectId;
                    model.ProjectId = projectId;
                }
                catch (HttpRequestException error) when (error.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
                {
                    ViewBag.Error = "Задача недоступна.";
                }
            }

            var query = "api/tasks?status=" + Uri.EscapeDataString(model.Status);
            if (projectId.HasValue) query += "&projectId=" + projectId.Value;
            model.Tasks = await Load<List<TaskResponse>>("TaskService", query);
            if (!string.IsNullOrEmpty(priority))
                model.Tasks = model.Tasks.Where(t => t.Priority == priority).ToList();
            var manager = User.IsInRole("Руководитель проекта");
            if (manager && projectId.HasValue)
                model.Participants = await Load<List<ParticipantResponse>>("TaskService",
                    $"api/projects/{projectId}/participants");
            if (manager && model.Projects.Any(p => p.Id == projectId && !p.IsArchived))
                model.Users = await Load<List<UserResponse>>("IdentityService", "api/users");
            if (report && projectId.HasValue && manager)
                model.Report = await Load<ProjectReportResponse>("ReportService",
                    $"api/reports/project/{projectId}?start={model.Start:yyyy-MM-dd}&end={model.End:yyyy-MM-dd}");
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            if (error is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized })
            {
                await HttpContext.SignOutAsync();
                return RedirectToAction(nameof(AccountController.Login), "Account");
            }
            ViewBag.Error = "Не удалось загрузить данные. Проверьте доступность сервисов и параметры запроса.";
        }
        return View(model);
    }

    [HttpPost("/workspace/run")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(string operation, long id, long projectId,
        string? name, string? description, long executorId, string? priority,
        DateTime? deadline, string? newStatus, string? text, string? filterStatus, string? filterPriority)
    {
        var manager = User.IsInRole("Руководитель проекта");
        string path;
        object body;
        var method = HttpMethod.Post;
        switch (operation)
        {
            case "project" when manager:
                path = "api/projects";
                body = new ProjectResponse { Name = name ?? "", Description = description ?? "" };
                break;
            case "editProject" when manager:
                path = $"api/projects/{id}"; method = HttpMethod.Put;
                body = new ProjectResponse { Name = name ?? "", Description = description ?? "" };
                break;
            case "complete" when manager:
                path = $"api/projects/{id}/complete"; body = new { };
                break;
            case "participant" when manager:
                path = $"api/projects/{projectId}/participants"; body = executorId;
                break;
            case "create" when manager:
                path = "api/tasks";
                body = new TaskCreateRequest { Name = name ?? "", Description = description ?? "",
                    ProjectId = projectId, ExecutorId = executorId, Priority = priority ?? "Средний",
                    Deadline = deadline?.Date.AddDays(1).AddTicks(-1) ?? default };
                break;
            case "assign" when manager:
                path = $"api/tasks/{id}/executor"; body = executorId;
                break;
            case "cancel" when manager:
                path = $"api/tasks/{id}/cancel"; body = new { };
                break;
            case "return" when manager:
                path = $"api/tasks/{id}/return"; body = new CommentRequest { Text = text ?? "" };
                break;
            case "status":
                path = $"api/tasks/{id}/status";
                body = new StatusChangeRequest { NewStatus = newStatus ?? "", Comment = text ?? "" };
                break;
            case "comment":
                path = $"api/tasks/{id}/comments"; body = new CommentRequest { Text = text ?? "" };
                break;
            default: return Forbid();
        }
        try
        {
            using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
            using var response = await Client("TaskService").SendAsync(request);
            if (response.IsSuccessStatusCode) TempData["Success"] = "Изменения сохранены.";
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                TempData["Error"] = "Операция отклонена. Проверьте права и состояние задачи.";
                try
                {
                    using var error = JsonDocument.Parse(content);
                    if (error.RootElement.ValueKind == JsonValueKind.Object &&
                        error.RootElement.TryGetProperty("message", out var message) &&
                        message.ValueKind == JsonValueKind.String)
                        TempData["Error"] = message.GetString();
                }
                catch (JsonException) { }
            }
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException) { TempData["Error"] = "Сервис недоступен. Попробуйте позже."; }

        var taskId = operation is "assign" or "cancel" or "return" or "status" or "comment" ? (long?)id : null;
        return RedirectToAction(nameof(Index), controllerName: null,
            routeValues: new
            {
                projectId = projectId > 0 ? (long?)projectId : null,
                taskId,
                status = filterStatus,
                priority = filterPriority
            },
            fragment: taskId.HasValue ? "task-details" : null);
    }

    [Authorize(Roles = "Руководитель проекта")]
    [HttpGet("/workspace/export")]
    public async Task<IActionResult> Export(long projectId, DateTime start, DateTime end)
    {
        try
        {
            using var response = await Client("ReportService").GetAsync(
                $"api/reports/project/{projectId}/export?start={start:yyyy-MM-dd}&end={end:yyyy-MM-dd}");
            if (response.IsSuccessStatusCode)
                return File(await response.Content.ReadAsByteArrayAsync(), "text/csv; charset=utf-8", $"project-{projectId}.csv");
            TempData["Error"] = "Не удалось выгрузить отчет. Проверьте период.";
        }
        catch (HttpRequestException) { TempData["Error"] = "Сервис отчетов недоступен."; }
        return RedirectToAction(nameof(Index), new { projectId });
    }
}
