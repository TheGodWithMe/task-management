using log4net;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Contracts.Models;

namespace TaskManagement.WebClient.Controllers;

/// <summary>
/// Прокси к микросервису отчётов. Пользователь работает только с WebClient,
/// запрос перенаправляется в композитный ReportService вместе с JWT-токеном.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly ILog _logger;

    public ReportsController(IHttpClientFactory httpFactory, ILog logger)
    {
        _httpClient = httpFactory.CreateClient("ReportService");
        _logger = logger;
    }

    /// <summary>Отчёт по статусам задач проекта за период.</summary>
    [HttpGet("project/{projectId:long}")]
    public async Task<ActionResult<ProjectReportResponse>> GetProjectReport(
        long projectId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        _logger.Info($"Формирование отчёта по проекту {projectId} за период {startDate:d} — {endDate:d}");

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"api/reports/project/{projectId}?start={startDate:yyyy-MM-dd}&end={endDate:yyyy-MM-dd}");
        ForwardAuthorization(request);

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.Warn($"Ошибка формирования отчёта: {response.StatusCode}");
            return StatusCode((int)response.StatusCode, content);
        }

        _logger.Info("Отчёт успешно получен");
        return Ok(System.Text.Json.JsonSerializer.Deserialize<ProjectReportResponse>(content,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }));
    }

    private void ForwardAuthorization(HttpRequestMessage request)
    {
        if (Request.Headers.Authorization.ToString() is { Length: > 0 } header)
            request.Headers.TryAddWithoutValidation("Authorization", header);
    }
}
