namespace TaskManagement.TaskService.Models;

/// <summary>Проект — корень агрегата задач.</summary>
public class Project
{
    public long ManagerId { get; set; }
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime PlannedEndDate { get; set; }
    public bool IsArchived { get; set; }

    public List<Task> Tasks { get; set; } = new();
    public List<Participation> Participants { get; set; } = new();
}

/// <summary>
/// Задача. Статус хранится именем справочника («Новая», «Назначена», …),
/// что согласовано с диаграммами состояний и спецификацией прецедентов.
/// </summary>
public class Task
{
    public long Id { get; set; }
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime Deadline { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double EstimatedHours { get; set; }

    public string Status { get; set; } = "Новая";
    public string Priority { get; set; } = string.Empty;

    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public long? ExecutorId { get; set; }
    public User? Executor { get; set; }

    public List<Comment> Comments { get; set; } = new();
    public List<TaskHistory> History { get; set; } = new();
}

/// <summary>
/// Пользователь предметной области. Роль дублирует роль из IdentityService,
/// чтобы микросервис задач был автономен по данным (своя БД).
/// </summary>
public class User
{
    public long Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "Исполнитель";
    public bool IsActive { get; set; } = true;

    public List<Task> AssignedTasks { get; set; } = new();
    public List<Participation> Participations { get; set; } = new();
    public List<Comment> Comments { get; set; } = new();
    public List<TaskHistory> History { get; set; } = new();
}

/// <summary>Справочник статусов задачи.</summary>
public class TaskStatus
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsFinal { get; set; }
}

/// <summary>
/// Справочник «Правило перехода»: какой переход допустим для какой роли
/// и требуется ли обязательный комментарий.
/// </summary>
public class TransitionRule
{
    public long Id { get; set; }
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public bool RequireComment { get; set; }
}

/// <summary>Связь многие-ко-многим «Проект — Исполнитель» с атрибутом связи.</summary>
public class Participation
{
    public long ProjectId { get; set; }
    public long ExecutorId { get; set; }
    public DateTime JoinedAt { get; set; }

    public Project Project { get; set; } = null!;
    public User Executor { get; set; } = null!;
}

/// <summary>Комментарий к задаче (в том числе замечание при возврате).</summary>
public class Comment
{
    public long Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public long TaskId { get; set; }
    public Task Task { get; set; } = null!;

    public long AuthorId { get; set; }
    public User Author { get; set; } = null!;
}

/// <summary>Запись журнала изменений задачи.</summary>
public class TaskHistory
{
    public long Id { get; set; }
    public DateTime ChangedAt { get; set; }
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;

    public long TaskId { get; set; }
    public Task Task { get; set; } = null!;

    public long AuthorId { get; set; }
    public User Author { get; set; } = null!;
}

/// <summary>Справочник приоритетов.</summary>
public class Priority
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Weight { get; set; }
}
