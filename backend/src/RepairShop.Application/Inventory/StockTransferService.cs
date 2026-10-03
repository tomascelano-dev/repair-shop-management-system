using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Domain.Common;
using RepairShop.Domain.Inventory;

namespace RepairShop.Application.Inventory;

/// <summary>
/// Stock transfers between branches of the same organization. Items are matched by SKU in the destination
/// (created automatically when missing).
/// </summary>
public sealed class StockTransferService
{
    private const string EntityType = "stock_transfer";

    private readonly IStockTransferRepository _transfers;
    private readonly IInventoryItemRepository _items;
    private readonly IInventoryAdjustmentRepository _adjustments;
    private readonly IInventoryReservationRepository _reservations;
    private readonly IShopRepository _shops;
    private readonly IUserShopAccessRepository _access;
    private readonly IUserRepository _users;
    private readonly ICounterService _counters;
    private readonly IAuditLog _audit;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public StockTransferService(
        IStockTransferRepository transfers,
        IInventoryItemRepository items,
        IInventoryAdjustmentRepository adjustments,
        IInventoryReservationRepository reservations,
        IShopRepository shops,
        IUserShopAccessRepository access,
        IUserRepository users,
        ICounterService counters,
        IAuditLog audit,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _transfers = transfers;
        _items = items;
        _adjustments = adjustments;
        _reservations = reservations;
        _shops = shops;
        _access = access;
        _users = users;
        _counters = counters;
        _audit = audit;
        _uow = uow;
        _clock = clock;
    }

    public async Task<PagedResult<TransferResponse>> ListAsync(Guid shopId, StockTransferStatus? status, int skip, int take, CancellationToken ct)
    {
        var (items, total) = await _transfers.SearchForShopAsync(shopId, status, skip, take, ct);
        var names = await ShopNamesAsync(items.SelectMany(t => new[] { t.FromShopId, t.ToShopId }), ct);
        return new PagedResult<TransferResponse>(items.Select(t => ToResponse(t, shopId, names)).ToList(), total);
    }

    public async Task<TransferResponse> CreateAsync(Guid shopId, CreateTransferRequest req, Actor actor, CancellationToken ct)
    {
        var from = await _shops.GetByIdAsync(shopId, ct) ?? throw new NotFoundException("Sucursal no encontrada.");
        var to = await _shops.GetByIdAsync(req.ToShopId, ct) ?? throw new NotFoundException("Sucursal de destino no encontrada.");
        if (to.OrganizationId != from.OrganizationId) throw new ForbiddenException("Solo se puede transferir entre sucursales de la misma organización.");
        if (!to.IsActive) throw new DomainException("La sucursal de destino está inactiva.");
        if (req.Lines is null || req.Lines.Count == 0) throw new DomainException("Agregá al menos un ítem.");

        var transfer = await _uow.InTransactionAsync(async c =>
        {
            var now = _clock.UtcNow;
            var t = new StockTransfer(from.OrganizationId, await _counters.NextAsync(from.OrganizationId, CounterKeys.StockTransfer, c), from.Id, to.Id, req.Notes, actor.UserId, now);
            var ids = req.Lines.Select(l => l.InventoryItemId).Distinct().ToList();
            var items = (await _items.GetByIdsAsync(shopId, ids, c)).ToDictionary(i => i.Id);
            var reserved = await _reservations.SumActiveByItemsAsync(shopId, ids, null, c);

            foreach (var line in req.Lines.GroupBy(l => l.InventoryItemId).Select(g => (Id: g.Key, Qty: g.Sum(x => x.Quantity))))
            {
                if (!items.TryGetValue(line.Id, out var item)) throw new NotFoundException("Uno de los ítems no existe en esta sucursal.");
                if (!item.TrackStock) throw new DomainException($"\"{item.Name}\" no controla stock.");
                if (line.Qty > item.QuantityOnHand - reserved.GetValueOrDefault(item.Id))
                    throw new DomainException($"Stock libre insuficiente de \"{item.Name}\".");

                t.AddLine(item.Id, item.Sku, item.Name, line.Qty, item.UnitCost, item.UnitCostCurrency);
                item.ApplyDelta(-line.Qty, now);
                await _adjustments.AddAsync(new InventoryAdjustment(shopId, item.Id, InventoryAdjustmentType.TransferOut, -line.Qty, $"Transferencia a {to.Name}", null, actor.UserId, now)
                    .WithReference(EntityType, t.Id), c);
            }

            await _transfers.AddAsync(t, c);
            await _audit.AddAsync(shopId, EntityType, t.Id, "transfer_created", actor, new { t.Number, to = to.Name, lines = t.Lines.Count }, c);
            await _uow.SaveChangesAsync(c);
            return t;
        }, ct);

        return ToResponse(transfer, shopId, await ShopNamesAsync(new[] { from.Id, to.Id }, ct));
    }

