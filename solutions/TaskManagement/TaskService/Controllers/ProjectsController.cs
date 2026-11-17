using System.Security.Claims;
using log4net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Contracts.Models;
using TaskManagement.TaskService.Data;
using TaskManagement.TaskService.Models;

namespace TaskManagement.TaskService.Controllers;

/// <summary>
/// Проекты и участники: прецеденты «Вести справочник проектов»,
/// «Назначать исполнителей проекта», «Просмотреть задачи проекта».
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly TaskDbContext _context;
    private readonly ILog _logger;

    public ProjectsController(TaskDbContext context, ILog logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Список проектов.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProjectResponse>>> GetAll()
    {
        var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var query = _context.Projects.AsQueryable();
        query = User.IsInRole("Руководитель проекта")
            ? query.Where(p => p.ManagerId == userId)
            : query.Where(p => p.Participants.Any(x => x.ExecutorId == userId));
        var projects = await query.OrderBy(p => p.Code).ToListAsync();
        return Ok(projects.Select(p => new ProjectResponse
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            Description = p.Description,
            StartDate = p.StartDate,
            PlannedEndDate = p.PlannedEndDate,
            IsArchived = p.IsArchived
        }));
    }

    /// <summary>Проект по идентификатору.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ProjectResponse>> GetById(long id)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project == null)
            return NotFound(new { message = $"Проект {id} не найден" });

        if (!await CanAccessProject(project)) return Forbid();

        return Ok(new ProjectResponse
        {
            Id = project.Id,
            Code = project.Code,
            Name = project.Name,
            Description = project.Description,
            StartDate = project.StartDate,
            PlannedEndDate = project.PlannedEndDate,
            IsArchived = project.IsArchived
        });
    }

    /// <summary>Создать проект (прецедент «Вести справочник проектов»).</summary>
    [HttpPost]
    [Authorize(Roles = "Руководитель проекта")]
    public async Task<ActionResult<ProjectResponse>> Create([FromBody] ProjectResponse request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Название проекта обязательно" });

        var project = new Project
        {
            ManagerId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            Code = string.IsNullOrWhiteSpace(request.Code) ? $"PRJ-{Guid.NewGuid().ToString()[..6].ToUpper()}" : request.Code,
            Name = request.Name,
            Description = request.Description,
            StartDate = request.StartDate == default ? DateTime.UtcNow : TimeHelper.ToUtc(request.StartDate),
            PlannedEndDate = request.PlannedEndDate == default
                ? DateTime.UtcNow.AddMonths(3)
                : TimeHelper.ToUtc(request.PlannedEndDate),
            IsArchived = false
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        _logger.Info($"Создан проект {project.Id}: {project.Name}");
        if (!await CanAccessProject(project)) return Forbid();

        return Ok(new ProjectResponse
        {
            Id = project.Id,
            Code = project.Code,
            Name = project.Name,
            Description = project.Description,
            StartDate = project.StartDate,
            PlannedEndDate = project.PlannedEndDate,
            IsArchived = project.IsArchived
        });
    }

    /// <summary>Участники проекта.</summary>
    [HttpGet("{id:long}/participants")]
    public async Task<ActionResult<IEnumerable<ParticipantResponse>>> GetParticipants(long id)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project == null)
            return NotFound(new { message = $"Проект {id} не найден" });

        if (!await CanAccessProject(project)) return Forbid();

        var participants = await _context.Participations
            .Where(p => p.ProjectId == id)
            .Include(p => p.Executor)
            .OrderBy(p => p.ExecutorId)
            .ToListAsync();

        return Ok(participants.Select(p => new ParticipantResponse
        {
            ExecutorId = p.ExecutorId,
            ExecutorName = p.Executor.FullName,
            Role = p.Executor.Role,
            JoinedAt = p.JoinedAt
        }));
    }

    /// <summary>Назначить исполнителя проекта (связь многие-ко-многим).</summary>
    [HttpPost("{id:long}/participants")]
    [Authorize(Roles = "Руководитель проекта")]
    public async Task<ActionResult<ParticipantResponse>> AddParticipant(long id, [FromBody] long executorId)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project == null)
            return NotFound(new { message = $"Проект {id} не найден" });

        if (project.ManagerId != long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)) return Forbid();

        var executor = await _context.Users.FindAsync(executorId);
        if (executor == null || executor.Role != "Исполнитель")
            return BadRequest(new { message = $"Исполнитель {executorId} не найден" });

        var existing = await _context.Participations
            .FirstOrDefaultAsync(p => p.ProjectId == id && p.ExecutorId == executorId);
        if (existing != null)
            return Conflict(new { message = "Исполнитель уже участвует в проекте" });

        var participation = new Participation
        {
            ProjectId = id,
            ExecutorId = executorId,
            JoinedAt = DateTime.UtcNow
        };

        _context.Participations.Add(participation);
        await _context.SaveChangesAsync();

        _logger.Info($"Исполнитель {executorId} назначен участником проекта {id}");
        return Ok(new ParticipantResponse
        {
            ExecutorId = executorId,
            ExecutorName = executor.FullName,
            Role = executor.Role,
            JoinedAt = participation.JoinedAt
        });
    }
    private async System.Threading.Tasks.Task<bool> CanAccessProject(Project project)
    {
        var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return User.IsInRole("Руководитель проекта")
            ? project.ManagerId == userId
            : await _context.Participations.AnyAsync(p => p.ProjectId == project.Id && p.ExecutorId == userId);
    }
}
