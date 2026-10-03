using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Application.RepairOrders;
using RepairShop.Domain.Common;
using RepairShop.Domain.Inventory;

namespace RepairShop.Application.Inventory;

public sealed class InventoryService
{
    private const string EntityTypeItem = "inventory_item";
    private const string EntityTypeOrder = RepairOrderService.EntityType;

    private readonly IInventoryItemRepository _items;
    private readonly IInventoryAdjustmentRepository _adjustments;
    private readonly IInventoryReservationRepository _reservations;
    private readonly IInventoryCompatibilityRepository _compat;
    private readonly IRepairOrderPartUsageRepository _usages;
    private readonly IRepairOrderRepository _orders;
    private readonly IRepairOrderReadModel _readModel;
    private readonly IShopRepository _shops;
    private readonly IAuditLog _audit;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public InventoryService(
        IInventoryItemRepository items,
        IInventoryAdjustmentRepository adjustments,
        IInventoryReservationRepository reservations,
        IInventoryCompatibilityRepository compat,
        IRepairOrderPartUsageRepository usages,
        IRepairOrderRepository orders,
        IRepairOrderReadModel readModel,
        IShopRepository shops,
        IAuditLog audit,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _items = items;
        _adjustments = adjustments;
        _reservations = reservations;
        _compat = compat;
        _usages = usages;
        _orders = orders;
        _readModel = readModel;
        _shops = shops;
        _audit = audit;
        _uow = uow;
        _clock = clock;
    }

    // ===== Items =====

    public async Task<PagedResult<InventoryItemResponse>> SearchAsync(Guid shopId, InventorySearchOptions options, CancellationToken ct)
    {
        var (items, total) = await _items.SearchAsync(shopId, options, ct);
        return new PagedResult<InventoryItemResponse>(await ToResponsesAsync(shopId, items, ct), total);
    }

    public async Task<InventoryItemResponse> GetAsync(Guid shopId, Guid id, CancellationToken ct)
        => (await ToResponsesAsync(shopId, new[] { await RequireItemAsync(shopId, id, ct) }, ct)).Single();

    /// <summary>Scanner lookup: barcode first, then SKU.</summary>
    public async Task<InventoryItemResponse> FindByCodeAsync(Guid shopId, string code, CancellationToken ct)
    {
        code = (code ?? "").Trim();
        if (code.Length == 0) throw new NotFoundException("Código vacío.");
        var item = await _items.GetByBarcodeAsync(shopId, code, ct) ?? await _items.GetBySkuAsync(shopId, code, ct)
                   ?? throw new NotFoundException($"No hay ningún ítem con el código \"{code}\".");
        return (await ToResponsesAsync(shopId, new[] { item }, ct)).Single();
    }

    public async Task<InventoryItemResponse> CreateAsync(Guid shopId, CreateInventoryItemRequest req, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        if (await _items.GetBySkuAsync(shopId, req.Sku, ct) is not null) throw new ConflictException("Ya existe un ítem con ese SKU.");
        if (!string.IsNullOrWhiteSpace(req.Barcode) && await _items.GetByBarcodeAsync(shopId, req.Barcode, ct) is not null)
            throw new ConflictException("Ya existe un ítem con ese código de barras.");

        var shop = await _shops.GetByIdAsync(shopId, ct);
        var item = new InventoryItem(shopId, req.Sku, req.Name, req.TrackStock ? req.InitialQuantity : 0, req.UnitCost,
            req.UnitCost is null ? null : req.UnitCostCurrency ?? shop?.DefaultCurrency, req.IsActive, now);
        item.UpdateCatalog(req.Category, req.Barcode, req.MinStock, req.TrackStock, req.IsSellable, req.SalePrice,
            req.SalePrice is null ? null : req.SalePriceCurrency ?? shop?.DefaultCurrency, req.WarrantyDays, req.Location, now);
        await _items.AddAsync(item, ct);

        if (req.TrackStock && req.InitialQuantity != 0)
            await _adjustments.AddAsync(new InventoryAdjustment(shopId, item.Id, InventoryAdjustmentType.Correction, req.InitialQuantity, "initial_quantity", null, actor.UserId, now), ct);

        await _audit.AddAsync(shopId, EntityTypeItem, item.Id, "inventory_item_created", actor, new { itemId = item.Id, sku = item.Sku, name = item.Name }, ct);
        await _uow.SaveChangesAsync(ct);
        return await GetAsync(shopId, item.Id, ct);
    }

