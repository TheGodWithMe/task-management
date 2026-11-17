using System.Security.Cryptography;
using System.Text;

namespace TaskManagement.IdentityService.Services;

/// <summary>
/// Хеширование паролей: SHA-256 от «соль + пароль». Для учебного проекта
/// достаточно, в промышленном коде используется PBKDF2/Argon2.
/// </summary>
public static class PasswordHasher
{
    private const int SaltSize = 16;

    public static string CreateSalt()
    {
        var bytes = new byte[SaltSize];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    public static string Hash(string password, string salt)
    {
        using var sha = SHA256.Create();
        var raw = sha.ComputeHash(Encoding.UTF8.GetBytes(salt + password));
        return Convert.ToBase64String(raw);
    }

    public static bool Verify(string password, string salt, string expectedHash) =>
        Hash(password, salt) == expectedHash;
}
