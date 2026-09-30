using System.Security.Cryptography;
using RepairShop.Application.Security;

namespace RepairShop.Infrastructure.Security;

/// <summary>
/// PBKDF2 password hashing (HMACSHA256).
/// Format: pbkdf2_sha256$<iterations>$<saltBase64>$<hashBase64>
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private const int MinPasswordLength = 10;

    public string Hash(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinPasswordLength)
            throw new ArgumentException($"Password must be at least {MinPasswordLength} characters.", nameof(password));

        // Minimal baseline: at least one letter and one digit.
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new ArgumentException("Password must contain at least one letter and one digit.", nameof(password));

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return $"pbkdf2_sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) return false;

        var parts = passwordHash.Split('$');
        if (parts.Length != 4) return false;
        if (parts[0] != "pbkdf2_sha256") return false;

        if (!int.TryParse(parts[1], out var iterations)) return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
