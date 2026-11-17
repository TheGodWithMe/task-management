namespace TaskManagement.IdentityService.Models;

/// <summary>
/// Учётная запись пользователя. Роль — «Руководитель проекта» или «Исполнитель»
/// (по заданию администратора нет).
/// </summary>
public class IdentityUser
{
    public long Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
