using System.Security.Cryptography;
using System.Text;

namespace RepairShop.Application.Security;

/// <summary>Opaque random tokens (refresh, invitations, password resets). Only hashes are stored.</summary>
public static class TokenHasher
{
    public static string NewToken(int bytes = 32)
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? ""))).ToLowerInvariant();
}
