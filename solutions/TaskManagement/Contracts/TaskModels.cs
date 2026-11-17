using System.Text.Json.Serialization;

namespace TaskManagement.Contracts.Models;

// ----------------------------- IdentityService -----------------------------

/// <summary>Запрос на вход в систему (прецедент «Авторизоваться в системе»).</summary>
public record LoginRequest
{
    [JsonPropertyName("login")] public string Login { get; set; } = string.Empty;
    [JsonPropertyName("password")] public string Password { get; set; } = string.Empty;
}

/// <summary>Ответ IdentityService: JWT-токен и данные пользователя.</summary>
public record LoginResponse
{
    [JsonPropertyName("token")] public string Token { get; set; } = string.Empty;
    [JsonPropertyName("expiresAt")] public DateTime ExpiresAt { get; set; }
    [JsonPropertyName("user")] public UserResponse User { get; set; } = new();
}

/// <summary>Регистрация пользователя (прецедент «Вести справочник пользователей»).</summary>
public record RegisterRequest
{
    [JsonPropertyName("login")] public string Login { get; set; } = string.Empty;
    [JsonPropertyName("password")] public string Password { get; set; } = string.Empty;
    [JsonPropertyName("fullName")] public string FullName { get; set; } = string.Empty;
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
}

/// <summary>Транспортная модель пользователя (роль — «Руководитель проекта» или «Исполнитель»).</summary>
public record UserResponse
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("login")] public string Login { get; set; } = string.Empty;
    [JsonPropertyName("fullName")] public string FullName { get; set; } = string.Empty;
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
}

// -------------------------------- TaskService ------------------------------

/// <summary>Создание задачи (прецедент «Поставить задачу»).</summary>
public record TaskCreateRequest
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("projectId")] public long ProjectId { get; set; }
    [JsonPropertyName("executorId")] public long ExecutorId { get; set; }
    [JsonPropertyName("priority")] public string Priority { get; set; } = string.Empty;
    [JsonPropertyName("deadline")] public DateTime Deadline { get; set; }
}

/// <summary>Смена статуса задачи (справочник «Правило перехода»).</summary>
public record StatusChangeRequest
{
    [JsonPropertyName("newStatus")] public string NewStatus { get; set; } = string.Empty;
    [JsonPropertyName("comment")] public string Comment { get; set; } = string.Empty;
}

/// <summary>Комментарий к задаче (прецедент «Добавить комментарий к задаче»).</summary>
public record CommentRequest
{
    [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
}

/// <summary>Задача в транспортном представлении.</summary>
public record TaskResponse
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("number")] public int Number { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; set; }
    [JsonPropertyName("deadline")] public DateTime Deadline { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("priority")] public string Priority { get; set; } = string.Empty;
    [JsonPropertyName("projectId")] public long ProjectId { get; set; }
    [JsonPropertyName("projectName")] public string ProjectName { get; set; } = string.Empty;
    [JsonPropertyName("executorId")] public long ExecutorId { get; set; }
    [JsonPropertyName("executorName")] public string ExecutorName { get; set; } = string.Empty;
    [JsonPropertyName("overdue")] public bool Overdue { get; set; }
}

/// <summary>Проект в транспортном представлении.</summary>
public record ProjectResponse
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("startDate")] public DateTime StartDate { get; set; }
    [JsonPropertyName("plannedEndDate")] public DateTime PlannedEndDate { get; set; }
    [JsonPropertyName("isArchived")] public bool IsArchived { get; set; }
}

/// <summary>Участник проекта (связь многие-ко-многим с атрибутом «Дата включения»).</summary>
public record ParticipantResponse
{
    [JsonPropertyName("executorId")] public long ExecutorId { get; set; }
    [JsonPropertyName("executorName")] public string ExecutorName { get; set; } = string.Empty;
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
    [JsonPropertyName("joinedAt")] public DateTime JoinedAt { get; set; }
}

/// <summary>Запись истории задачи.</summary>
public record TaskHistoryResponse
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("changedAt")] public DateTime ChangedAt { get; set; }
    [JsonPropertyName("fromStatus")] public string FromStatus { get; set; } = string.Empty;
    [JsonPropertyName("toStatus")] public string ToStatus { get; set; } = string.Empty;
    [JsonPropertyName("authorName")] public string AuthorName { get; set; } = string.Empty;
    [JsonPropertyName("comment")] public string Comment { get; set; } = string.Empty;
}

// ------------------------------- ReportService ------------------------------
// Транспортные модели отчёта вынесены в ReportModels.cs.
