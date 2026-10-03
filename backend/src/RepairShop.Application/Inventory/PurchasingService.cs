using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Domain.Common;
using RepairShop.Domain.Inventory;

namespace RepairShop.Application.Inventory;

/// <summary>Suppliers and purchase orders; receiving goods updates stock and weighted average cost.</summary>
public sealed class PurchasingService
{
    private const string EntityType = "purchase_order";

    private readonly ISupplierRepository _suppliers;
    private readonly IPurchaseOrderRepository _purchases;
    private readonly IInventoryItemRepository _items;
    private readonly IInventoryAdjustmentRepository _adjustments;
    private readonly ICounterService _counters;
    private readonly IAuditLog _audit;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public PurchasingService(
        ISupplierRepository suppliers,
        IPurchaseOrderRepository purchases,
        IInventoryItemRepository items,
        IInventoryAdjustmentRepository adjustments,
        ICounterService counters,
        IAuditLog audit,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _suppliers = suppliers;
        _purchases = purchases;
        _items = items;
        _adjustments = adjustments;
        _counters = counters;
        _audit = audit;
        _uow = uow;
        _clock = clock;
    }

    // ===== Suppliers =====

    public async Task<PagedResult<SupplierResponse>> SearchSuppliersAsync(Guid shopId, string? q, bool includeInactive, int skip, int take, CancellationToken ct)
    {
        var (items, total) = await _suppliers.SearchAsync(shopId, q, includeInactive, skip, take, ct);
        return new PagedResult<SupplierResponse>(items.Select(ToResponse).ToList(), total);
    }

    public async Task<SupplierResponse> CreateSupplierAsync(Guid shopId, SupplierRequest req, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var s = new Supplier(shopId, req.Name, now);
        s.Update(req.Name, req.ContactName, req.Phone, req.Email, req.TaxId, req.Notes, req.IsActive, now);
        await _suppliers.AddAsync(s, ct);
        await _audit.AddAsync(shopId, "supplier", s.Id, "supplier_created", actor, new { s.Name }, ct);
        await _uow.SaveChangesAsync(ct);
        return ToResponse(s);
    }

    public async Task<SupplierResponse> UpdateSupplierAsync(Guid shopId, Guid id, SupplierRequest req, Actor actor, CancellationToken ct)
    {
        var s = await _suppliers.GetByIdAsync(shopId, id, ct) ?? throw new NotFoundException("Proveedor no encontrado.");
        s.Update(req.Name, req.ContactName, req.Phone, req.Email, req.TaxId, req.Notes, req.IsActive, _clock.UtcNow);
        await _audit.AddAsync(shopId, "supplier", s.Id, "supplier_updated", actor, null, ct);
        await _uow.SaveChangesAsync(ct);
        return ToResponse(s);
    }

    // ===== Purchase orders =====

    public async Task<PagedResult<PurchaseOrderResponse>> SearchAsync(Guid shopId, PurchaseOrderStatus? status, Guid? supplierId, int skip, int take, CancellationToken ct)
    {
        var (items, total) = await _purchases.SearchAsync(shopId, status, supplierId, skip, take, ct);
        var names = await SupplierNamesAsync(shopId, items.Select(i => i.SupplierId), ct);
        return new PagedResult<PurchaseOrderResponse>(items.Select(p => ToResponse(p, names.GetValueOrDefault(p.SupplierId))).ToList(), total);
    }

    public async Task<PurchaseOrderResponse> GetAsync(Guid shopId, Guid id, CancellationToken ct)
    {
        var po = await RequireAsync(shopId, id, ct);
        var names = await SupplierNamesAsync(shopId, new[] { po.SupplierId }, ct);
        return ToResponse(po, names.GetValueOrDefault(po.SupplierId));
    }

    public async Task<PurchaseOrderResponse> CreateAsync(Guid shopId, SavePurchaseOrderRequest req, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var supplier = await _suppliers.GetByIdAsync(shopId, req.SupplierId, ct) ?? throw new NotFoundException("Proveedor no encontrado.");
        var lines = await BuildLinesAsync(shopId, req.Lines, ct);

        var po = await _uow.InTransactionAsync(async c =>
        {
            var p = new PurchaseOrder(shopId, await _counters.NextAsync(shopId, CounterKeys.PurchaseOrder, c), supplier.Id, req.Currency, req.Notes, req.ExpectedAtUtc, actor.UserId, now);
            p.SetLines(lines, now);
            await _purchases.AddAsync(p, c);
            await _audit.AddAsync(shopId, EntityType, p.Id, "purchase_order_created", actor, new { p.Number, p.Total, p.Currency }, c);
            await _uow.SaveChangesAsync(c);
            return p;
        }, ct);

        return await GetAsync(shopId, po.Id, ct);
    }

    public async Task<PurchaseOrderResponse> UpdateAsync(Guid shopId, Guid id, SavePurchaseOrderRequest req, Actor actor, CancellationToken ct)
    {
        var po = await RequireAsync(shopId, id, ct);
        if (po.SupplierId != req.SupplierId) throw new DomainException("No se puede cambiar el proveedor de una compra.");
        po.SetLines(await BuildLinesAsync(shopId, req.Lines, ct), _clock.UtcNow);
        await _audit.AddAsync(shopId, EntityType, po.Id, "purchase_order_updated", actor, new { po.Total }, ct);
        await _uow.SaveChangesAsync(ct);
        return await GetAsync(shopId, id, ct);
    }

