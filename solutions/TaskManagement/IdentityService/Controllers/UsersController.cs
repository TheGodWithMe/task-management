using log4net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Contracts.Models;
using TaskManagement.IdentityService.Data;

namespace TaskManagement.IdentityService.Controllers;

/// <summary>
/// Справочник пользователей: чтение учётных записей и их ролей.
/// Используется WebClient и композитным сервисом отчётов для подстановки ФИО.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IdentityDbContext _context;
    private readonly ILog _logger;

    public UsersController(IdentityDbContext context, ILog logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Список пользователей (прецедент «Вести справочник пользователей»).</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetAll()
    {
        _logger.Info("Запрос списка пользователей");
        var users = await _context.Users.OrderBy(u => u.Id).ToListAsync();
        return Ok(users.Select(u => new UserResponse
        {
            Id = u.Id,
            Login = u.Login,
            FullName = u.FullName,
            Email = u.Email,
            Role = u.Role
        }));
    }

    /// <summary>Пользователь по идентификатору.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<UserResponse>> GetById(long id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { message = $"Пользователь {id} не найден" });

        return Ok(new UserResponse
        {
            Id = user.Id,
            Login = user.Login,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role
        });
    }
}
