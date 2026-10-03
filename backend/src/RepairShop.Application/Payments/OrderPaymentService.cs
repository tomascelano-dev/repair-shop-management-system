using RepairShop.Application.Abstractions;
using RepairShop.Application.Cash;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Application.RepairOrders;
using RepairShop.Domain.Cash;
using RepairShop.Domain.Common;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.Payments;

/// <summary>
/// Payments of repair orders: deposits ("señas"), partial and final payments, and refunds.
/// Validates currency and balance, links cash movements to the open cash session and touches the order
/// row so concurrent payments can't over-charge (optimistic concurrency).
/// </summary>
public sealed class OrderPaymentService
{
    private const string EntityType = RepairOrderService.EntityType;

    private readonly IRepairOrderRepository _orders;
    private readonly IRepairOrderPaymentRepository _payments;
    private readonly IRepairOrderReadModel _readModel;
    private readonly IShopRepository _shops;
    private readonly CashRegisterService _cash;
    private readonly IAuditLog _audit;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public OrderPaymentService(
        IRepairOrderRepository orders,
        IRepairOrderPaymentRepository payments,
        IRepairOrderReadModel readModel,
        IShopRepository shops,
        CashRegisterService cash,
        IAuditLog audit,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _orders = orders;
        _payments = payments;
        _readModel = readModel;
        _shops = shops;
        _cash = cash;
        _audit = audit;
        _uow = uow;
        _clock = clock;
    }

    public async Task<List<RepairOrderPaymentResponse>> ListAsync(Guid shopId, Guid orderId, CancellationToken ct)
    {
        await RequireOrderAsync(shopId, orderId, ct);
        var list = await _payments.ListByOrderAsync(shopId, orderId, ct);
        var refunds = list.Where(p => p.Type == PaymentType.Refund && p.RefundOfPaymentId is not null)
            .GroupBy(p => p.RefundOfPaymentId!.Value).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        return list.Select(p => ToResponse(p, refunds.GetValueOrDefault(p.Id))).ToList();
    }

    public async Task<OrderFinancialsResponse> GetFinancialsAsync(Guid shopId, Guid orderId, CancellationToken ct)
    {
        var order = await RequireOrderAsync(shopId, orderId, ct);
        return await FinancialsAsync(shopId, order, ct);
    }

    public async Task<RepairOrderPaymentResponse> AddAsync(Guid shopId, Guid orderId, CreateRepairOrderPaymentRequest req, Actor actor, CancellationToken ct,
        string? externalPaymentId = null)
    {
        var now = _clock.UtcNow;
        var order = await RequireOrderAsync(shopId, orderId, ct);
        if (order.Status == RepairOrderStatus.Cancelled) throw new DomainException("No se pueden registrar pagos en una orden cancelada.");

        var currency = Money.NormalizeCurrency(req.Currency);
        var money = await FinancialsAsync(shopId, order, ct);
        var hasCurrency = money.HasAgreedPrice || money.Paid != 0 || money.ExtraCharges > 0;
        if (hasCurrency && currency != money.Currency)
            throw new DomainException($"La orden se cobra en {money.Currency}: registrá el pago en esa moneda.");

        var amount = Money.Round(req.Amount);
        if (amount <= 0) throw new DomainException("El importe debe ser mayor a 0.");
        if (money.HasAgreedPrice && amount > money.BalanceDue)
            throw new DomainException(money.BalanceDue <= 0
                ? "La orden ya está totalmente paga."
                : $"El pago supera el saldo pendiente ({money.BalanceDue:0.00} {money.Currency}).");

        if (externalPaymentId is not null && await _payments.ExistsExternalAsync(shopId, externalPaymentId, ct))
            throw new ConflictException("Ese pago ya fue registrado.");

        var session = await _cash.GetSessionForPaymentAsync(shopId, req.Method, forceOptional: externalPaymentId is not null, ct);

        var payment = new RepairOrderPayment(shopId, orderId, amount, currency, req.Method, req.Reference, actor.UserId, now);
        if (!money.HasAgreedPrice) payment.MarkAsDeposit();
        payment.LinkCashSession(session?.Id);
        if (externalPaymentId is not null) payment.SetExternalPaymentId(externalPaymentId);

        await _payments.AddAsync(payment, ct);
        order.TouchFinancials(now);
        await _cash.RecordAsync(session, shopId, CashMovementType.OrderPayment, req.Method, amount, currency,
            $"{(payment.IsDeposit ? "Seña" : "Pago")} orden {order.Code}", EntityType, order.Id, actor, ct);

        await _audit.AddAsync(shopId, EntityType, order.Id, "payment_added", actor, new
        {
            orderId = order.Id,
            amount,
            currency,
            method = req.Method.ToString(),
            reference = payment.Reference,
            deposit = payment.IsDeposit,
            external = externalPaymentId
        }, ct);

        await _uow.SaveChangesAsync(ct);
        return ToResponse(payment, 0);
    }

    public async Task<RepairOrderPaymentResponse> RefundAsync(Guid shopId, Guid orderId, Guid paymentId, RefundOrderPaymentRequest req, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var order = await RequireOrderAsync(shopId, orderId, ct);
        var original = await _payments.GetByIdAsync(shopId, paymentId, ct);
        if (original is null || original.RepairOrderId != orderId) throw new NotFoundException("Pago no encontrado.");
        if (original.Type != PaymentType.Payment) throw new DomainException("Solo se pueden devolver pagos.");

        var alreadyRefunded = await _payments.SumRefundsAsync(shopId, paymentId, ct);
        var amount = Money.Round(req.Amount);
        if (amount <= 0) throw new DomainException("El importe a devolver debe ser mayor a 0.");
        if (amount > original.Amount - alreadyRefunded)
            throw new DomainException($"Solo quedan {original.Amount - alreadyRefunded:0.00} {original.Currency} por devolver de ese pago.");

        var session = await _cash.GetSessionForPaymentAsync(shopId, req.Method, forceOptional: false, ct);
        var refund = RepairOrderPayment.CreateRefund(original, amount, req.Method, req.Reason, actor.UserId, session?.Id, now);
        await _payments.AddAsync(refund, ct);
        order.TouchFinancials(now);
        await _cash.RecordAsync(session, shopId, CashMovementType.Refund, req.Method, amount, original.Currency,
            $"Devolución orden {order.Code}", EntityType, order.Id, actor, ct);

        await _audit.AddAsync(shopId, EntityType, order.Id, "payment_refunded", actor,
            new { paymentId, amount, currency = original.Currency, method = req.Method.ToString(), req.Reason }, ct);
        await _uow.SaveChangesAsync(ct);
        return ToResponse(refund, 0);
    }

    private async Task<OrderFinancialsResponse> FinancialsAsync(Guid shopId, RepairOrder order, CancellationToken ct)
    {
        var shop = await _shops.GetByIdAsync(shopId, ct);
        var info = (await _readModel.GetInfoAsync(shopId, new[] { order.Id }, ct))[order.Id];
        return OrderMapping.Financials(order, info, shop?.DefaultCurrency ?? "ARS");
    }

    private async Task<RepairOrder> RequireOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => await _orders.GetByIdAsync(shopId, orderId, ct) ?? throw new NotFoundException("Orden no encontrada.");

    public static RepairOrderPaymentResponse ToResponse(RepairOrderPayment p, decimal refunded)
        => new(p.Id, p.RepairOrderId, p.Amount, p.Currency, p.Method, p.Reference, p.CreatedByUserId, p.CreatedAtUtc,
            p.Type.ToString(), p.IsDeposit, p.RefundOfPaymentId, refunded);
}
