using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Contracts.Models;

namespace TaskManagement.WebClient.Controllers;

[Route("account")]
public class AccountController(IHttpClientFactory factory) : Controller
{
    [HttpGet("login")]
    public IActionResult Login() => View();

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string login, string password)
    {
        try
        {
            using var response = await factory.CreateClient("IdentityService")
                .PostAsJsonAsync("api/auth/login", new LoginRequest { Login = login, Password = password });
            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Проверьте логин и пароль.";
                return View();
            }
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (result == null || string.IsNullOrWhiteSpace(result.Token))
                throw new HttpRequestException();
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, result.User.Id.ToString()),
                new Claim(ClaimTypes.Name, result.User.FullName),
                new Claim(ClaimTypes.Role, result.User.Role),
                new Claim("access_token", result.Token)
            };
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
                new AuthenticationProperties { ExpiresUtc = new DateTimeOffset(DateTime.SpecifyKind(result.ExpiresAt, DateTimeKind.Utc)) });
            return Redirect("/");
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            ViewBag.Error = "Сервис входа недоступен. Попробуйте позже.";
            return View();
        }
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
}
