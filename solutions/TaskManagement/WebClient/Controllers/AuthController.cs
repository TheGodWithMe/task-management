using log4net;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Contracts.Models;

namespace TaskManagement.WebClient.Controllers;

/// <summary>
/// Вход в систему (прецедент «Авторизоваться в системе»).
/// WebClient проверяет логин и пароль через IdentityService и возвращает JWT,
/// который дальше используется для обращения к TaskService и ReportService.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly ILog _logger;

    public AuthController(IHttpClientFactory httpFactory, ILog logger)
    {
        _httpClient = httpFactory.CreateClient("IdentityService");
        _logger = logger;
    }

    /// <summary>Вход: логин, пароль → JWT-токен.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        _logger.Info($"Попытка входа пользователя {request.Login}");

        var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.Warn($"Отказ в авторизации для '{request.Login}': {response.StatusCode}");
            return StatusCode((int)response.StatusCode, content);
        }

        _logger.Info($"Пользователь '{request.Login}' авторизован");
        return Ok(System.Text.Json.JsonSerializer.Deserialize<LoginResponse>(content,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }));
    }

    /// <summary>Список пользователей справочника (прецедент «Вести справочник пользователей»).</summary>
    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetUsers()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "api/users");
        ForwardAuthorization(request);

        var response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode
            ? Ok(await response.Content.ReadAsStringAsync())
            : StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private void ForwardAuthorization(HttpRequestMessage request)
    {
        if (Request.Headers.Authorization.ToString() is { Length: > 0 } header)
            request.Headers.TryAddWithoutValidation("Authorization", header);
    }
}
