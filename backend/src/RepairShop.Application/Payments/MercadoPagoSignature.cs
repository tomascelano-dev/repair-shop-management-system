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

    /// <summary>Shape of an x-signature header for diagnostics: which parts it has and how old its timestamp is (seconds).</summary>
    public static (bool HasTs, bool HasV1, long? AgeSeconds) Describe(string? signatureHeader, DateTime nowUtc)
    {
        string? ts = null, v1 = null;
        foreach (var part in (signatureHeader ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0] == "ts") ts = kv[1];
            else if (kv[0] == "v1") v1 = kv[1];
        }
        long? age = null;
        if (long.TryParse(ts, out var t))
        {
            var when = t > 10_000_000_000 ? DateTimeOffset.FromUnixTimeMilliseconds(t) : DateTimeOffset.FromUnixTimeSeconds(t);
            age = (long)(nowUtc - when.UtcDateTime).TotalSeconds;
        }
        return (ts is not null, v1 is not null, age);
    }

    /// <summary>
    /// For a rejected notification: which other way of building the manifest the signature matches with this secret
    /// (a format difference on our side), or null when none does (the secret is not the one Mercado Pago signs with).
    /// </summary>
    public static string? MatchingVariant(string secret, string? signatureHeader, string? requestId, string? dataId)
    {
        string? ts = null, v1 = null;
        foreach (var part in (signatureHeader ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0] == "ts") ts = kv[1];
            else if (kv[0] == "v1") v1 = kv[1];
        }
        if (string.IsNullOrWhiteSpace(secret) || ts is null || v1 is null) return null;
        var id = dataId ?? "";
        var variants = new (string Name, string Manifest)[]
        {
            ("without request-id", $"id:{id.ToLowerInvariant()};ts:{ts};"),
            ("id as received", $"id:{id};request-id:{requestId};ts:{ts};"),
            ("without id", $"request-id:{requestId};ts:{ts};"),
            ("without trailing semicolon", $"id:{id.ToLowerInvariant()};request-id:{requestId};ts:{ts}"),
            ("secret trimmed", $"id:{id.ToLowerInvariant()};request-id:{requestId};ts:{ts};"),
        };
        foreach (var (name, manifest) in variants)
        {
            var key = name == "secret trimmed" ? secret.Trim() : secret;
            var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(v1.ToLowerInvariant()))) return name;
        }
        return null;
    }

    public static string Sign(string secret, string requestId, string dataId, string ts)
    {
        var manifest = $"id:{dataId.ToLowerInvariant()};request-id:{requestId};ts:{ts};";
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();
    }
}
