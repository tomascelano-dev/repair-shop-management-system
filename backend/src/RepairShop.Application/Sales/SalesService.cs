using RepairShop.Application.Abstractions;
using RepairShop.Application.Cash;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Domain.Cash;
using RepairShop.Domain.Common;
using RepairShop.Domain.Inventory;
using RepairShop.Domain.Sales;

namespace RepairShop.Application.Sales;

/// <summary>
/// Point of sale: sells accessories/parts/services at the counter with discounts and split payments,
/// moves stock, records cash movements and supports partial refunds and voids.
/// </summary>
public sealed class SalesService
{
    private const string EntityType = "sale";

    private readonly ISaleRepository _sales;
    private readonly IInventoryItemRepository _items;
    private readonly IInventoryAdjustmentRepository _adjustments;
    private readonly IInventoryReservationRepository _reservations;
    private readonly ICustomerRepository _customers;
    private readonly IUserRepository _users;
    private readonly IShopRepository _shops;
    private readonly ICounterService _counters;
    private readonly CashRegisterService _cash;
    private readonly IAuditLog _audit;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public SalesService(
        ISaleRepository sales,
        IInventoryItemRepository items,
        IInventoryAdjustmentRepository adjustments,
        IInventoryReservationRepository reservations,
        ICustomerRepository customers,
        IUserRepository users,
        IShopRepository shops,
        ICounterService counters,
        CashRegisterService cash,
        IAuditLog audit,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _sales = sales;
        _items = items;
        _adjustments = adjustments;
        _reservations = reservations;
        _customers = customers;
        _users = users;
        _shops = shops;
        _counters = counters;
        _cash = cash;
        _audit = audit;
        _uow = uow;
        _clock = clock;
    }

    /// <summary>Products for the POS grid/search (sellable items with price and free stock).</summary>
    public async Task<List<PosCatalogItem>> CatalogAsync(Guid shopId, string? q, int take, CancellationToken ct)
    {
        var (items, _) = await _items.SearchAsync(shopId, new InventorySearchOptions(Q: q, OnlySellable: true, Take: Math.Clamp(take, 1, 100), SortBy: "name"), ct);
        var reserved = items.Count == 0 ? new Dictionary<Guid, int>() : await _reservations.SumActiveByItemsAsync(shopId, items.Select(i => i.Id).ToList(), null, ct);
        return items.Select(i => new PosCatalogItem(i.Id, i.Sku, i.Barcode, i.Name, i.Category, i.SalePrice, i.SalePriceCurrency, i.TrackStock,
            i.TrackStock ? i.QuantityOnHand - reserved.GetValueOrDefault(i.Id) : 9999, i.WarrantyDays)).ToList();
    }

    public async Task<PagedResult<SaleResponse>> SearchAsync(Guid shopId, SaleSearchOptions options, CancellationToken ct)
    {
        var (items, total) = await _sales.SearchAsync(shopId, options, ct);
        return new PagedResult<SaleResponse>(await ToResponsesAsync(shopId, items, ct), total);
    }

    public async Task<SaleResponse> GetAsync(Guid shopId, Guid id, CancellationToken ct)
        => (await ToResponsesAsync(shopId, new[] { await RequireAsync(shopId, id, ct) }, ct)).Single();

