using System.Text.Json.Serialization;

namespace TaskManagement.Contracts.Models;

/// <summary>Строка отчёта по статусу задач проекта.</summary>
public record TaskStatusReport
{
    [JsonPropertyName("statusName")] public string StatusName { get; set; } = string.Empty;
    [JsonPropertyName("taskCount")] public int TaskCount { get; set; }
    [JsonPropertyName("overdueCount")] public int OverdueCount { get; set; }
    [JsonPropertyName("percentage")] public double Percentage { get; set; }
    [JsonPropertyName("averageTimeInStatusHours")] public double AverageTimeInStatusHours { get; set; }
}

/// <summary>Отчёт по статусам задач проекта за период.</summary>
public record ProjectReportResponse
{
    [JsonPropertyName("projectName")] public string ProjectName { get; set; } = string.Empty;
    [JsonPropertyName("periodStart")] public DateTime PeriodStart { get; set; }
    [JsonPropertyName("periodEnd")] public DateTime PeriodEnd { get; set; }
    [JsonPropertyName("totalTasks")] public int TotalTasks { get; set; }
    [JsonPropertyName("statusReports")] public List<TaskStatusReport> StatusReports { get; set; } = new();
}
