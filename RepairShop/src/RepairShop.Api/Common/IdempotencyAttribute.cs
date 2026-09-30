using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace RepairShop.Api.Common;

/// <summary>
/// Minimal idempotency support using the Idempotency-Key header.
/// If the same key is re-used for the same authenticated user + path, returns the cached response.
///
/// NOTE: This is in-memory (single instance). For production multi-replica deployments,
/// use Redis/DB.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute : Attribute, IAsyncActionFilter
{
    public const string HeaderName = "Idempotency-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;

        if (!http.Request.Headers.TryGetValue(HeaderName, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            await next();
            return;
        }

        var key = raw.ToString().Trim();
        if (key.Length > 128) key = key[..128];

        var user = http.User?.Identity?.IsAuthenticated == true
            ? (http.User.FindFirst("sub")?.Value ?? http.User.FindFirst("id")?.Value ?? "auth")
            : "anon";

        var cache = http.RequestServices.GetRequiredService<IMemoryCache>();
        var cacheKey = $"idem::{user}::{http.Request.Path}::{key}";

        if (cache.TryGetValue<IdempotencyCacheEntry>(cacheKey, out var cached) && cached is not null)
        {
            http.Response.Headers["X-Idempotency-Replay"] = "true";
            context.Result = new ContentResult
            {
                StatusCode = cached.StatusCode,
                ContentType = cached.ContentType,
                Content = cached.BodyJson
            };
            return;
        }

        var executed = await next();

        // Cache only successful JSON object results.
        if (executed.Result is ObjectResult obj && obj.Value is not null)
        {
            var jsonOpts = http.RequestServices.GetService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>()?.Value?.JsonSerializerOptions;
            var json = JsonSerializer.Serialize(obj.Value, jsonOpts ?? new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var ttl = TimeSpan.FromMinutes(10);

            cache.Set(cacheKey, new IdempotencyCacheEntry
            {
                StatusCode = obj.StatusCode ?? 200,
                ContentType = "application/json",
                BodyJson = json
            }, ttl);
        }
    }

    private sealed class IdempotencyCacheEntry
    {
        public int StatusCode { get; init; }
        public string ContentType { get; init; } = "application/json";
        public string BodyJson { get; init; } = "{}";
    }
}