    /// <summary>Received in the destination branch (the caller must be working in it).</summary>
    public async Task<TransferResponse> ReceiveAsync(Guid shopId, Guid id, Actor actor, CancellationToken ct)
    {
        var transfer = await _uow.InTransactionAsync(async c =>
        {
            var now = _clock.UtcNow;
            var t = await _transfers.GetByIdAsync(id, c) ?? throw new NotFoundException("Transferencia no encontrada.");
            if (t.ToShopId != shopId) throw new ForbiddenException("La transferencia se recibe desde la sucursal de destino.");
            t.MarkReceived(actor.UserId, now);

            var from = await _shops.GetByIdAsync(t.FromShopId, c);
            foreach (var line in t.Lines)
            {
                var item = await _items.GetBySkuAsync(shopId, line.Sku, c);
                if (item is null)
                {
                    item = new InventoryItem(shopId, line.Sku, line.Name, 0, line.UnitCost, line.UnitCostCurrency, true, now);
                    await _items.AddAsync(item, c);
                }

                if (line.UnitCost is not null && line.UnitCostCurrency is not null) item.ReceivePurchase(line.Quantity, line.UnitCost.Value, line.UnitCostCurrency, now);
                else item.ApplyDelta(line.Quantity, now);

                await _adjustments.AddAsync(new InventoryAdjustment(shopId, item.Id, InventoryAdjustmentType.TransferIn, line.Quantity, $"Transferencia desde {from?.Name}", null, actor.UserId, now)
                    .WithReference(EntityType, t.Id), c);
            }

            await _audit.AddAsync(shopId, EntityType, t.Id, "transfer_received", actor, new { t.Number }, c);
            await _uow.SaveChangesAsync(c);
            return t;
        }, ct);

        return ToResponse(transfer, shopId, await ShopNamesAsync(new[] { transfer.FromShopId, transfer.ToShopId }, ct));
    }

    /// <summary>Cancelled from the origin branch: the stock goes back.</summary>
    public async Task<TransferResponse> CancelAsync(Guid shopId, Guid id, Actor actor, CancellationToken ct)
    {
        var transfer = await _uow.InTransactionAsync(async c =>
        {
            var now = _clock.UtcNow;
            var t = await _transfers.GetByIdAsync(id, c) ?? throw new NotFoundException("Transferencia no encontrada.");
            if (t.FromShopId != shopId) throw new ForbiddenException("La transferencia se cancela desde la sucursal de origen.");
            t.Cancel(now);

            var items = (await _items.GetByIdsAsync(shopId, t.Lines.Select(l => l.SourceItemId).ToList(), c)).ToDictionary(i => i.Id);
            foreach (var line in t.Lines)
            {
                if (!items.TryGetValue(line.SourceItemId, out var item)) continue;
                item.ApplyDelta(line.Quantity, now);
                await _adjustments.AddAsync(new InventoryAdjustment(shopId, item.Id, InventoryAdjustmentType.TransferIn, line.Quantity, "Transferencia cancelada", null, actor.UserId, now)
                    .WithReference(EntityType, t.Id), c);
            }

            await _audit.AddAsync(shopId, EntityType, t.Id, "transfer_cancelled", actor, new { t.Number }, c);
            await _uow.SaveChangesAsync(c);
            return t;
        }, ct);

        return ToResponse(transfer, shopId, await ShopNamesAsync(new[] { transfer.FromShopId, transfer.ToShopId }, ct));
    }

    private async Task<Dictionary<Guid, string>> ShopNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
        => (await _shops.GetByIdsAsync(ids.Distinct().ToList(), ct)).ToDictionary(s => s.Id, s => s.Name);

    private static TransferResponse ToResponse(StockTransfer t, Guid currentShopId, IReadOnlyDictionary<Guid, string> names)
        => new(t.Id, t.Code, t.FromShopId, names.GetValueOrDefault(t.FromShopId), t.ToShopId, names.GetValueOrDefault(t.ToShopId),
            t.Status.ToString(), t.Notes, t.CreatedAtUtc, t.ReceivedAtUtc, t.ToShopId == currentShopId,
            t.Lines.Select(l => new TransferLineResponse(l.Id, l.SourceItemId, l.Sku, l.Name, l.Quantity)).ToList());
}