    public async Task<InventoryItemResponse> UpdateAsync(Guid shopId, Guid id, UpdateInventoryItemRequest req, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var item = await RequireItemAsync(shopId, id, ct);
        if (!string.IsNullOrWhiteSpace(req.Barcode))
        {
            var other = await _items.GetByBarcodeAsync(shopId, req.Barcode, ct);
            if (other is not null && other.Id != item.Id) throw new ConflictException("Ya existe un ítem con ese código de barras.");
        }

        var shop = await _shops.GetByIdAsync(shopId, ct);
        item.Update(req.Name, req.IsActive, now);
        item.UpdateCatalog(req.Category, req.Barcode, req.MinStock, req.TrackStock, req.IsSellable, req.SalePrice,
            req.SalePrice is null ? null : req.SalePriceCurrency ?? shop?.DefaultCurrency, req.WarrantyDays, req.Location, now);
        if (req.UnitCost is not null) item.UpdateCost(req.UnitCost, req.UnitCostCurrency ?? item.UnitCostCurrency ?? shop?.DefaultCurrency, now);

        await _audit.AddAsync(shopId, EntityTypeItem, item.Id, "inventory_item_updated", actor, new { itemId = item.Id }, ct);
        await _uow.SaveChangesAsync(ct);
        return await GetAsync(shopId, id, ct);
    }

    public async Task<InventoryItemResponse> AddAdjustmentAsync(Guid shopId, Guid id, CreateInventoryAdjustmentRequest req, Actor actor, CancellationToken ct)
    {
        if (req.Type is InventoryAdjustmentType.Sale or InventoryAdjustmentType.Consumption or InventoryAdjustmentType.TransferIn or InventoryAdjustmentType.TransferOut)
            throw new DomainException("Ese tipo de movimiento se genera automáticamente (ventas, órdenes o transferencias).");

        await _uow.RetryOnConflictAsync(async c =>
        {
            var now = _clock.UtcNow;
            var item = await RequireItemAsync(shopId, id, c);
            if (!item.TrackStock) throw new DomainException("Este ítem no controla stock.");
            item.ApplyDelta(req.DeltaQuantity, now);
            await _adjustments.AddAsync(new InventoryAdjustment(shopId, item.Id, req.Type, req.DeltaQuantity, req.Reason, null, actor.UserId, now), c);
            await _audit.AddAsync(shopId, EntityTypeItem, item.Id, "inventory_adjusted", actor,
                new { itemId = item.Id, delta = req.DeltaQuantity, type = req.Type.ToString(), reason = req.Reason }, c);
            await _uow.SaveChangesAsync(c);
            return true;
        }, ct);

        return await GetAsync(shopId, id, ct);
    }

    public async Task<List<InventoryAdjustmentResponse>> ListAdjustmentsAsync(Guid shopId, Guid id, int skip, int take, CancellationToken ct)
    {
        (skip, take) = Paging.Normalize(skip, take);
        return (await _adjustments.ListByItemAsync(shopId, id, skip, take, ct))
            .Select(a => new InventoryAdjustmentResponse(a.Id, a.InventoryItemId, a.Type, a.DeltaQuantity, a.Reason, a.RepairOrderId, a.CreatedByUserId, a.CreatedAtUtc, a.ReferenceType, a.ReferenceId))
            .ToList();
    }

    // ===== Compatibility =====

    public async Task<List<CompatibilityResponse>> ListCompatibilityAsync(Guid shopId, Guid itemId, CancellationToken ct)
    {
        await RequireItemAsync(shopId, itemId, ct);
        return (await _compat.ListByItemAsync(shopId, itemId, ct)).Select(c => new CompatibilityResponse(c.Id, c.Brand, c.Model)).ToList();
    }

