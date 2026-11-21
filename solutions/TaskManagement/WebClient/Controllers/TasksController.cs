using log4net;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Contracts.Models;

namespace TaskManagement.WebClient.Controllers;

/// <summary>
/// Прокси к микросервису задач: прецеденты «Поставить задачу», «Просмотреть задачи»,
/// «Вернуть задачу на доработку» и другие. JWT-токен передаётся дальше в TaskService.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly ILog _logger;

    public TasksController(IHttpClientFactory httpFactory, ILog logger)
    {
        _httpClient = httpFactory.CreateClient("TaskService");
        _logger = logger;
    }

    /// <summary>Список задач с фильтрами.</summary>
    [HttpGet]
    public async Task<ActionResult> GetAll([FromQuery] long? projectId, [FromQuery] long? executorId, [FromQuery] string? status)
    {
        var query = "api/tasks";
        var filters = new List<string>();
        if (projectId.HasValue) filters.Add($"projectId={projectId.Value}");
        if (executorId.HasValue) filters.Add($"executorId={executorId.Value}");
        if (!string.IsNullOrWhiteSpace(status)) filters.Add($"status={Uri.EscapeDataString(status)}");
        if (filters.Count > 0) query += "?" + string.Join("&", filters);

        var request = new HttpRequestMessage(HttpMethod.Get, query);
        ForwardAuthorization(request);

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        return response.IsSuccessStatusCode
            ? Ok(content)
            : StatusCode((int)response.StatusCode, content);
    }

    /// <summary>Задача по идентификатору.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult> GetById(long id)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"api/tasks/{id}");
        ForwardAuthorization(request);

        var response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode
            ? Ok(await response.Content.ReadAsStringAsync())
            : StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    /// <summary>Поставить задачу.</summary>
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] TaskCreateRequest request)
    {
        _logger.Info($"Создание задачи: {request.Name}");
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/tasks")
        {
            Content = JsonContent.Create(request)
        };
        ForwardAuthorization(httpRequest);

        var response = await _httpClient.SendAsync(httpRequest);
        return response.IsSuccessStatusCode
            ? Ok(await response.Content.ReadAsStringAsync())
            : StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    /// <summary>Изменить статус задачи по правилу перехода.</summary>
    [HttpPost("{id:long}/status")]
    public async Task<ActionResult> ChangeStatus(long id, [FromBody] StatusChangeRequest request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"api/tasks/{id}/status")
        {
            Content = JsonContent.Create(request)
        };
        ForwardAuthorization(httpRequest);

        var response = await _httpClient.SendAsync(httpRequest);
        return response.IsSuccessStatusCode
            ? Ok(await response.Content.ReadAsStringAsync())
            : StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    /// <summary>Вернуть задачу на доработку (комментарий обязателен).</summary>
    [HttpPost("{id:long}/return")]
    public async Task<ActionResult> ReturnForRework(long id, [FromBody] CommentRequest request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"api/tasks/{id}/return")
        {
            Content = JsonContent.Create(request)
        };
        ForwardAuthorization(httpRequest);

        var response = await _httpClient.SendAsync(httpRequest);
        return response.IsSuccessStatusCode
            ? Ok(await response.Content.ReadAsStringAsync())
            : StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    /// <summary>Добавить комментарий к задаче.</summary>
    [HttpPost("{id:long}/comments")]
    public async Task<ActionResult> AddComment(long id, [FromBody] CommentRequest request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"api/tasks/{id}/comments")
        {
            Content = JsonContent.Create(request)
        };
        ForwardAuthorization(httpRequest);

        var response = await _httpClient.SendAsync(httpRequest);
        return response.IsSuccessStatusCode
            ? Ok(await response.Content.ReadAsStringAsync())
            : StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    /// <summary>Назначить исполнителя проекта.</summary>
    [HttpPost("{projectId:long}/participants")]
    public async Task<ActionResult> AddParticipant(long projectId, [FromBody] long executorId)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"api/projects/{projectId}/participants")
        {
            Content = JsonContent.Create(executorId)
        };
        ForwardAuthorization(httpRequest);

        var response = await _httpClient.SendAsync(httpRequest);
        return response.IsSuccessStatusCode
            ? Ok(await response.Content.ReadAsStringAsync())
            : StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [HttpPost("{id:long}/executor")]
    public async Task<ActionResult> AssignExecutor(long id, [FromBody] long executorId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/tasks/{id}/executor")
        {
            Content = JsonContent.Create(executorId)
        };
        ForwardAuthorization(request);
        using var response = await _httpClient.SendAsync(request);
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            ContentType = "application/json",
            Content = await response.Content.ReadAsStringAsync()
        };
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<ActionResult> Cancel(long id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/tasks/{id}/cancel");
        ForwardAuthorization(request);
        using var response = await _httpClient.SendAsync(request);
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            ContentType = "application/json",
            Content = await response.Content.ReadAsStringAsync()
        };
    }

    private void ForwardAuthorization(HttpRequestMessage request)
    {
        if (Request.Headers.Authorization.ToString() is { Length: > 0 } header)
            request.Headers.TryAddWithoutValidation("Authorization", header);
    }
}
