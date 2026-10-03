using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RepairShop.Api.Security;
using RepairShop.Application.Contracts;
using RepairShop.Application.Payments;

namespace RepairShop.Api.Controllers;

[ApiController]
[Route("api/v1/webhooks")]
[AllowAnonymous]
[EnableRateLimiting(RateLimits.Webhooks)]
public sealed class WebhooksController : ControllerBase
{
    /// <summary>
    /// Mercado Pago notifications (one URL per branch). The x-signature header is validated with the branch's
    /// webhook secret; the payment is then fetched from Mercado Pago (never trusted from the body) and applied
    /// to the order idempotently.
    /// </summary>
    [HttpPost("mercadopago/{shopId:guid}")]
    public async Task<IActionResult> MercadoPago(Guid shopId, [FromServices] PaymentLinkService links, CancellationToken ct)
    {
        string? type = Request.Query["type"].FirstOrDefault() ?? Request.Query["topic"].FirstOrDefault();
        string? dataId = Request.Query["data.id"].FirstOrDefault() ?? Request.Query["id"].FirstOrDefault();

        if (Request.ContentLength is > 0 and < 64 * 1024)
        {
            try
            {
                using var doc = await JsonDocument.ParseAsync(Request.Body, cancellationToken: ct);
                var root = doc.RootElement;
                if (type is null && root.TryGetProperty("type", out var t)) type = t.GetString();
                if (dataId is null && root.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var id))
                    dataId = id.ValueKind == JsonValueKind.Number ? id.GetRawText() : id.GetString();
            }
            catch (JsonException)
            {
                return BadRequest();
            }
        }

        var notification = new MercadoPagoNotification(type, dataId, Request.Headers["x-request-id"].FirstOrDefault(), Request.Headers["x-signature"].FirstOrDefault());
        var accepted = await links.HandleWebhookAsync(shopId, notification, ct);
        return accepted ? Ok() : Unauthorized();
    }
}
