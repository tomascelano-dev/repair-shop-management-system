using Serilog.Context;

namespace RepairShop.Api.Common;

/// <summary>
/// Ensures every request has a stable correlation id (X-Correlation-Id) and adds it to logging scope.
/// </summary>
public sealed class CorrelationIdMiddleware : IMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var corr = context.Request.Headers.TryGetValue(HeaderName, out var incoming)
            ? incoming.ToString().Trim()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(corr))
        {
            corr = Guid.NewGuid().ToString("N");
        }

        context.Items[ItemKey] = corr;
        context.Response.Headers[HeaderName] = corr;

        using (LogContext.PushProperty("CorrelationId", corr))
        using (context.RequestServices.GetRequiredService<ILogger<CorrelationIdMiddleware>>().BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = corr
               }))
        {
            await next(context);
        }
    }

    public static string? TryGet(HttpContext ctx)
        => ctx.Items.TryGetValue(ItemKey, out var v) ? v?.ToString() : null;
}
