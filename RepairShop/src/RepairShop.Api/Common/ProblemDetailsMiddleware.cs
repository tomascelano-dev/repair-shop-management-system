using System.Net;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Application.Common;
using RepairShop.Domain.Common;

namespace RepairShop.Api.Common;

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
        catch (Exception ex)
        {
            // Always log the server-side exception (incl. stack trace) so prod failures are debuggable.
            var corrForLog = CorrelationIdMiddleware.TryGet(context);
            _logger.LogError(ex,
                "Unhandled exception for {Method} {Path}. CorrelationId={CorrelationId}",
                context.Request.Method,
                context.Request.Path.Value,
                corrForLog);

            var (statusCode, title) = ex switch
            {
                RepairShop.Api.V2.WorkflowConflict => (409, "Actualización en conflicto"),
                RepairShop.Api.Saas.PlanRequiredException => (403, "Plan insuficiente"),
                RepairShop.Api.Saas.SubscriptionInactiveException => (402, "Suscripción inactiva"),
                RepairShop.Api.Saas.ForbiddenException => (403, "Forbidden"),
                UnauthorizedAccessException => (401, "Unauthorized"),
                DomainException => ((int)HttpStatusCode.BadRequest, "Domain validation error"),
                NotFoundException => ((int)HttpStatusCode.NotFound, "Resource not found"),
                UnauthorizedException => ((int)HttpStatusCode.Unauthorized, "Unauthorized"),
                LockedException => (StatusCodes.Status423Locked, "Locked"),
                TooManyRequestsException => (StatusCodes.Status429TooManyRequests, "Too Many Requests"),
                _ => ((int)HttpStatusCode.InternalServerError, "Unexpected error")
            };

            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = statusCode < 500 ? ex.Message : "Ocurrió un error interno. Revisá el registro del servidor.",
                Instance = context.Request.Path
            };

            // RFC7807 extensions
            problem.Extensions["traceId"] = context.TraceIdentifier;
            var corr = CorrelationIdMiddleware.TryGet(context);
            if (!string.IsNullOrWhiteSpace(corr)) problem.Extensions["correlationId"] = corr;

            if (ex is RetryAfterException ra)
            {
                context.Response.Headers["Retry-After"] = ra.RetryAfterSeconds.ToString();
                problem.Extensions["retryAfterSeconds"] = ra.RetryAfterSeconds;
            }

            // A simple stable "type" URI (keeps it standard without leaking internals)
            problem.Type = $"https://httpstatuses.com/{problem.Status}";

            context.Response.StatusCode = problem.Status.Value;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
