using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RepairShop.Api.Common;

public static class HealthCheckResponseWriter
{
    public static Task WriteJson(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            correlationId = CorrelationIdMiddleware.TryGet(context),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = (int)e.Value.Duration.TotalMilliseconds
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
