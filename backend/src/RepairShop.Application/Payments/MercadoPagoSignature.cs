using System.Security.Cryptography;
using System.Text;

namespace RepairShop.Application.Payments;

/// <summary>
/// Validates Mercado Pago webhook signatures (x-signature: "ts=...,v1=...").
/// Manifest: "id:{data.id};request-id:{x-request-id};ts:{ts};" signed with HMAC-SHA256 and the webhook secret.
/// </summary>
public static class MercadoPagoSignature
{
    public static bool IsValid(string secret, string? signatureHeader, string? requestId, string? dataId, DateTime nowUtc, TimeSpan? tolerance = null)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(dataId)) return false;

        string? ts = null, v1 = null;
        foreach (var part in signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0] == "ts") ts = kv[1];
            else if (kv[0] == "v1") v1 = kv[1];
        }
        if (ts is null || v1 is null) return false;

        if (tolerance is not null && long.TryParse(ts, out var tsValue))
        {
            // ts may come in seconds or milliseconds.
            var when = tsValue > 10_000_000_000 ? DateTimeOffset.FromUnixTimeMilliseconds(tsValue) : DateTimeOffset.FromUnixTimeSeconds(tsValue);
            if ((nowUtc - when.UtcDateTime).Duration() > tolerance) return false;
        }

        var id = dataId.All(char.IsLetterOrDigit) ? dataId.ToLowerInvariant() : dataId;
        var manifest = $"id:{id};" + (string.IsNullOrWhiteSpace(requestId) ? "" : $"request-id:{requestId};") + $"ts:{ts};";
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(v1.ToLowerInvariant()));
    }

    public static string Sign(string secret, string requestId, string dataId, string ts)
    {
        var manifest = $"id:{dataId.ToLowerInvariant()};request-id:{requestId};ts:{ts};";
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();
    }
}
