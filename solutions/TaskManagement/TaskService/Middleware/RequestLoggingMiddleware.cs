using System.Diagnostics;
using System.Security.Claims;
using log4net;

namespace TaskManagement.TaskService.Middleware;

/// <summary>
/// Протоколирует каждый HTTP-запрос к сервису: метод, путь, код ответа,
/// длительность обработки и пользователя из JWT-токена.
/// Успешные ответы пишутся как INFO, отказы 4xx — как WARN, ошибки 5xx — как ERROR.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(RequestLoggingMiddleware));

    private readonly RequestDelegate _next;

    public RequestLoggingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            Log.Error($"Ошибка при обработке {Describe(context)}", ex);
            throw;
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var line = $"{Describe(context)} → {context.Response.StatusCode} за {elapsed:F1} мс, " +
                       $"пользователь: {UserOf(context)}";

            if (context.Response.StatusCode >= 500)
            {
                Log.Error(line);
            }
            else if (context.Response.StatusCode >= 400)
            {
                Log.Warn(line);
            }
            else
            {
                Log.Info(line);
            }
        }
    }

    private static string Describe(HttpContext context) =>
        $"{context.Request.Method} {context.Request.Path}{context.Request.QueryString}";

    private static string UserOf(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return "анонимный";
        }

        var login = context.User.Identity.Name ?? "неизвестный";
        var role = context.User.FindFirst(ClaimTypes.Role)?.Value;
        return role is null ? login : $"{login} ({role})";
    }
}
