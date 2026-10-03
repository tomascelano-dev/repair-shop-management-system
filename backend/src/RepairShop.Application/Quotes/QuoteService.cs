using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Application.Notifications;
using RepairShop.Application.RepairOrders;
using RepairShop.Domain.Common;
using RepairShop.Domain.Inventory;
using RepairShop.Domain.Quotes;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.Quotes;

/// <summary>
/// Quotes (presupuestos): draft -> sent -> approved/rejected/expired. Approving a quote sets the order's
/// agreed price and reserves the inventory parts it includes.
/// </summary>
public sealed class QuoteService
{
    private const string EntityType = RepairOrderService.EntityType;

    private readonly IQuoteRepository _quotes;
    private readonly IRepairOrderRepository _orders;
    private readonly IRepairOrderPaymentRepository _payments;
    private readonly IInventoryItemRepository _items;
    private readonly IInventoryReservationRepository _reservations;
    private readonly IShopRepository _shops;
    private readonly NotificationService _notifications;
    private readonly IAuditLog _audit;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public QuoteService(
        IQuoteRepository quotes,
        IRepairOrderRepository orders,
        IRepairOrderPaymentRepository payments,
        IInventoryItemRepository items,
        IInventoryReservationRepository reservations,
        IShopRepository shops,
        NotificationService notifications,
        IAuditLog audit,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _quotes = quotes;
        _orders = orders;
        _payments = payments;
        _items = items;
        _reservations = reservations;
        _shops = shops;
        _notifications = notifications;
        _audit = audit;
        _uow = uow;
        _clock = clock;
    }

    public async Task<List<QuoteResponse>> ListAsync(Guid shopId, Guid orderId, CancellationToken ct)
    {
        await RequireOrderAsync(shopId, orderId, ct);
        return (await _quotes.ListByOrderAsync(shopId, orderId, ct)).Select(ToResponse).ToList();
    }

    public async Task<QuoteResponse> GetAsync(Guid shopId, Guid orderId, Guid quoteId, CancellationToken ct)
        => ToResponse(await RequireQuoteAsync(shopId, orderId, quoteId, ct));

    /// <summary>Creates a new draft version. Open (draft/sent) versions are superseded.</summary>
    public async Task<QuoteResponse> CreateAsync(Guid shopId, Guid orderId, SaveQuoteRequest req, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var order = await RequireOrderAsync(shopId, orderId, ct);
        if (order.IsFinal) throw new DomainException("La orden está finalizada.");
        if (order.IsWarrantyClaim && req.Items.Sum(i => i.Quantity * i.UnitPrice) > 0)
            throw new DomainException("Los reingresos por garantía no se cobran: el presupuesto debe ser de importe 0.");

        var currency = Money.NormalizeCurrency(req.Currency);
        await EnsureCurrencyAsync(shopId, order, currency, ct);
        var items = await BuildItemsAsync(shopId, req.Items, ct);

        var existing = await _quotes.ListByOrderAsync(shopId, orderId, ct);
        foreach (var open in existing.Where(q => q.IsOpen)) open.Supersede(now);

        var quote = new Quote(shopId, orderId, existing.Select(q => q.Version).DefaultIfEmpty(0).Max() + 1, currency, actor.UserId, now);
        quote.Edit(currency, items, req.DiscountAmount, req.WarrantyDays, req.Notes, now);
        await _quotes.AddAsync(quote, ct);
        await _audit.AddAsync(shopId, EntityType, orderId, "quote_created", actor, new { quote.Version, quote.Total, quote.Currency }, ct);
        await _uow.SaveChangesAsync(ct);
        return ToResponse(quote);
    }

    public async Task<QuoteResponse> UpdateAsync(Guid shopId, Guid orderId, Guid quoteId, SaveQuoteRequest req, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var order = await RequireOrderAsync(shopId, orderId, ct);
        var quote = await RequireQuoteAsync(shopId, orderId, quoteId, ct);
        var currency = Money.NormalizeCurrency(req.Currency);
        await EnsureCurrencyAsync(shopId, order, currency, ct);

        quote.Edit(currency, await BuildItemsAsync(shopId, req.Items, ct), req.DiscountAmount, req.WarrantyDays, req.Notes, now);
        await _audit.AddAsync(shopId, EntityType, orderId, "quote_updated", actor, new { quote.Version, quote.Total }, ct);
        await _uow.SaveChangesAsync(ct);
        return ToResponse(quote);
    }

