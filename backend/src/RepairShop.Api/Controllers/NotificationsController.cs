using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Contracts;
using RepairShop.Application.Notifications;
using RepairShop.Domain.Notifications;

namespace RepairShop.Api.Controllers;

/// <summary>Outgoing message queue (WhatsApp/SMS/email): status, retries and cancellation.</summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize(Policy = Policies.OrdersWork)]
public sealed class NotificationsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NotificationResponse>>>> List(
        [FromServices] INotificationOutboxRepository outbox, [FromQuery] OutboxStatus? status, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var (items, total) = await outbox.SearchAsync(CurrentUser.GetShopId(User), status, null, null, Math.Max(0, skip), Math.Clamp(take, 1, 200), ct);
        Response.Headers[HttpExtensions.TotalCountHeader] = total.ToString();
        return Ok(Envelope.Ok<IReadOnlyList<NotificationResponse>>(items.Select(NotificationService.ToResponse).ToList()));
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<ActionResult<ApiResponse<NotificationResponse>>> Retry([FromServices] NotificationService notifications, Guid id, CancellationToken ct)
        => Ok(Envelope.Ok(await notifications.RetryAsync(CurrentUser.GetShopId(User), id, ct)));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<NotificationResponse>>> Cancel([FromServices] NotificationService notifications, Guid id, CancellationToken ct)
        => Ok(Envelope.Ok(await notifications.CancelAsync(CurrentUser.GetShopId(User), id, ct)));
}
