using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Application.Security;

namespace RepairShop.Api.Controllers;

/// <summary>
/// Sessions: short-lived access token in the response body (kept in memory by the SPA) and a rotating
/// refresh token in an httpOnly cookie. Cookie-reading endpoints require the X-RS-Refresh header (CSRF).
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly RefreshCookie _cookie;

    public AuthController(AuthService auth, RefreshCookie cookie)
    {
        _auth = auth;
        _cookie = cookie;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromServices] LoginSecurityService loginSecurity, [FromBody] LoginRequest body, CancellationToken ct)
    {
        loginSecurity.EnsureNotLocked(body.Email);
        try
        {
            var result = await _auth.LoginAsync(body, HttpContext.ClientIp(), HttpContext.UserAgent(), ct);
            loginSecurity.RegisterSuccess(body.Email);
            return Ok(Issue(result));
        }
        catch (UnauthorizedException)
        {
            loginSecurity.RegisterFailure(body.Email);
            throw;
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Refresh)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Refresh(CancellationToken ct)
    {
        RequireCsrfHeader();
        try
        {
            var result = await _auth.RefreshAsync(_cookie.Read(Request), HttpContext.ClientIp(), HttpContext.UserAgent(), ct);
            return Ok(Issue(result));
        }
        catch (UnauthorizedException)
        {
            _cookie.Clear(Response);
            throw;
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        RequireCsrfHeader();
        await _auth.LogoutAsync(_cookie.Read(Request), ct);
        _cookie.Clear(Response);
        return NoContent();
    }

    /// <summary>Closes every session of the current user (all devices) and invalidates issued access tokens.</summary>
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll([FromServices] IMemoryCache cache, CancellationToken ct)
    {
        await _auth.LogoutEverywhereAsync(CurrentUser.GetUserId(User), ct);
        SecurityStampValidator.Evict(cache, CurrentUser.GetUserId(User));
        _cookie.Clear(Response);
        return NoContent();
    }

    [HttpPost("switch-shop")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> SwitchShop([FromBody] SwitchShopRequest body, CancellationToken ct)
    {
        RequireCsrfHeader();
        var result = await _auth.SwitchShopAsync(CurrentUser.GetUserId(User), _cookie.Read(Request), body.ShopId, HttpContext.ClientIp(), HttpContext.UserAgent(), ct);
        return Ok(Issue(result));
    }

    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<MeResponse>>> Me(CancellationToken ct)
        => Ok(Envelope.Ok(await _auth.MeAsync(CurrentUser.GetUserId(User), CurrentUser.GetShopId(User), ct)));

    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> ChangePassword([FromServices] IMemoryCache cache, [FromBody] ChangePasswordRequest body, CancellationToken ct)
    {
        var result = await _auth.ChangePasswordAsync(CurrentUser.GetUserId(User), CurrentUser.GetShopId(User), body, HttpContext.ClientIp(), HttpContext.UserAgent(), ct);
        SecurityStampValidator.Evict(cache, CurrentUser.GetUserId(User));
        return Ok(Issue(result));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest body, CancellationToken ct)
    {
        await _auth.ForgotPasswordAsync(body.Email, ct);
        return Accepted(); // same answer whether the email exists or not
    }

    [HttpGet("token-info")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<ActionResult<ApiResponse<TokenInfoResponse>>> TokenInfo([FromQuery] string token, CancellationToken ct)
        => Ok(Envelope.Ok(await _auth.GetTokenInfoAsync(token, ct)));

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest body, CancellationToken ct)
    {
        await _auth.ResetPasswordAsync(body, ct);
        return NoContent();
    }

    [HttpPost("accept-invitation")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> AcceptInvitation([FromBody] AcceptInvitationRequest body, CancellationToken ct)
    {
        var result = await _auth.AcceptInvitationAsync(body, HttpContext.ClientIp(), HttpContext.UserAgent(), ct);
        return Ok(Issue(result));
    }

    private ApiResponse<LoginResponse> Issue(AuthResult result)
    {
        _cookie.Write(Response, result.RefreshToken, result.RefreshTokenExpiresAtUtc);
        Response.Headers.CacheControl = "no-store";
        return Envelope.Ok(result.Response);
    }

    private void RequireCsrfHeader()
    {
        if (!_cookie.HasCsrfHeader(Request))
            throw new ForbiddenException($"Falta el encabezado {RefreshCookie.CsrfHeader}.");
    }
}
