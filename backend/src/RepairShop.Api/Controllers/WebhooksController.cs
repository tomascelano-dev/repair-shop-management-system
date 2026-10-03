using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RepairShop.Api.Common;
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
        var (type, dataId) = await WebhookPayload.ReadMercadoPagoAsync(Request, ct);

        var notification = new MercadoPagoNotification(type, dataId, Request.Headers["x-request-id"].FirstOrDefault(), Request.Headers["x-signature"].FirstOrDefault());
        var accepted = await links.HandleWebhookAsync(shopId, notification, ct);
        return accepted ? Ok() : Unauthorized();
    }
}
