using log4net;
using Microsoft.AspNetCore.Mvc;

namespace TaskManagement.WebClient.Controllers;

/// <summary>
/// Прокси к справочнику проектов TaskService.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly ILog _logger;

    public ProjectsController(IHttpClientFactory httpFactory, ILog logger)
    {
        _httpClient = httpFactory.CreateClient("TaskService");
        _logger = logger;
    }

    /// <summary>Список проектов.</summary>
    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "api/projects");
        ForwardAuthorization(request);

        var response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode
            ? Ok(await response.Content.ReadAsStringAsync())
            : StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    /// <summary>Проект по идентификатору.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult> GetById(long id)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"api/projects/{id}");
        ForwardAuthorization(request);

        var response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode
            ? Ok(await response.Content.ReadAsStringAsync())
            : StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    /// <summary>Создать проект.</summary>
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] object requestBody)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "api/projects")
        {
            Content = JsonContent.Create(requestBody)
        };
        ForwardAuthorization(request);

        var response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode
            ? Ok(await response.Content.ReadAsStringAsync())
            : StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    /// <summary>Участники проекта.</summary>
    [HttpGet("{id:long}/participants")]
    public async Task<ActionResult> GetParticipants(long id)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"api/projects/{id}/participants");
        ForwardAuthorization(request);

        var response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode
            ? Ok(await response.Content.ReadAsStringAsync())
            : StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult> Update(long id, [FromBody] object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/projects/{id}")
        {
            Content = JsonContent.Create(body)
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

    [HttpPost("{id:long}/complete")]
    public async Task<ActionResult> Complete(long id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/projects/{id}/complete");
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