    public async Task<SaleResponse> CreateAsync(Guid shopId, CreateSaleRequest req, Actor actor, CancellationToken ct)
    {
        if (req.Lines is null || req.Lines.Count == 0) throw new DomainException("Agregá al menos un producto.");
        if (req.Payments is null || req.Payments.Count == 0) throw new DomainException("Registrá al menos un pago.");

        var shop = await _shops.GetByIdAsync(shopId, ct) ?? throw new NotFoundException("Sucursal no encontrada.");
        if (req.CustomerId is not null && await _customers.GetByIdAsync(shopId, req.CustomerId.Value, ct) is null)
            throw new NotFoundException("Cliente no encontrado.");

        var sale = await _uow.RetryOnConflictAsync(c => _uow.InTransactionAsync(async tx =>
        {
            var now = _clock.UtcNow;
            var currency = Money.NormalizeCurrency(string.IsNullOrWhiteSpace(req.Currency) ? shop.DefaultCurrency : req.Currency);
            var session = await _cash.GetSessionForPaymentAsync(shopId, req.Payments[0].Method, forceOptional: false, tx);

            var ids = req.Lines.Where(l => l.InventoryItemId is not null).Select(l => l.InventoryItemId!.Value).Distinct().ToList();
            var items = (await _items.GetByIdsAsync(shopId, ids, tx)).ToDictionary(i => i.Id);
            var reserved = ids.Count == 0 ? new Dictionary<Guid, int>() : await _reservations.SumActiveByItemsAsync(shopId, ids, null, tx);

            var lineInputs = new List<SaleLineInput>();
            foreach (var l in req.Lines)
            {
                if (l.Quantity <= 0) throw new DomainException("Las cantidades deben ser mayores a 0.");
                if (l.InventoryItemId is null)
                {
                    if (l.UnitPrice is null) throw new DomainException("Los ítems libres necesitan precio.");
                    var gross = Money.Round(l.UnitPrice.Value * l.Quantity);
                    lineInputs.Add(new SaleLineInput(null, "", l.Description ?? "", l.Quantity, l.UnitPrice.Value, LineDiscount(l, gross), null, false, null));
                    continue;
                }

                if (!items.TryGetValue(l.InventoryItemId.Value, out var item)) throw new NotFoundException("Uno de los productos no existe.");
                if (!item.IsActive) throw new DomainException($"\"{item.Name}\" está inactivo.");
                var price = l.UnitPrice ?? item.SalePrice ?? throw new DomainException($"\"{item.Name}\" no tiene precio de venta.");
                if (l.UnitPrice is null && item.SalePriceCurrency is not null && item.SalePriceCurrency != currency)
                    throw new DomainException($"\"{item.Name}\" tiene precio en {item.SalePriceCurrency}: indicá el precio en {currency}.");

                var lineGross = Money.Round(price * l.Quantity);
                lineInputs.Add(new SaleLineInput(item.Id, item.Sku, string.IsNullOrWhiteSpace(l.Description) ? item.Name : l.Description!, l.Quantity, price,
                    LineDiscount(l, lineGross), item.UnitCost, item.TrackStock, item.WarrantyDays));
            }

            // Stock check against free (not reserved) stock, aggregated per item.
            foreach (var g in lineInputs.Where(x => x.InventoryItemId is not null && x.TrackStock).GroupBy(x => x.InventoryItemId!.Value))
            {
                var item = items[g.Key];
                var free = item.QuantityOnHand - reserved.GetValueOrDefault(item.Id);
                var qty = g.Sum(x => x.Quantity);
                if (qty > free) throw new DomainException($"Stock insuficiente de \"{item.Name}\": disponible {Math.Max(0, free)}.");
            }

            var subtotal = lineInputs.Sum(x => Money.Round(x.UnitPrice * x.Quantity) - x.DiscountAmount);
            var globalDiscount = req.DiscountPercent is > 0 ? Money.Round(subtotal * req.DiscountPercent.Value / 100m) : req.DiscountAmount;
            if (req.DiscountPercent is < 0 or > 100) throw new DomainException("El descuento debe estar entre 0 y 100%.");

            var number = await _counters.NextAsync(shopId, CounterKeys.Sale, tx);
            var s = new Sale(shopId, number, req.CustomerId, currency, lineInputs, globalDiscount,
                req.Payments.Select(p => new SalePaymentInput(p.Method, p.Amount, p.Reference)).ToList(), session?.Id, req.Notes, actor.UserId, now);

            foreach (var line in s.Lines.Where(x => x.TrackStock))
            {
                items[line.InventoryItemId!.Value].ApplyDelta(-line.Quantity, now);
                await _adjustments.AddAsync(new InventoryAdjustment(shopId, line.InventoryItemId.Value, InventoryAdjustmentType.Sale, -line.Quantity, $"Venta {s.Code}", null, actor.UserId, now)
                    .WithReference(EntityType, s.Id), tx);
            }

            foreach (var (method, amount) in s.NetPaymentsByMethod())
                await _cash.RecordAsync(session, shopId, CashMovementType.Sale, method, amount, currency, $"Venta {s.Code}", EntityType, s.Id, actor, tx);

            await _sales.AddAsync(s, tx);
            await _audit.AddAsync(shopId, EntityType, s.Id, "sale_created", actor, new { s.Number, s.Total, s.Currency, lines = s.Lines.Count }, tx);
            await _uow.SaveChangesAsync(tx);
            return s;
        }, c), ct);

        return await GetAsync(shopId, sale.Id, ct);
    }

    public async Task<SaleResponse> RefundAsync(Guid shopId, Guid id, RefundSaleRequest req, Actor actor, CancellationToken ct)
    {
        await _uow.RetryOnConflictAsync(async c =>
        {
            var now = _clock.UtcNow;
            var sale = await RequireAsync(shopId, id, c);
            var session = await _cash.GetSessionForPaymentAsync(shopId, req.Method, forceOptional: false, c);
            var refund = sale.Refund(req.Lines.GroupBy(l => l.SaleLineId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity)),
                req.Method, req.Restock, req.Reason, actor.UserId, session?.Id, now);
            await AfterRefundAsync(shopId, sale, refund, session, actor, c);
            await _audit.AddAsync(shopId, EntityType, sale.Id, "sale_refunded", actor, new { refund.Amount, method = req.Method.ToString(), req.Restock, req.Reason }, c);
            await _uow.SaveChangesAsync(c);
            return true;
        }, ct);