    public async Task<PurchaseOrderResponse> MarkOrderedAsync(Guid shopId, Guid id, Actor actor, CancellationToken ct)
    {
        var po = await RequireAsync(shopId, id, ct);
        po.MarkOrdered(_clock.UtcNow);
        await _audit.AddAsync(shopId, EntityType, po.Id, "purchase_order_ordered", actor, null, ct);
        await _uow.SaveChangesAsync(ct);
        return await GetAsync(shopId, id, ct);
    }

    public async Task<PurchaseOrderResponse> CancelAsync(Guid shopId, Guid id, Actor actor, CancellationToken ct)
    {
        var po = await RequireAsync(shopId, id, ct);
        po.Cancel(_clock.UtcNow);
        await _audit.AddAsync(shopId, EntityType, po.Id, "purchase_order_cancelled", actor, null, ct);
        await _uow.SaveChangesAsync(ct);
        return await GetAsync(shopId, id, ct);
    }

    public async Task<PurchaseOrderResponse> ReceiveAsync(Guid shopId, Guid id, ReceivePurchaseOrderRequest req, Actor actor, CancellationToken ct)
    {
        await _uow.RetryOnConflictAsync(async c =>
        {
            var now = _clock.UtcNow;
            var po = await RequireAsync(shopId, id, c);
            var received = po.Receive(req.Lines.GroupBy(l => l.LineId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity)), now);

            var items = (await _items.GetByIdsAsync(shopId, received.Select(r => r.Line.InventoryItemId).Distinct().ToList(), c)).ToDictionary(i => i.Id);
            foreach (var (line, qty) in received)
            {
                if (!items.TryGetValue(line.InventoryItemId, out var item)) throw new NotFoundException("Un ítem de la compra ya no existe.");
                item.ReceivePurchase(qty, line.UnitCost, po.Currency, now);
                if (item.TrackStock)
                    await _adjustments.AddAsync(new InventoryAdjustment(shopId, item.Id, InventoryAdjustmentType.Purchase, qty, $"Compra {po.Code}", null, actor.UserId, now)
                        .WithReference(EntityType, po.Id), c);
            }

            await _audit.AddAsync(shopId, EntityType, po.Id, "purchase_order_received", actor,
                new { lines = received.Select(r => new { r.Line.Id, r.Quantity }), status = po.Status.ToString() }, c);
            await _uow.SaveChangesAsync(c);
            return true;
        }, ct);

        return await GetAsync(shopId, id, ct);
    }

    // ---------------------------------------------------------------------------------------------

    private async Task<List<(Guid InventoryItemId, string Description, int Quantity, decimal UnitCost)>> BuildLinesAsync(Guid shopId, IReadOnlyList<PurchaseOrderLineRequest> lines, CancellationToken ct)
    {
        if (lines is null || lines.Count == 0) throw new DomainException("La compra debe tener al menos un ítem.");
        var items = (await _items.GetByIdsAsync(shopId, lines.Select(l => l.InventoryItemId).Distinct().ToList(), ct)).ToDictionary(i => i.Id);
        return lines.Select(l =>
        {
            if (!items.TryGetValue(l.InventoryItemId, out var item)) throw new NotFoundException("Uno de los ítems de la compra no existe.");
            return (item.Id, string.IsNullOrWhiteSpace(l.Description) ? $"{item.Sku} - {item.Name}" : l.Description!, l.Quantity, l.UnitCost);
        }).ToList();
    }

    private async Task<PurchaseOrder> RequireAsync(Guid shopId, Guid id, CancellationToken ct)
        => await _purchases.GetByIdAsync(shopId, id, ct) ?? throw new NotFoundException("Compra no encontrada.");

    private async Task<Dictionary<Guid, string>> SupplierNamesAsync(Guid shopId, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var result = new Dictionary<Guid, string>();
        foreach (var id in ids.Distinct())
        {
            var s = await _suppliers.GetByIdAsync(shopId, id, ct);
            if (s is not null) result[id] = s.Name;
        }
        return result;
    }

    private static SupplierResponse ToResponse(Supplier s)
        => new(s.Id, s.Name, s.ContactName, s.Phone, s.Email, s.TaxId, s.Notes, s.IsActive, s.CreatedAtUtc);

    private static PurchaseOrderResponse ToResponse(PurchaseOrder p, string? supplierName)
        => new(p.Id, p.Number, p.Code, p.SupplierId, supplierName, p.Status.ToString(), p.Currency, p.Total, p.Notes, p.ExpectedAtUtc,
            p.CreatedAtUtc, p.OrderedAtUtc, p.ReceivedAtUtc,
            p.Lines.Select(l => new PurchaseOrderLineResponse(l.Id, l.InventoryItemId, l.Description, l.Quantity, l.UnitCost, l.ReceivedQuantity, Money.Round(l.Quantity * l.UnitCost))).ToList());
}
