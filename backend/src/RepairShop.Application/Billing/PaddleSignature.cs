using System.Security.Cryptography;
using System.Text;

namespace RepairShop.Application.Billing;

/// <summary>
/// Validates Paddle Billing webhook signatures (Paddle-Signature: "ts=1671552777;h1=..."):
/// HMAC-SHA256 of "{ts}:{raw body}" with the notification destination's secret key.
/// </summary>
public static class PaddleSignature
{
    public static bool IsValid(string secret, string? signatureHeader, string rawBody, DateTime nowUtc, TimeSpan? tolerance = null)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signatureHeader)) return false;

        string? ts = null;
        var signatures = new List<string>();
        foreach (var part in signatureHeader.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0] == "ts") ts = kv[1];
            else if (kv[0] == "h1") signatures.Add(kv[1].ToLowerInvariant());
        }
        if (ts is null || signatures.Count == 0 || !long.TryParse(ts, out var seconds)) return false;

        var when = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
        if ((nowUtc - when).Duration() > (tolerance ?? TimeSpan.FromMinutes(5))) return false;

        var expected = Encoding.ASCII.GetBytes(Sign(secret, ts, rawBody));
        // Paddle sends several h1 values while a secret is being rotated.
        return signatures.Any(s => CryptographicOperations.FixedTimeEquals(expected, Encoding.ASCII.GetBytes(s)));
    }

    public static string Sign(string secret, string ts, string rawBody)
        => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{ts}:{rawBody}"))).ToLowerInvariant();
}
