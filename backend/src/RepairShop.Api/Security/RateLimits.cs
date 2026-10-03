using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace RepairShop.Api.Security;

/// <summary>
/// Per-IP rate limits for anonymous endpoints. The IP comes from the connection after the forwarded
/// headers middleware ran, which only trusts X-Forwarded-For from configured proxies (so it can't be spoofed).
/// </summary>
public static class RateLimits
{
    /// <summary>Login, password reset and invitations: the brute-force targets.</summary>
    public const string Auth = "auth";

    /// <summary>Session refresh: called on every page load and tab, guarded by an unguessable httpOnly cookie + CSRF header.</summary>
    public const string Refresh = "refresh";

    public const string Public = "public";
    public const string Webhooks = "webhooks";

    public static void Configure(RateLimiterOptions options, IConfiguration config)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (ctx, ct) =>
        {
            if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                ctx.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

            ctx.HttpContext.Response.ContentType = "application/problem+json";
            await ctx.HttpContext.Response.WriteAsJsonAsync(new
            {
                status = 429,
                title = "Demasiadas solicitudes",
                detail = "Hiciste demasiadas solicitudes seguidas. Esperá un momento y volvé a intentar."
            }, ct);
        };

        Add(options, Auth, config.GetValue("RateLimiting:AuthPerMinute", 20));
        Add(options, Refresh, config.GetValue("RateLimiting:RefreshPerMinute", 120));
        Add(options, Public, config.GetValue("RateLimiting:PublicPerMinute", 60));
        Add(options, Webhooks, config.GetValue("RateLimiting:WebhooksPerMinute", 300));
    }

    private static void Add(RateLimiterOptions options, string name, int permitsPerMinute)
        => options.AddPolicy(name, http => RateLimitPartition.GetFixedWindowLimiter(
            $"{name}:{http.Connection.RemoteIpAddress}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, permitsPerMinute),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
}
