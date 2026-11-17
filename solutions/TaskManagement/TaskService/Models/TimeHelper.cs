namespace TaskManagement.TaskService.Models;

/// <summary>
/// Приводит даты к UTC: столбцы PostgreSQL типа timestamptz принимают
/// только DateTime с Kind=Utc, иначе Npgsql бросает исключение.
/// </summary>
public static class TimeHelper
{
    public static DateTime ToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public static DateTime? ToUtc(DateTime? value) =>
        value is null ? value : ToUtc(value.Value);
}
