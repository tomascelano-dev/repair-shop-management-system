using System.Text.Json;
using Microsoft.Extensions.Logging;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Domain.Auditing;
using RepairShop.Domain.Notifications;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.RepairOrders;

public sealed class ChangeOrderStatusService
{
    private const string EntityTypeRepairOrder = "repair_order";

    private readonly IRepairOrderRepository _orders;
    private readonly IRepairOrderStatusHistoryRepository _history;
    private readonly IAuditEventRepository _audit;
    private readonly INotificationOutboxRepository _outbox;
    private readonly ICustomerRepository _customers;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;
    private readonly RenderOrderMessageService _renderer;
	private readonly ILogger<ChangeOrderStatusService> _logger;

    public ChangeOrderStatusService(
        IRepairOrderRepository orders,
        IRepairOrderStatusHistoryRepository history,
        IAuditEventRepository audit,
        INotificationOutboxRepository outbox,
        ICustomerRepository customers,
        IUnitOfWork uow,
        IDateTimeProvider clock,
		RenderOrderMessageService renderer,
		ILogger<ChangeOrderStatusService> logger)
    {
        _orders = orders;
        _history = history;
        _audit = audit;
        _outbox = outbox;
        _customers = customers;
        _uow = uow;
        _clock = clock;
        _renderer = renderer;
		_logger = logger;
    }

    public async Task<ChangeOrderStatusResponse> HandleAsync(
        Guid shopId,
        Guid orderId,
        RepairOrderStatus newStatus,
        Guid actorUserId,
        string? actorEmail,
        bool enqueueOutbox,
        NotificationChannel channel,
        CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(shopId, orderId, ct);
        if (order is null) throw new NotFoundException("Order not found.");

        var from = order.Status;

        if (from == newStatus)
        {
            return new ChangeOrderStatusResponse(order.Id, from, newStatus, "", null);
        }

        order.MoveTo(newStatus, _clock.UtcNow);

        var h = new RepairOrderStatusHistory(shopId, order.Id, from, newStatus, actorUserId, _clock.UtcNow);
        await _history.AddAsync(h, ct);

        // Audit
        var auditData = JsonSerializer.Serialize(new
        {
            fromStatus = from.ToString(),
            toStatus = newStatus.ToString(),
            orderId = order.Id,
            customerId = order.CustomerId,
            deviceId = order.DeviceId
        });

        await _audit.AddAsync(new AuditEvent(
            shopId: shopId,
            entityType: EntityTypeRepairOrder,
            entityId: order.Id,
            action: "status_changed",
            actorUserId: actorUserId == Guid.Empty ? null : actorUserId,
            actorEmail: actorEmail,
            dataJson: auditData,
            nowUtc: _clock.UtcNow
        ), ct);

        // Persist the core status transition first.
        // Notifications / message previews are side-effects and must NOT be able to block the transition.
        await _uow.SaveChangesAsync(ct);

        string previewBody = "";
        Guid? outboxId = null;

        try
        {
            // Suggested message (template: order.status.<status>)
            var templateKey = $"order.status.{newStatus.ToString().ToLowerInvariant()}";
            var preview = await _renderer.RenderAsync(shopId, order.Id, templateKey, allowFallback: true, ct);
            previewBody = preview.Body;

            if (enqueueOutbox)
            {
                var customer = await _customers.GetByIdAsync(shopId, order.CustomerId, ct);

                var recipient = customer?.Phone ?? "";
                if (!string.IsNullOrWhiteSpace(recipient))
                {
                    var correlationKey = $"order:{order.Id}:status:{newStatus}";

                    var item = new NotificationOutboxItem(
                        shopId: shopId,
                        channel: channel,
                        recipient: recipient,
                        title: preview.Title,
                        body: preview.Body,
                        status: OutboxStatus.Pending,
                        correlationKey: correlationKey,
                        relatedEntityType: EntityTypeRepairOrder,
                        relatedEntityId: order.Id,
                        nowUtc: _clock.UtcNow
                    );

                    await _outbox.AddAsync(item, ct);
                    await _uow.SaveChangesAsync(ct);
                    outboxId = item.Id;
                }
            }
        }
		catch (Exception ex)
        {
            // Best-effort: keep status transition successful even if preview/outbox fails.
			_logger.LogWarning(ex,
				"Status changed, but preview/outbox failed for order {OrderId} -> {NewStatus} (shop {ShopId}).",
				order.Id,
				newStatus,
				shopId);
        }

        return new ChangeOrderStatusResponse(order.Id, from, newStatus, previewBody, outboxId);
    }
}
