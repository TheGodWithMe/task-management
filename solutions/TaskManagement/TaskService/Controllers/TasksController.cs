using System.Security.Claims;
using log4net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Contracts.Models;
using TaskManagement.TaskService.Data;
using TaskManagement.TaskService.Models;
using Task = System.Threading.Tasks.Task;
using DomainTask = TaskManagement.TaskService.Models.Task;

namespace TaskManagement.TaskService.Controllers;

/// <summary>
/// Микросервис задач: прецеденты «Поставить задачу», «Назначить исполнителя»,
/// «Изменить статус задачи», «Вернуть задачу на доработку», «Добавить комментарий»,
/// «Отменить задачу», «Просмотреть задачи проекта».
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly TaskDbContext _context;
    private readonly ILog _logger;

    public TasksController(TaskDbContext context)
    {
        _context = context;
        // имя логгера = тип контроллера, чтобы в журнале было видно, кто пишет
        _logger = LogManager.GetLogger(typeof(TasksController));
    }

    /// <summary>Список задач с фильтрами (projectId, executorId, status).</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskResponse>>> GetAll(
        long? projectId = null, long? executorId = null, string? status = null)
    {
        var query = _context.Tasks
            .Include(t => t.Project)
            .Include(t => t.Executor)
            .AsQueryable();

        var userId = CurrentUserId();
        query = CurrentRole() == "Руководитель проекта"
            ? query.Where(t => t.Project.ManagerId == userId)
            : query.Where(t => t.ExecutorId == userId);

        if (projectId.HasValue) query = query.Where(t => t.ProjectId == projectId.Value);
        if (executorId.HasValue) query = query.Where(t => t.ExecutorId == executorId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(t => t.Status == status);

        var tasks = await query.OrderBy(t => t.Number).ToListAsync();
        _logger.Info($"Возвращено задач: {tasks.Count}");
        return Ok(tasks.Select(ToResponse));
    }

    /// <summary>Задача по идентификатору вместе с комментариями и историей.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<DomainTask>> GetById(long id)
    {
        var task = await _context.Tasks
            .Include(t => t.Project)
            .Include(t => t.Executor)
            .Include(t => t.Comments)
            .ThenInclude(c => c.Author)
            .Include(t => t.History)
            .ThenInclude(h => h.Author)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return NotFound(new { message = $"Задача {id} не найдена" });

        if (!CanAccess(task)) return Forbid();

        // Транспортное представление: возврат сущности привёл бы к циклу
        // Task -> Comments -> Task при сериализации.
        return Ok(new
        {
            task = ToResponse(task),
            comments = task.Comments
                .OrderBy(c => c.CreatedAt)
                .Select(c => new { c.Id, c.Text, c.CreatedAt, AuthorName = c.Author?.FullName ?? string.Empty }),
            history = task.History
                .OrderBy(h => h.ChangedAt)
                .Select(h => new { h.Id, h.ChangedAt, h.FromStatus, h.ToStatus, h.Comment, AuthorName = h.Author?.FullName ?? string.Empty })
        });
    }

    /// <summary>Поставить задачу (прецедент «Поставить задачу»).</summary>
    [HttpPost]
    [Authorize(Roles = "Руководитель проекта")]
    public async Task<ActionResult<TaskResponse>> Create([FromBody] TaskCreateRequest request)
    {
        var authorId = CurrentUserId();
        _logger.Info($"Создание задачи '{request.Name}' (проект {request.ProjectId}) пользователем {authorId}");

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Название задачи обязательно" });

        var project = await _context.Projects.FindAsync(request.ProjectId);
        if (project == null)
            return BadRequest(new { message = $"Проект {request.ProjectId} не найден" });

        if (project.ManagerId != authorId) return Forbid();
        if (project.IsArchived)
            return BadRequest(new { message = "Проект завершен" });
        if (request.Deadline != default && TimeHelper.ToUtc(request.Deadline).Date < DateTime.UtcNow.Date)
            return BadRequest(new { message = "Срок задачи не может быть в прошлом" });

        var priority = await _context.Priorities.FirstOrDefaultAsync(p => p.Name == request.Priority);
        if (priority == null)
            return BadRequest(new { message = $"Приоритет '{request.Priority}' не найден" });

        var number = await _context.Tasks.CountAsync() + 1;

        var task = new DomainTask
        {
            Number = number,
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            Deadline = request.Deadline == default
                ? DateTime.UtcNow.AddDays(7)
                : TimeHelper.ToUtc(request.Deadline),
            Status = "Новая",
            Priority = priority.Name,
            ProjectId = project.Id
        };

        // А1: задача создаётся без исполнителя — тогда остаётся «Новая»
        if (request.ExecutorId != 0)
        {
            var executor = await _context.Users.FindAsync(request.ExecutorId);
            if (executor == null || executor.Role != "Исполнитель")
                return BadRequest(new { message = $"Исполнитель {request.ExecutorId} не найден" });

            var isParticipant = await _context.Participations
                .AnyAsync(p => p.ProjectId == project.Id && p.ExecutorId == executor.Id);
            if (!isParticipant)
                return BadRequest(new { message = "Исполнитель не является участником проекта" });

            task.ExecutorId = executor.Id;
            task.Status = "Назначена";
        }

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();

        await AddHistoryAsync(task.Id, "—", task.Status, "Задача создана", authorId);

        _logger.Info($"Задача {task.Id} создана со статусом «{task.Status}»");
        return Ok(ToResponse(task));
    }

    /// <summary>Назначить или перенеазначить исполнителя.</summary>
    [HttpPost("{id:long}/executor")]
    [Authorize(Roles = "Руководитель проекта")]
    public async Task<ActionResult<TaskResponse>> AssignExecutor(long id, [FromBody] long executorId)
    {
        var task = await _context.Tasks.Include(t => t.Project).Include(t => t.Executor).FirstOrDefaultAsync(t => t.Id == id);
        if (task == null)
            return NotFound(new { message = $"Задача {id} не найдена" });

        if (!CanAccess(task)) return Forbid();
        if (task.Status is not ("Новая" or "Назначена" or "В работе"))
            return BadRequest(new { message = "Назначение исполнителя недоступно в текущем статусе" });
        var previous = task.Status;
        var executor = await _context.Users.FindAsync(executorId);
        if (executor == null)
            return BadRequest(new { message = $"Исполнитель {executorId} не найден" });

        var isParticipant = await _context.Participations
            .AnyAsync(p => p.ProjectId == task.ProjectId && p.ExecutorId == executorId);
        if (!isParticipant)
            return BadRequest(new { message = "Исполнитель не является участником проекта" });

        task.ExecutorId = executorId;
        if (task.Status == "Новая")
            task.Status = "Назначена";

        await AddHistoryAsync(task.Id, previous, task.Status, "Назначен исполнитель " + executor.FullName, CurrentUserId());
        _logger.Info($"Задача {id} «{task.Name}»: назначен исполнитель {executor.FullName}, " +
                     $"пользователь: {CurrentUserName()}");

        return Ok(ToResponse(task));
    }

    /// <summary>
    /// Изменить статус задачи по правилу перехода (справочник «Правило перехода»).
    /// </summary>
    [HttpPost("{id:long}/status")]
    public Task<ActionResult<TaskResponse>> ChangeStatus(long id, [FromBody] StatusChangeRequest request) =>
        ChangeStatusCore(id, request);

    private async Task<ActionResult<TaskResponse>> ChangeStatusCore(
        long id, StatusChangeRequest request, bool returningForRework = false)
    {
        var authorId = CurrentUserId();
        var role = CurrentRole();
        var task = await _context.Tasks.Include(t => t.Project).Include(t => t.Executor).FirstOrDefaultAsync(t => t.Id == id);
        if (task == null)
            return NotFound(new { message = $"Задача {id} не найдена" });

        if (!CanAccess(task)) return Forbid();

        if (returningForRework && role != "Руководитель проекта")
            return Forbid();

        if (returningForRework && task.Status != "На проверке")
            return BadRequest(new { message = "Вернуть на доработку можно только задачу на проверке" });

        var rule = await _context.TransitionRules.FirstOrDefaultAsync(r =>
            r.FromStatus == task.Status && r.ToStatus == request.NewStatus && r.UserRole == role);

        if (rule == null)
        {
            _logger.Warn($"Задача {id}: переход «{task.Status}» → «{request.NewStatus}» " +
                         $"запрещён для роли «{role}» (пользователь: {CurrentUserName()})");
            return BadRequest(new { message = $"Переход «{task.Status}» → «{request.NewStatus}» для роли «{role}» запрещён" });
        }

        if (rule.RequireComment && string.IsNullOrWhiteSpace(request.Comment))
        {
            _logger.Warn($"Задача {id}: переход «{task.Status}» → «{request.NewStatus}» отклонён — " +
                         $"обязателен комментарий (пользователь: {CurrentUserName()})");
            return BadRequest(new { message = "Для этого перехода обязателен комментарий" });
        }

        if (request.NewStatus == "Назначена" && task.ExecutorId == null)
            return BadRequest(new { message = "Сначала назначьте исполнителя задачи" });

        var previous = task.Status;
        task.Status = request.NewStatus;

        if (rule.ToStatus is "Завершена" or "Отменена")
            task.CompletedAt = DateTime.UtcNow;

        var changedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Comment))
        {
            _context.Comments.Add(new Comment
            {
                TaskId = task.Id,
                Text = request.Comment,
                CreatedAt = changedAt,
                AuthorId = authorId
            });
        }

        _context.TaskHistories.Add(new TaskHistory
        {
            TaskId = task.Id,
            ChangedAt = changedAt,
            FromStatus = previous,
            ToStatus = task.Status,
            Comment = request.Comment,
            AuthorId = authorId
        });
        // xmin проверяет, что задача не была изменена параллельным запросом.
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Задача изменена другим пользователем. Обновите данные." });
        }

        _logger.Info($"Задача {id} «{task.Name}»: «{previous}» → «{task.Status}», " +
                     $"пользователь: {CurrentUserName()} ({role})" +
                     (string.IsNullOrWhiteSpace(request.Comment) ? string.Empty : $", комментарий: «{request.Comment}»"));
        return Ok(ToResponse(task));
    }

    /// <summary>Вернуть задачу на доработку (прецедент «Вернуть задачу на доработку»).</summary>
    [HttpPost("{id:long}/return")]
    public async Task<ActionResult<TaskResponse>> ReturnForRework(long id, [FromBody] CommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { message = "Возврат без комментария запрещён" });

        return await ChangeStatusCore(id, new StatusChangeRequest
        {
            NewStatus = "В работе",
            Comment = request.Text
        }, returningForRework: true);
    }

    /// <summary>Добавить комментарий к задаче.</summary>
    [HttpPost("{id:long}/comments")]
    public async Task<ActionResult> AddComment(long id, [FromBody] CommentRequest request)
    {
        var task = await _context.Tasks.Include(t => t.Project).FirstOrDefaultAsync(t => t.Id == id);
        if (task == null)
            return NotFound(new { message = $"Задача {id} не найдена" });

        if (!CanAccess(task)) return Forbid();

        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { message = "Текст комментария обязателен" });

        _context.Comments.Add(new Comment
        {
            TaskId = id,
            Text = request.Text,
            CreatedAt = DateTime.UtcNow,
            AuthorId = CurrentUserId()
        });
        await _context.SaveChangesAsync();

        _logger.Info($"К задаче {id} добавлен комментарий");
        return Ok(new { message = "Комментарий добавлен" });
    }

    /// <summary>Отменить задачу (только руководитель, из любого нефинального статуса).</summary>
    [HttpPost("{id:long}/cancel")]
    [Authorize(Roles = "Руководитель проекта")]
    public Task<ActionResult<TaskResponse>> Cancel(long id) =>
        ChangeStatusCore(id, new StatusChangeRequest
        {
            NewStatus = "Отменена",
            Comment = "Задача отменена"
        });

    // ------------------------------- helpers --------------------------------

    private bool CanAccess(DomainTask task) =>
        CurrentRole() == "Руководитель проекта"
            ? task.Project?.ManagerId == CurrentUserId()
            : CurrentRole() == "Исполнитель" && task.ExecutorId == CurrentUserId();

    private long CurrentUserId() =>
        long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    private string CurrentRole() => User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

    private string CurrentUserName() => User.FindFirst(ClaimTypes.Name)?.Value ?? "неизвестный";

    private async Task AddHistoryAsync(long taskId, string from, string to, string comment, long authorId)
    {
        _context.TaskHistories.Add(new TaskHistory
        {
            TaskId = taskId,
            ChangedAt = DateTime.UtcNow,
            FromStatus = from,
            ToStatus = to,
            Comment = comment,
            AuthorId = authorId
        });
        await _context.SaveChangesAsync();
    }

    private static TaskResponse ToResponse(DomainTask t) => new()
    {
        Id = t.Id,
        Number = t.Number,
        Name = t.Name,
        Description = t.Description,
        CreatedAt = t.CreatedAt,
        Deadline = t.Deadline,
        Status = t.Status,
        Priority = t.Priority,
        ProjectId = t.ProjectId,
        ProjectName = t.Project?.Name ?? string.Empty,
        ExecutorId = t.ExecutorId ?? 0,
        ExecutorName = t.Executor?.FullName ?? string.Empty,
        Overdue = t.Deadline < DateTime.UtcNow && t.Status is not ("Завершена" or "Отменена")
    };
}
