using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RepairShop.Application.Common;

namespace RepairShop.Api.Security;

/// <summary>
/// Basic, in-memory operational security for the login endpoint:
/// - IP-based rate limiting
/// - Identifier (email) lockout on repeated failures
///
/// NOTE: This is intentionally simple and does not survive restarts.
/// In multi-instance deployments, replace with a shared store (Redis) or a gateway/WAF.
/// </summary>
public sealed class LoginSecurityService
{
    private readonly IMemoryCache _cache;
    private readonly IOptionsMonitor<LoginSecurityOptions> _options;

    public LoginSecurityService(IMemoryCache cache, IOptionsMonitor<LoginSecurityOptions> options)
    {
        _cache = cache;
        _options = options;
    }

    public void EnforceRateLimit(string ip)
    {
        ip = NormalizeIp(ip);
        var opt = _options.CurrentValue.RateLimit;

        var windowSeconds = Math.Max(1, opt.WindowSeconds);
        var permitLimit = Math.Max(1, opt.PermitLimit);

        var key = $"login:rl:ip:{ip}";
        var now = DateTimeOffset.UtcNow;

        if (!_cache.TryGetValue(key, out RateLimitEntry entry) || now >= entry.WindowEndUtc)
        {
            entry = new RateLimitEntry(0, now.AddSeconds(windowSeconds));
        }

        entry = entry with { Count = entry.Count + 1 };
        _cache.Set(key, entry, entry.WindowEndUtc);

        if (entry.Count > permitLimit)
        {
            var retryAfter = (int)Math.Ceiling((entry.WindowEndUtc - now).TotalSeconds);
            retryAfter = Math.Max(1, retryAfter);
            throw new TooManyRequestsException(
                "Demasiados intentos de inicio de sesión. Probá de nuevo en unos minutos.",
                retryAfter);
        }
    }

    // Security audit 2026-11-23 (M-09): the lockout used to key on the email alone, so anybody who knew an owner's address
    // could keep them out with 5 wrong passwords every 10 minutes. Now the normal lockout keys on email + client IP, and
    // the email alone is only locked after many more failures (a distributed guessing attack), which no single
    // address can trigger by itself.
    public const int EmailWideFactor = 10;

    private static string PairKey(string email, string? ip) => $"login:lock:pair:{NormalizeEmail(email)}|{NormalizeIp(ip ?? "")}";
    private static string EmailKey(string email) => $"login:lock:email:{NormalizeEmail(email)}";

    public void EnsureNotLocked(string email, string? ip)
    {
        Ensure(PairKey(email, ip));
        Ensure(EmailKey(email));
    }

    private void Ensure(string key)
    {
        var now = DateTimeOffset.UtcNow;
        if (_cache.TryGetValue(key, out LockoutEntry entry) && entry.LockedUntilUtc is not null)
        {
            var until = entry.LockedUntilUtc.Value;
            if (until > now)
            {
                var retryAfter = (int)Math.Ceiling((until - now).TotalSeconds);
                retryAfter = Math.Max(1, retryAfter);
                throw new LockedException(
                    "La cuenta quedó bloqueada temporalmente por varios intentos fallidos. Probá de nuevo más tarde.",
                    retryAfter);
            }
        }
    }

    public void RegisterSuccess(string email, string? ip)
    {
        _cache.Remove(PairKey(email, ip));
    }

    public void RegisterFailure(string email, string? ip)
    {
        var opt = _options.CurrentValue.Lockout;
        var maxAttempts = Math.Max(1, opt.MaxFailedAttempts);
        RegisterFailure(PairKey(email, ip), maxAttempts);
        RegisterFailure(EmailKey(email), maxAttempts * EmailWideFactor);
    }

    private void RegisterFailure(string key, int maxAttempts)
    {
        var opt = _options.CurrentValue.Lockout;

        var cooldownSeconds = Math.Max(10, opt.CooldownSeconds);
        var resetSeconds = Math.Max(60, opt.FailureResetSeconds);

        var now = DateTimeOffset.UtcNow;

        LockoutEntry entry;
        if (_cache.TryGetValue(key, out LockoutEntry existing))
        {
            // If last failure was long ago, reset the counter.
            if (now - existing.LastFailureUtc > TimeSpan.FromSeconds(resetSeconds))
            {
                entry = new LockoutEntry(0, null, now);
            }
            else
            {
                entry = existing;
            }
        }
        else
        {
            entry = new LockoutEntry(0, null, now);
        }

        // If currently locked, keep it locked.
        if (entry.LockedUntilUtc is not null && entry.LockedUntilUtc.Value > now)
        {
            _cache.Set(key, entry with { LastFailureUtc = now }, entry.LockedUntilUtc.Value);
            return;
        }

        var newCount = entry.FailureCount + 1;
        DateTimeOffset? lockedUntil = null;

        if (newCount >= maxAttempts)
        {
            lockedUntil = now.AddSeconds(cooldownSeconds);
            // Reset counter after lockout is applied.
            newCount = 0;
        }

        var newEntry = new LockoutEntry(newCount, lockedUntil, now);

        // Expire when lockout ends OR after reset window (whichever is later).
        var expiration = lockedUntil ?? now.AddSeconds(resetSeconds);
        if (expiration < now.AddSeconds(resetSeconds))
        {
            expiration = now.AddSeconds(resetSeconds);
        }

        _cache.Set(key, newEntry, expiration);
    }

    private static string NormalizeEmail(string email)
        => string.IsNullOrWhiteSpace(email) ? "" : email.Trim().ToLowerInvariant();

    private static string NormalizeIp(string ip)
        => string.IsNullOrWhiteSpace(ip) ? "unknown" : ip.Trim();

    private readonly record struct RateLimitEntry(int Count, DateTimeOffset WindowEndUtc);
    private readonly record struct LockoutEntry(int FailureCount, DateTimeOffset? LockedUntilUtc, DateTimeOffset LastFailureUtc);
}
