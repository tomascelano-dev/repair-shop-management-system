using Microsoft.AspNetCore.Mvc;
using RepairShop.Application.Common;
using RepairShop.Domain.Common;

namespace RepairShop.Api.Common;

/// <summary>Maps exceptions to RFC 7807 responses with Spanish, user-facing messages.</summary>
public sealed class ProblemDetailsMiddleware : IMiddleware
{
    private readonly ILogger<ProblemDetailsMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ProblemDetailsMiddleware(IHostEnvironment env, ILogger<ProblemDetailsMiddleware> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client went away: nothing to answer, nothing to log as an error.
            if (!context.Response.HasStarted) context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            var (status, title, expose) = Map(ex);
            var correlationId = CorrelationIdMiddleware.TryGet(context);

            if (status >= 500)
                _logger.LogError(ex, "Unhandled exception for {Method} {Path}. CorrelationId={CorrelationId}", context.Request.Method, context.Request.Path.Value, correlationId);
            else
                _logger.LogInformation("Request {Method} {Path} failed with {Status}: {Message}", context.Request.Method, context.Request.Path.Value, status, ex.Message);

            if (context.Response.HasStarted) throw;

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = expose ? ex.Message : _env.IsDevelopment() ? ex.ToString() : "Ocurrió un error inesperado. Si persiste, informá el código de seguimiento.",
                Instance = context.Request.Path,
                Type = $"https://httpstatuses.com/{status}"
            };

            problem.Extensions["traceId"] = context.TraceIdentifier;
            if (!string.IsNullOrWhiteSpace(correlationId)) problem.Extensions["correlationId"] = correlationId;

            if (ex is RetryAfterException ra)
            {
                context.Response.Headers["Retry-After"] = ra.RetryAfterSeconds.ToString();
                problem.Extensions["retryAfterSeconds"] = ra.RetryAfterSeconds;
            }

            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem);
        }
    }

    private static (int Status, string Title, bool Expose) Map(Exception ex) => ex switch
    {
        DomainException => (StatusCodes.Status400BadRequest, "Datos inválidos", true),
        BadHttpRequestException b => (b.StatusCode, "Solicitud inválida", true),
        NotFoundException => (StatusCodes.Status404NotFound, "No encontrado", true),
        UnauthorizedException => (StatusCodes.Status401Unauthorized, "No autorizado", true),
        ForbiddenException => (StatusCodes.Status403Forbidden, "Sin permiso", true),
        ConflictException => (StatusCodes.Status409Conflict, "Conflicto", true),
        LockedException => (StatusCodes.Status423Locked, "Bloqueado", true),
        TooManyRequestsException => (StatusCodes.Status429TooManyRequests, "Demasiados intentos", true),
        _ => (StatusCodes.Status500InternalServerError, "Error inesperado", false)
    };
}
