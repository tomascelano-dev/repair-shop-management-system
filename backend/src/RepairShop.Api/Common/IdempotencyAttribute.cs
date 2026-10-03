using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using RepairShop.Infrastructure.Idempotency;

namespace RepairShop.Api.Common;

/// <summary>
/// Idempotency-Key support for POST endpoints that create money or stock movements (sales, payments,
/// orders...). The first response is stored in the database for 24 h and replayed for retries with the
/// same key and body, across all API instances. A different body with the same key returns 422; a
/// concurrent duplicate returns 409.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute : Attribute, IAsyncActionFilter
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayHeader = "X-Idempotency-Replay";
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (!http.Request.Headers.TryGetValue(HeaderName, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            await next();
            return;
        }

        var key = raw.ToString().Trim();
        if (key.Length > 200)
        {
            context.Result = Problem(http, StatusCodes.Status400BadRequest, "El Idempotency-Key es demasiado largo.");
            return;
        }

        var user = CurrentUser.GetUserId(http.User);
        var shop = CurrentUser.GetShopId(http.User);
        var id = Hash($"{user:N}|{shop:N}|{http.Request.Method}|{http.Request.Path}|{key}");

        var jsonOptions = http.RequestServices.GetService<IOptions<JsonOptions>>()?.Value.JsonSerializerOptions
                          ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var bodyHash = Hash(JsonSerializer.Serialize(context.ActionArguments.Where(a => a.Value is not CancellationToken)
            .ToDictionary(a => a.Key, a => a.Value), jsonOptions));

        var store = http.RequestServices.GetRequiredService<IdempotencyStore>();
        var begin = await store.BeginAsync(id, bodyHash, Ttl, http.RequestAborted);

        switch (begin.Outcome)
        {
            case IdempotencyOutcome.Replay:
                http.Response.Headers[ReplayHeader] = "true";
                context.Result = new ContentResult { StatusCode = begin.StatusCode, ContentType = "application/json", Content = begin.ResponseBody ?? "" };
                return;
            case IdempotencyOutcome.InProgress:
                context.Result = Problem(http, StatusCodes.Status409Conflict, "Esta operación ya se está procesando. Esperá unos segundos.");
                return;
            case IdempotencyOutcome.Mismatch:
                context.Result = Problem(http, StatusCodes.Status422UnprocessableEntity, "El Idempotency-Key ya se usó con otros datos.");
                return;
        }

        ActionExecutedContext executed;
        try
        {
            executed = await next();
        }
        catch
        {
            await store.AbandonAsync(id, CancellationToken.None);
            throw;
        }

        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            // Errors (validation, conflicts...) are not stored: the client may fix and retry with the same key.
            await store.AbandonAsync(id, CancellationToken.None);
            return;
        }

        if (executed.Result is ObjectResult obj && (obj.StatusCode ?? 200) is >= 200 and < 300)
        {
            var json = obj.Value is null ? "" : JsonSerializer.Serialize(obj.Value, obj.Value.GetType(), jsonOptions);
            await store.CompleteAsync(id, obj.StatusCode ?? 200, json, CancellationToken.None);
        }
        else if (executed.Result is StatusCodeResult sc && sc.StatusCode is >= 200 and < 300)
        {
            await store.CompleteAsync(id, sc.StatusCode, null, CancellationToken.None);
        }
        else
        {
            await store.AbandonAsync(id, CancellationToken.None);
        }
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static ObjectResult Problem(HttpContext http, int status, string detail)
        => new(new ProblemDetails { Status = status, Title = detail, Detail = detail, Instance = http.Request.Path })
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
}
