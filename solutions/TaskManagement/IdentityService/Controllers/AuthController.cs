using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using log4net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TaskManagement.Contracts.Models;
using TaskManagement.IdentityService.Data;
using TaskManagement.IdentityService.Models;
using TaskManagement.IdentityService.Services;

namespace TaskManagement.IdentityService.Controllers;

/// <summary>
/// Микросервис авторизации: прецеденты «Авторизоваться в системе»,
/// «Вести справочник пользователей», «Сменить пароль».
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IdentityDbContext _context;
    private readonly ILog _logger;
    private readonly IConfiguration _configuration;

    public AuthController(IdentityDbContext context, ILog logger, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>Вход: проверка логина и пароля, выдача JWT.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        _logger.Info($"Попытка входа пользователя '{request.Login}'");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == request.Login);
        if (user == null || !user.IsActive ||
            !PasswordHasher.Verify(request.Password, user.PasswordSalt, user.PasswordHash))
        {
            _logger.Warn($"Отказ в авторизации для '{request.Login}'");
            return Unauthorized(new { message = "Неверный логин или пароль" });
        }

        var token = CreateToken(user);
        var expires = DateTime.UtcNow.AddHours(8);

        _logger.Info($"Пользователь '{user.Login}' ({user.Role}) авторизован");

        return Ok(new LoginResponse
        {
            Token = token,
            ExpiresAt = expires,
            User = ToResponse(user)
        });
    }

    /// <summary>Регистрация пользователя (в справочнике — только руководитель проекта).</summary>
    [HttpPost("register")]
    public async Task<ActionResult<UserResponse>> Register([FromBody] RegisterRequest request)
    {
        _logger.Info($"Регистрация пользователя '{request.Login}' с ролью '{request.Role}'");

        if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Логин и пароль обязательны" });

        if (await _context.Users.AnyAsync(u => u.Login == request.Login))
            return Conflict(new { message = $"Пользователь '{request.Login}' уже существует" });

        var salt = PasswordHasher.CreateSalt();
        var user = new IdentityUser
        {
            Login = request.Login,
            PasswordSalt = salt,
            PasswordHash = PasswordHasher.Hash(request.Password, salt),
            FullName = request.FullName,
            Email = request.Email,
            Role = string.IsNullOrWhiteSpace(request.Role) ? "Исполнитель" : request.Role
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(ToResponse(user));
    }

    private string CreateToken(IdentityUser user)
    {
        var key = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Не задан Jwt:Key");
        var issuer = _configuration["Jwt:Issuer"] ?? "TaskManagement";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role),
            new("login", user.Login)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: issuer,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private static UserResponse ToResponse(IdentityUser user) => new()
    {
        Id = user.Id,
        Login = user.Login,
        FullName = user.FullName,
        Email = user.Email,
        Role = user.Role
    };
}