    public async Task<QuoteActionResponse> SendAsync(Guid shopId, Guid orderId, Guid quoteId, SendQuoteRequest req, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var order = await RequireOrderAsync(shopId, orderId, ct);
        var shop = await _shops.GetByIdAsync(shopId, ct) ?? throw new NotFoundException("Sucursal no encontrada.");
        var quote = await RequireQuoteAsync(shopId, orderId, quoteId, ct);

        var days = req.ValidDays ?? shop.QuoteValidityDays;
        if (days is < 1 or > 365) throw new DomainException("La validez debe estar entre 1 y 365 días.");
        quote.Send(now.AddDays(days), now);
        await _audit.AddAsync(shopId, EntityType, orderId, "quote_sent", actor, new { quote.Version, quote.Total, validDays = days }, ct);
        await _uow.SaveChangesAsync(ct);

        var msg = await _notifications.NotifyOrderAsync(shopId, order, TemplateKeys.QuoteSent, $"order:{orderId}:quote:{quote.Id}:sent",
            req.EnqueueOutbox, req.Channel, actor, ct);
        return new QuoteActionResponse(ToResponse(quote), msg.Body, msg.WhatsAppUrl, msg.OutboxItemId, Array.Empty<string>());
    }

    public Task<QuoteActionResponse> ApproveAsync(Guid shopId, Guid orderId, Guid quoteId, DecideQuoteRequest req, Actor actor, CancellationToken ct)
        => DecideAsync(shopId, orderId, quoteId, approve: true, QuoteDecisionSource.Staff, req.Note, null, actor, ct);

    public Task<QuoteActionResponse> RejectAsync(Guid shopId, Guid orderId, Guid quoteId, DecideQuoteRequest req, Actor actor, CancellationToken ct)
        => DecideAsync(shopId, orderId, quoteId, approve: false, QuoteDecisionSource.Staff, req.Note, null, actor, ct);

    /// <summary>Decision taken by the customer from the public tracking page.</summary>
    public Task<QuoteActionResponse> DecideFromPortalAsync(Guid shopId, Guid orderId, Guid quoteId, bool approve, string? note, string? ip, CancellationToken ct)
        => DecideAsync(shopId, orderId, quoteId, approve, QuoteDecisionSource.CustomerPortal, note, ip, Actor.System, ct);

    /// <summary>Background job: marks sent quotes past their validity as expired.</summary>
    public async Task<int> ExpireDueAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var due = await _quotes.ListExpiredSentAsync(now, 200, ct);
        var count = 0;
        foreach (var q in due)
        {
            if (!q.ExpireIfDue(now)) continue;
            await _audit.AddAsync(q.ShopId, EntityType, q.RepairOrderId, "quote_expired", Actor.System, new { q.Version }, ct);
            count++;
        }

