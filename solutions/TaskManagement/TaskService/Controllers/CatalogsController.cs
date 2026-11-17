using log4net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Contracts.Models;
using TaskManagement.TaskService.Data;
using TaskManagement.TaskService.Models;

namespace TaskManagement.TaskService.Controllers;

/// <summary>
/// Справочники: статусы задачи, правила перехода, приоритеты.
/// Данные справочников используются композитным сервисом отчётов.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CatalogsController : ControllerBase
{
    private readonly TaskDbContext _context;
    private readonly ILog _logger;

    public CatalogsController(TaskDbContext context, ILog logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Справочник статусов задачи.</summary>
    [HttpGet("statuses")]
    public async Task<ActionResult> GetStatuses()
    {
        var statuses = await _context.TaskStatuses.OrderBy(s => s.Order).ToListAsync();
        return Ok(statuses);
    }

    /// <summary>Справочник правил перехода (переход, роль, обязательный комментарий).</summary>
    [HttpGet("transition-rules")]
    public async Task<ActionResult> GetTransitionRules()
    {
        var rules = await _context.TransitionRules.OrderBy(r => r.FromStatus).ThenBy(r => r.ToStatus).ToListAsync();
        return Ok(rules);
    }

    /// <summary>Справочник приоритетов.</summary>
    [HttpGet("priorities")]
    public async Task<ActionResult> GetPriorities()
    {
        var priorities = await _context.Priorities.OrderByDescending(p => p.Weight).ToListAsync();
        return Ok(priorities);
    }

    /// <summary>Записи истории задачи (для отчёта по времени в статусе).</summary>
    [HttpGet("history/{taskId:long}")]
    public async Task<ActionResult> GetHistory(long taskId)
    {
        var userId = long.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var task = await _context.Tasks.Include(t => t.Project).FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null) return NotFound();
        var allowed = User.IsInRole("Руководитель проекта")
            ? task.Project.ManagerId == userId
            : task.ExecutorId == userId;
        if (!allowed) return Forbid();

        var history = await _context.TaskHistories
            .Where(h => h.TaskId == taskId)
            .Include(h => h.Author)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync();

        _logger.Info($"Запрошена история задачи {taskId}: {history.Count} записей");

        // Возвращается транспортная модель, иначе сериализатор уходит в цикл
        // TaskHistory -> Task -> TaskHistory.
        return Ok(history.Select(h => new TaskHistoryResponse
        {
            Id = h.Id,
            ChangedAt = h.ChangedAt,
            FromStatus = h.FromStatus,
            ToStatus = h.ToStatus,
            AuthorName = h.Author?.FullName ?? string.Empty,
            Comment = h.Comment
        }));
    }
}