    public async Task<CompatibilityResponse> AddCompatibilityAsync(Guid shopId, Guid itemId, CompatibilityRequest req, Actor actor, CancellationToken ct)
    {
        await RequireItemAsync(shopId, itemId, ct);
        var existing = await _compat.ListByItemAsync(shopId, itemId, ct);
        if (existing.Any(c => c.BrandKey == req.Brand.Trim().ToLowerInvariant() && c.ModelKey == req.Model.Trim().ToLowerInvariant()))
            throw new ConflictException("Esa compatibilidad ya está cargada.");
        var compat = new InventoryItemCompatibility(shopId, itemId, req.Brand, req.Model);
        await _compat.AddAsync(compat, ct);
        await _uow.SaveChangesAsync(ct);
        return new CompatibilityResponse(compat.Id, compat.Brand, compat.Model);
    }

    public async Task RemoveCompatibilityAsync(Guid shopId, Guid itemId, Guid compatibilityId, CancellationToken ct)
    {
        var compat = (await _compat.ListByItemAsync(shopId, itemId, ct)).FirstOrDefault(c => c.Id == compatibilityId)
                     ?? throw new NotFoundException("Compatibilidad no encontrada.");
        _compat.Remove(compat);
        await _uow.SaveChangesAsync(ct);
    }

    // ===== Parts used on repair orders =====

    public async Task<List<RepairOrderPartUsageResponse>> UseOnOrderAsync(Guid shopId, Guid orderId, UsePartOnOrderRequest req, Actor actor, CancellationToken ct)
    {
        if (req.QuantityUsed <= 0) throw new DomainException("La cantidad usada debe ser mayor a 0.");

        return await _uow.RetryOnConflictAsync(async c =>
        {
            var now = _clock.UtcNow;
            var order = await _orders.GetByIdAsync(shopId, orderId, c) ?? throw new NotFoundException("Orden no encontrada.");
            if (order.IsFinal) throw new DomainException("No se pueden cargar repuestos en una orden entregada o cancelada.");

            var item = await RequireItemAsync(shopId, req.InventoryItemId, c);
            if (!item.IsActive) throw new DomainException("El ítem está inactivo.");

            // Parts promised to this order (approved quote) are consumed first and never charged twice.
            var own = (await _reservations.ListActiveByOrderAsync(shopId, orderId, c)).Where(r => r.InventoryItemId == item.Id).ToList();
            var reservedForOthers = (await _reservations.SumActiveByItemsAsync(shopId, new[] { item.Id }, orderId, c)).GetValueOrDefault(item.Id);
            if (item.TrackStock && req.QuantityUsed > item.QuantityOnHand - reservedForOthers)
                throw new DomainException($"Stock disponible insuficiente de \"{item.Name}\": hay {Math.Max(0, item.QuantityOnHand - reservedForOthers)} sin reservar para otras órdenes.");

            var remaining = req.QuantityUsed;
            var covered = 0;
            Guid? reservationId = null;
            foreach (var r in own)
            {
                var took = r.Consume(remaining - covered, now);
                if (took > 0) reservationId ??= r.Id;
                covered += took;
                if (covered >= remaining) break;
            }

            var uncovered = req.QuantityUsed - covered;
            var charge = uncovered > 0 && req.UnitPrice is > 0;
            string? chargeCurrency = null;
            if (charge)
            {
                var info = (await _readModel.GetInfoAsync(shopId, new[] { order.Id }, c))[order.Id];
                var shop = await _shops.GetByIdAsync(shopId, c);
                var orderCurrency = OrderMapping.Financials(order, info, shop?.DefaultCurrency ?? "ARS").Currency;
                chargeCurrency = string.IsNullOrWhiteSpace(req.UnitPriceCurrency) ? orderCurrency : Money.NormalizeCurrency(req.UnitPriceCurrency);
                if (chargeCurrency != orderCurrency) throw new DomainException($"El repuesto debe cobrarse en la moneda de la orden ({orderCurrency}).");
            }

            item.ApplyDelta(-req.QuantityUsed, now);

            var created = new List<RepairOrderPartUsage>();
            if (covered > 0)
            {
                var u = new RepairOrderPartUsage(shopId, orderId, item.Id, covered, null, null, actor.UserId, now);
                u.SnapshotCost(item.UnitCost, item.UnitCostCurrency);
                if (reservationId is not null) u.LinkReservation(reservationId.Value);
                created.Add(u);
            }
            if (uncovered > 0)
            {
                var u = new RepairOrderPartUsage(shopId, orderId, item.Id, uncovered, charge ? req.UnitPrice : null, chargeCurrency, actor.UserId, now);
                u.SnapshotCost(item.UnitCost, item.UnitCostCurrency);
                created.Add(u);
            }

            foreach (var u in created) await _usages.AddAsync(u, c);
            if (item.TrackStock)
                await _adjustments.AddAsync(new InventoryAdjustment(shopId, item.Id, InventoryAdjustmentType.Consumption, -req.QuantityUsed, "used_on_order", orderId, actor.UserId, now)
                    .WithReference(EntityTypeOrder, orderId), c);
            if (charge) order.TouchFinancials(now);

            await _audit.AddAsync(shopId, EntityTypeOrder, orderId, "part_used", actor, new
            {
                orderId,
                inventoryItemId = item.Id,
                sku = item.Sku,
                quantityUsed = req.QuantityUsed,
                coveredByQuote = covered,
                unitPrice = charge ? req.UnitPrice : null,
                currency = chargeCurrency
            }, c);
            await _uow.SaveChangesAsync(c);

            return created.Select(u => ToResponse(u, item)).ToList();
        }, ct);
    }