        return await GetAsync(shopId, id, ct);
    }

    public async Task<SaleResponse> VoidAsync(Guid shopId, Guid id, VoidSaleRequest req, Actor actor, CancellationToken ct)
    {
        await _uow.RetryOnConflictAsync(async c =>
        {
            var now = _clock.UtcNow;
            var sale = await RequireAsync(shopId, id, c);
            var session = await _cash.GetSessionForPaymentAsync(shopId, req.Method, forceOptional: false, c);
            var refund = sale.Void(req.Reason, req.Method, actor.UserId, session?.Id, now);
            await AfterRefundAsync(shopId, sale, refund, session, actor, c);
            await _audit.AddAsync(shopId, EntityType, sale.Id, "sale_voided", actor, new { req.Reason, refund.Amount }, c);
            await _uow.SaveChangesAsync(c);
            return true;
        }, ct);

        return await GetAsync(shopId, id, ct);
    }

    private async Task AfterRefundAsync(Guid shopId, Sale sale, SaleRefund refund, CashRegisterSession? session, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        if (refund.Restocked)
        {
            var lines = refund.Lines.Select(rl => (Line: sale.Lines.First(l => l.Id == rl.SaleLineId), rl.Quantity)).Where(x => x.Line.TrackStock).ToList();
            var items = (await _items.GetByIdsAsync(shopId, lines.Select(x => x.Line.InventoryItemId!.Value).Distinct().ToList(), ct)).ToDictionary(i => i.Id);
            foreach (var (line, qty) in lines)
            {
                if (!items.TryGetValue(line.InventoryItemId!.Value, out var item)) continue;
                item.ApplyDelta(qty, now);
                await _adjustments.AddAsync(new InventoryAdjustment(shopId, item.Id, InventoryAdjustmentType.Return, qty, $"Devolución {sale.Code}", null, actor.UserId, now)
                    .WithReference(EntityType, sale.Id), ct);
            }
        }

        await _cash.RecordAsync(session, shopId, CashMovementType.Refund, refund.Method, refund.Amount, sale.Currency, $"Devolución {sale.Code}", EntityType, sale.Id, actor, ct);
    }

    private static decimal LineDiscount(SaleLineRequest l, decimal gross)
    {
        if (l.DiscountPercent is < 0 or > 100) throw new DomainException("El descuento debe estar entre 0 y 100%.");
        return l.DiscountPercent is > 0 ? Money.Round(gross * l.DiscountPercent.Value / 100m) : Money.Round(l.DiscountAmount);
    }

    private async Task<Sale> RequireAsync(Guid shopId, Guid id, CancellationToken ct)
        => await _sales.GetByIdAsync(shopId, id, ct) ?? throw new NotFoundException("Venta no encontrada.");

    private async Task<List<SaleResponse>> ToResponsesAsync(Guid shopId, IReadOnlyCollection<Sale> sales, CancellationToken ct)
    {
        var customers = (await _customers.GetByIdsAsync(shopId, sales.Where(s => s.CustomerId is not null).Select(s => s.CustomerId!.Value).Distinct().ToList(), ct))
            .ToDictionary(c => c.Id, c => c.FullName);
        var users = (await _users.GetByIdsAsync(sales.Select(s => s.CreatedByUserId).Distinct().ToList(), ct)).ToDictionary(u => u.Id, u => u.DisplayName);
        return sales.Select(s => ToResponse(s, s.CustomerId is null ? null : customers.GetValueOrDefault(s.CustomerId.Value), users.GetValueOrDefault(s.CreatedByUserId))).ToList();
    }

    public static SaleResponse ToResponse(Sale s, string? customerName, string? userName)
        => new(s.Id, s.Number, s.Code, s.Status.ToString(), s.CustomerId, customerName, s.Currency, s.Subtotal, s.DiscountAmount, s.Total,
            s.PaidAmount, s.ChangeAmount, s.RefundedAmount, s.CashSessionId, s.Notes, s.CreatedByUserId, userName, s.CreatedAtUtc, s.VoidReason,
            s.Lines.OrderBy(l => l.Position).Select(l => new SaleLineResponse(l.Id, l.Position, l.InventoryItemId, l.Sku, l.Description, l.Quantity, l.UnitPrice,
                l.DiscountAmount, l.LineTotal, l.RefundedQuantity, l.WarrantyDays)).ToList(),
            s.Payments.Select(p => new SalePaymentResponse(p.Id, p.Method.ToString(), p.Amount, p.Reference)).ToList(),
            s.Refunds.OrderBy(r => r.CreatedAtUtc).Select(r => new SaleRefundResponse(r.Id, r.Amount, r.Method.ToString(), r.Restocked, r.Reason, r.CreatedAtUtc)).ToList());
}