        if (count > 0) await _uow.SaveChangesAsync(ct);
        return count;
    }

    private async Task<QuoteActionResponse> DecideAsync(Guid shopId, Guid orderId, Guid quoteId, bool approve, QuoteDecisionSource source,
        string? note, string? ip, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var order = await RequireOrderAsync(shopId, orderId, ct);
        if (order.IsFinal) throw new DomainException("La orden está finalizada.");
        var quote = await RequireQuoteAsync(shopId, orderId, quoteId, ct);
        var warnings = new List<string>();
        Guid? userId = actor.IsSystem ? null : actor.UserId;

        if (approve)
        {
            await EnsureCurrencyAsync(shopId, order, quote.Currency, ct);
            quote.Approve(source, userId, note, ip, now);

            foreach (var other in (await _quotes.ListByOrderAsync(shopId, orderId, ct)).Where(q => q.Id != quote.Id))
                other.Supersede(now);

            // Agreed price = approved quote total. Customer approvals have no staff user: record the quote creator.
            order.SetQuote(quote.Total, quote.Currency, userId ?? quote.CreatedByUserId, now);
            if (quote.WarrantyDays is not null) order.SetWarrantyDays(quote.WarrantyDays, now);

            warnings.AddRange(await ReservePartsAsync(shopId, order, quote, now, ct));
        }
        else
        {
            quote.Reject(source, userId, note, ip, now);
        }

        await _audit.AddAsync(shopId, EntityType, orderId, approve ? "quote_approved" : "quote_rejected", actor,
            new { quote.Version, quote.Total, source = source.ToString(), note }, ct);
        await _uow.SaveChangesAsync(ct);

        var msg = await _notifications.NotifyOrderAsync(shopId, order, approve ? TemplateKeys.QuoteApproved : TemplateKeys.QuoteRejected,
            $"order:{orderId}:quote:{quote.Id}:{(approve ? "approved" : "rejected")}", true, null, actor, ct);
        return new QuoteActionResponse(ToResponse(quote), msg.Body, msg.WhatsAppUrl, msg.OutboxItemId, warnings);
    }

    /// <summary>Releases previous reservations of the order and reserves the parts of the approved quote.</summary>
    private async Task<List<string>> ReservePartsAsync(Guid shopId, RepairOrder order, Quote quote, DateTime now, CancellationToken ct)
    {
        var warnings = new List<string>();
        foreach (var r in await _reservations.ListActiveByOrderAsync(shopId, order.Id, ct)) r.Release(now);

        var partLines = quote.Items.Where(i => i.InventoryItemId is not null).GroupBy(i => i.InventoryItemId!.Value)
            .Select(g => (ItemId: g.Key, Qty: (int)g.Sum(i => i.Quantity))).ToList();
        if (partLines.Count == 0) return warnings;

        var items = (await _items.GetByIdsAsync(shopId, partLines.Select(p => p.ItemId).ToList(), ct)).ToDictionary(i => i.Id);
        var reserved = await _reservations.SumActiveByItemsAsync(shopId, partLines.Select(p => p.ItemId).ToList(), order.Id, ct);

        foreach (var (itemId, qty) in partLines)
        {
            if (!items.TryGetValue(itemId, out var item) || !item.TrackStock) continue;
            await _reservations.AddAsync(new InventoryReservation(shopId, itemId, order.Id, quote.Id, qty, now), ct);

            var available = item.QuantityOnHand - reserved.GetValueOrDefault(itemId);
            if (available < qty)
                warnings.Add($"Stock insuficiente de \"{item.Name}\": disponibles {Math.Max(0, available)}, necesarios {qty}. Pasá la orden a \"Esperando repuesto\" y generá la compra.");
        }

        return warnings;
    }

    private async Task<List<QuoteItemInput>> BuildItemsAsync(Guid shopId, IReadOnlyList<QuoteItemRequest> items, CancellationToken ct)
    {
        if (items is null || items.Count == 0) throw new DomainException("El presupuesto debe tener al menos un ítem.");
        var ids = items.Where(i => i.InventoryItemId is not null).Select(i => i.InventoryItemId!.Value).Distinct().ToList();
        if (ids.Count > 0)
        {
            var found = (await _items.GetByIdsAsync(shopId, ids, ct)).Select(i => i.Id).ToHashSet();
            if (ids.Any(id => !found.Contains(id))) throw new NotFoundException("Uno de los repuestos del presupuesto no existe en el inventario.");
        }

        return items.Select(i => new QuoteItemInput(i.Kind, i.Description, i.Quantity, i.UnitPrice, i.InventoryItemId, i.WarrantyDays)).ToList();
    }

    private async Task EnsureCurrencyAsync(Guid shopId, RepairOrder order, string currency, CancellationToken ct)
    {
        var payments = await _payments.ListByOrderAsync(shopId, order.Id, ct);
        var paid = payments.FirstOrDefault();
        if (paid is not null && paid.Currency != currency)
            throw new DomainException($"La orden tiene pagos en {paid.Currency}: el presupuesto debe estar en la misma moneda.");
    }

    private async Task<RepairOrder> RequireOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => await _orders.GetByIdAsync(shopId, orderId, ct) ?? throw new NotFoundException("Orden no encontrada.");

    private async Task<Quote> RequireQuoteAsync(Guid shopId, Guid orderId, Guid quoteId, CancellationToken ct)
    {
        var quote = await _quotes.GetByIdAsync(shopId, quoteId, ct);
        if (quote is null || quote.RepairOrderId != orderId) throw new NotFoundException("Presupuesto no encontrado.");
        return quote;
    }

    public static QuoteResponse ToResponse(Quote q)
        => new(q.Id, q.RepairOrderId, q.Version, q.Status.ToString(), OrderLabels.Quote(q.Status), q.Currency,
            q.Items.OrderBy(i => i.Position).Select(i => new QuoteItemResponse(i.Id, i.Position, i.Kind.ToString(), i.Description, i.Quantity, i.UnitPrice, i.LineTotal, i.InventoryItemId, i.WarrantyDays)).ToList(),
            q.Subtotal, q.DiscountAmount, q.Total, q.ValidUntilUtc, q.WarrantyDays, q.Notes, q.CreatedAtUtc, q.SentAtUtc, q.DecidedAtUtc,
            q.DecisionSource?.ToString(), q.DecisionNote, q.DecidedByUserId);
}