    public async Task<List<RepairOrderPartUsageResponse>> ListByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
    {
        var usages = await _usages.ListByOrderAsync(shopId, orderId, ct);
        var items = (await _items.GetByIdsAsync(shopId, usages.Select(u => u.InventoryItemId).Distinct().ToList(), ct)).ToDictionary(i => i.Id);
        return usages.Select(u => ToResponse(u, items.GetValueOrDefault(u.InventoryItemId))).ToList();
    }

    public async Task<List<ReservationResponse>> ListReservationsAsync(Guid shopId, Guid orderId, CancellationToken ct)
    {
        var reservations = await _reservations.ListActiveByOrderAsync(shopId, orderId, ct);
        var items = (await _items.GetByIdsAsync(shopId, reservations.Select(r => r.InventoryItemId).Distinct().ToList(), ct)).ToDictionary(i => i.Id);
        return reservations.Select(r => new ReservationResponse(r.Id, r.InventoryItemId, items.GetValueOrDefault(r.InventoryItemId)?.Name ?? "?",
            r.RepairOrderId, r.Quantity, r.ConsumedQuantity, r.Status.ToString(), r.CreatedAtUtc)).ToList();
    }

    // ---------------------------------------------------------------------------------------------

    public async Task<List<InventoryItemResponse>> ToResponsesAsync(Guid shopId, IReadOnlyCollection<InventoryItem> items, CancellationToken ct)
    {
        var reserved = items.Count == 0
            ? new Dictionary<Guid, int>()
            : await _reservations.SumActiveByItemsAsync(shopId, items.Select(i => i.Id).ToList(), null, ct);
        return items.Select(i => ToResponse(i, reserved.GetValueOrDefault(i.Id))).ToList();
    }

    public static InventoryItemResponse ToResponse(InventoryItem i, int reserved)
        => new(i.Id, i.ShopId, i.Sku, i.Name, i.QuantityOnHand, i.UnitCost, i.UnitCostCurrency, i.IsActive, i.CreatedAtUtc, i.UpdatedAtUtc,
            i.Category, i.Barcode, i.MinStock, i.TrackStock, i.IsSellable, i.SalePrice, i.SalePriceCurrency, i.WarrantyDays, i.Location,
            reserved, i.TrackStock ? i.QuantityOnHand - reserved : 0, i.IsLowStock(reserved));

    private static RepairOrderPartUsageResponse ToResponse(RepairOrderPartUsage u, InventoryItem? item)
        => new(u.Id, u.RepairOrderId, u.InventoryItemId, u.QuantityUsed, u.UnitPrice, u.UnitPriceCurrency, u.CreatedByUserId, u.CreatedAtUtc,
            item?.Name, item?.Sku, u.ChargedToCustomer, u.UnitCost, u.ReservationId is not null);

    private async Task<InventoryItem> RequireItemAsync(Guid shopId, Guid id, CancellationToken ct)
        => await _items.GetByIdAsync(shopId, id, ct) ?? throw new NotFoundException("Ítem de inventario no encontrado.");
}
