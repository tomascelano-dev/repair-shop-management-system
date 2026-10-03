using Microsoft.Extensions.Options;

namespace RepairShop.Api.Security;

public sealed class RefreshCookieOptions
{
    public const string SectionName = "Auth:RefreshCookie";

    public string Name { get; set; } = "rs_refresh";

    /// <summary>Strict | Lax | None. Use None (+ HTTPS) only when the SPA and the API live on different sites.</summary>
    public string SameSite { get; set; } = "Strict";

    /// <summary>Null = Secure when the request is HTTPS.</summary>
    public bool? Secure { get; set; }

    public string? Domain { get; set; }
}

/// <summary>
/// The refresh token lives in an httpOnly cookie scoped to /api/v1/auth, so JavaScript never sees it.
/// Endpoints that read it also require the X-RS-Refresh header: a cross-site form can't set custom headers (CSRF).
/// </summary>
public sealed class RefreshCookie
{
    public const string Path = "/api/v1/auth";
    public const string CsrfHeader = "X-RS-Refresh";

    private readonly RefreshCookieOptions _opt;

    public RefreshCookie(IOptions<RefreshCookieOptions> options) => _opt = options.Value;

    public string? Read(HttpRequest request) => request.Cookies.TryGetValue(_opt.Name, out var value) ? value : null;

    public bool HasCsrfHeader(HttpRequest request) => request.Headers.ContainsKey(CsrfHeader);

    public void Write(HttpResponse response, string token, DateTime expiresAtUtc)
        => response.Cookies.Append(_opt.Name, token, Options(response.HttpContext.Request, expiresAtUtc));

    public void Clear(HttpResponse response)
        => response.Cookies.Delete(_opt.Name, Options(response.HttpContext.Request, null));

    private CookieOptions Options(HttpRequest request, DateTime? expiresAtUtc)
    {
        var sameSite = Enum.TryParse<SameSiteMode>(_opt.SameSite, true, out var mode) ? mode : SameSiteMode.Strict;
        var secure = _opt.Secure ?? request.IsHttps;
        if (sameSite == SameSiteMode.None) secure = true; // browsers reject SameSite=None without Secure

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = sameSite,
            Path = Path,
            Domain = string.IsNullOrWhiteSpace(_opt.Domain) ? null : _opt.Domain,
            Expires = expiresAtUtc is null ? null : new DateTimeOffset(expiresAtUtc.Value, TimeSpan.Zero),
            IsEssential = true,
        };
    }
}
