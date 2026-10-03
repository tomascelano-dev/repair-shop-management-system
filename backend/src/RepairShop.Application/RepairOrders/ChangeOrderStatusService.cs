using Microsoft.Extensions.Logging;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Application.Notifications;
using RepairShop.Domain.Notifications;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.RepairOrders;

public sealed class ChangeOrderStatusService
{
    private const string EntityTypeRepairOrder = "repair_order";

    private readonly IRepairOrderRepository _orders;
    private readonly IRepairOrderStatusHistoryRepository _history;
    private readonly IQuoteRepository _quotes;
    private readonly IRepairOrderQaChecklistRepository _qa;
    private readonly IInventoryReservationRepository _reservations;
    private readonly IShopRepository _shops;
    private readonly IRepairOrderReadModel _readModel;
    private readonly NotificationService _notifications;
    private readonly IAuditLog _audit;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ChangeOrderStatusService> _logger;

    public ChangeOrderStatusService(
        IRepairOrderRepository orders,
        IRepairOrderStatusHistoryRepository history,
        IQuoteRepository quotes,
        IRepairOrderQaChecklistRepository qa,
        IInventoryReservationRepository reservations,
        IShopRepository shops,
        IRepairOrderReadModel readModel,
        NotificationService notifications,
        IAuditLog audit,
        IUnitOfWork uow,
        IDateTimeProvider clock,
        ILogger<ChangeOrderStatusService> logger)
    {
        _orders = orders;
        _history = history;
        _quotes = quotes;
        _qa = qa;
        _reservations = reservations;
        _shops = shops;
        _readModel = readModel;
        _notifications = notifications;
        _audit = audit;
        _uow = uow;
        _clock = clock;
        _logger = logger;
    }

    // Back-compat signature (used by older callers/tests).
    public Task<ChangeOrderStatusResponse> HandleAsync(
        Guid shopId,
        Guid orderId,
        RepairOrderStatus newStatus,
        Guid actorUserId,
        string? actorEmail,
        bool enqueueOutbox,
        NotificationChannel channel,
        CancellationToken ct)
        => HandleAsync(shopId, orderId, new ChangeOrderStatusRequest(newStatus, enqueueOutbox, channel), new Actor(actorUserId, actorEmail, null), ct);

    public async Task<ChangeOrderStatusResponse> HandleAsync(Guid shopId, Guid orderId, ChangeOrderStatusRequest req, Actor actor, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(shopId, orderId, ct);
        if (order is null) throw new NotFoundException("Orden no encontrada.");

        var newStatus = req.Status;
        var from = order.Status;
        if (from == newStatus) return new ChangeOrderStatusResponse(order.Id, from, newStatus, "", null);

        if (string.Equals(actor.Role, "Cashier", StringComparison.OrdinalIgnoreCase) && newStatus != RepairOrderStatus.Delivered)
            throw new ForbiddenException("Desde caja solo se puede registrar la entrega del equipo.");
        if (req.ForceUnpaidDelivery && !actor.IsAdmin)
            throw new ForbiddenException("Solo un administrador puede entregar un equipo con saldo pendiente.");

        var now = _clock.UtcNow;
        var shop = await _shops.GetByIdAsync(shopId, ct) ?? throw new NotFoundException("Sucursal no encontrada.");
        var approved = await _quotes.GetApprovedAsync(shopId, order.Id, ct);
        var qa = await _qa.GetByOrderAsync(shopId, order.Id, ct);
        var info = (await _readModel.GetInfoAsync(shopId, new[] { order.Id }, ct))[order.Id];
        var money = OrderMapping.Financials(order, info, shop.DefaultCurrency);

        order.MoveTo(newStatus, now, new StatusTransitionContext(
            // An agreed price (quick quote or legacy) also counts as the customer's approval.
            HasApprovedQuote: approved is not null || order.QuoteAmount is not null,
            QaPassed: qa?.Passed == true,
            BalanceDue: money.BalanceDue,
            AllowUnpaidDelivery: req.ForceUnpaidDelivery,
            DefaultWarrantyDays: approved?.WarrantyDays ?? shop.DefaultWarrantyDays,
            Reason: req.Reason));

        await _history.AddAsync(new RepairOrderStatusHistory(shopId, order.Id, from, newStatus, actor.UserId, now, req.Reason), ct);

        // A new repair round after testing/ready requires a new QA approval.
        if (newStatus == RepairOrderStatus.InProgress && from is RepairOrderStatus.Ready or RepairOrderStatus.Testing)
            qa?.Invalidate(now);

        if (newStatus == RepairOrderStatus.Cancelled)
        {
            foreach (var r in await _reservations.ListActiveByOrderAsync(shopId, order.Id, ct)) r.Release(now);
            foreach (var q in (await _quotes.ListByOrderAsync(shopId, order.Id, ct)).Where(q => q.IsOpen)) q.Supersede(now);
        }

        await _audit.AddAsync(shopId, EntityTypeRepairOrder, order.Id, "status_changed", actor, new
        {
            fromStatus = from.ToString(),
            toStatus = newStatus.ToString(),
            orderId = order.Id,
            customerId = order.CustomerId,
            deviceId = order.DeviceId,
            reason = req.Reason,
            forcedUnpaid = req.ForceUnpaidDelivery && money.BalanceDue > 0
        }, ct);

        // Persist the core transition first: notifications are side effects and must never block it.
        await _uow.SaveChangesAsync(ct);

        string body = "";
        string? waUrl = null;
        Guid? outboxId = null;
        try
        {
            var result = await _notifications.NotifyOrderAsync(shopId, order, OrderLabels.StatusTemplateKey(newStatus),
                $"order:{order.Id}:status:{newStatus}:{now:yyyyMMddHHmmss}", req.EnqueueOutbox, req.Channel, actor, ct);
            body = result.Body;
            waUrl = result.WhatsAppUrl;
            outboxId = result.OutboxItemId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Status changed, but preview/outbox failed for order {OrderId} -> {NewStatus} (shop {ShopId}).", order.Id, newStatus, shopId);
        }

        return new ChangeOrderStatusResponse(order.Id, from, newStatus, body, outboxId, waUrl);
    }
}
