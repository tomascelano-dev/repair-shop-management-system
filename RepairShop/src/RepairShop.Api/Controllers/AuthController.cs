using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Application.Security;

namespace RepairShop.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(
        [FromServices] AuthService auth,
        [FromServices] LoginSecurityService loginSecurity,
        [FromBody] LoginRequest body,
        CancellationToken ct)
    {
        // Basic operational security: IP rate limiting + lockout on repeated failures.
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        loginSecurity.EnforceRateLimit(ip);
        loginSecurity.EnsureNotLocked(body.Email);

        try
        {
            var res = await auth.LoginAsync(body, ct);
            loginSecurity.RegisterSuccess(body.Email);
            return Ok(new ApiResponse<LoginResponse>(res));
        }
        catch (UnauthorizedException)
        {
            loginSecurity.RegisterFailure(body.Email);
            throw;
        }
    }
}
