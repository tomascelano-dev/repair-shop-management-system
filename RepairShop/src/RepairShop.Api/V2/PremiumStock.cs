using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepairShop.Api.Security;
using RepairShop.Application.Common;
using RepairShop.Domain.Common;
using RepairShop.Domain.Inventory;
using RepairShop.Domain.Premium;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Api.V2;

public sealed record PriceRow(string Supplier, string Sku, string Description, string Compatibility, string Quality, decimal UnitCost, string Currency);
public sealed record ImportPrices(List<PriceRow> Rows, string Source);
public sealed record ReceiveStock(Guid? OfferId, Guid? ItemId, Guid BranchId, string Sku, string Name, string Supplier, string LotCode, string? Serial, int Quantity, decimal UnitCost, string Currency);
public sealed record ReserveStock(Guid LotId, Guid OrderId, int Quantity);
public sealed record ReservationAction(int Version, string Action);
public sealed record TransferStock(Guid LotId, Guid BranchId, int Quantity, int Version);
public sealed record AdjustStock(Guid LotId, int Delta, int Version, string Reason);
public sealed record MinimumStock(Guid ItemId, Guid BranchId, int Minimum);
public sealed record UpdateItemCost(Guid OfferId, Guid ItemId);

public sealed partial class PremiumController
{
    private static PriceRow ValidatePrice(PriceRow r) => new(Text(r.Supplier, "Proveedor", 2, 120), Text(r.Sku, "Código", 2, 80).ToUpperInvariant(),
        Text(r.Description, "Descripción", 2, 200), Text(r.Compatibility, "Compatibilidad", 0, 120), Text(r.Quality, "Calidad", 0, 80), Money(r.UnitCost), Currency(r.Currency));

    [HttpPost("prices"), Authorize(Policy = Policies.AdminOnly)]
    public Task<IActionResult> Import(ImportPrices body) => Change("premium:prices", body, async () => {
        if (body.Rows is null || body.Rows.Count is < 1 or > 500) throw new DomainException("Importá entre 1 y 500 filas por vez.");
        var source = Text(body.Source, "Origen", 0, 200); var rows = body.Rows.Select(ValidatePrice).ToList();
        if (rows.Select(x => (x.Supplier.ToUpperInvariant(), x.Sku)).Distinct().Count() != rows.Count) throw new DomainException("Hay códigos repetidos para el mismo proveedor. Revisá la vista previa.");
        var existing = await Own<SupplierPrice>().ToListAsync();
        foreach (var r in rows) {
            var p = existing.FirstOrDefault(x => x.Supplier.Equals(r.Supplier, StringComparison.OrdinalIgnoreCase) && x.Sku == r.Sku);
            if (p is null) { p = new SupplierPrice { ShopId = Shop }; db.SupplierPrices.Add(p); }
            p.Supplier = r.Supplier; p.Sku = r.Sku; p.Description = r.Description; p.Compatibility = r.Compatibility;
            p.Quality = r.Quality; p.UnitCost = r.UnitCost; p.Currency = r.Currency; p.Source = source; p.UpdatedAtUtc = DateTime.UtcNow; p.Version++;
        }
        return new { imported = rows.Count };
    });

    [HttpPost("prices/apply-cost"), Authorize(Policy = Policies.AdminOnly)]
    public Task<IActionResult> ApplyCost(UpdateItemCost body) => Change("premium:apply-cost", body, async () => {
        var offer = await Find<SupplierPrice>(body.OfferId);
        var item = await db.InventoryItems.SingleOrDefaultAsync(x => x.Id == body.ItemId && x.ShopId == Shop) ?? throw new NotFoundException("Repuesto no encontrado.");
        item.SetCost(offer.UnitCost, offer.Currency, DateTime.UtcNow);
        return new { item.Id };
    });

    private async Task<int> Available(StockLot lot) => lot.Quantity - await Own<StockReservation>().Where(x => x.LotId == lot.Id && x.Status == "Reserved").SumAsync(x => x.Quantity);
    private void Movement(StockLot lot, string kind, int quantity, string reason, Guid? order = null) =>
        db.StockMovements.Add(new StockMovement { ShopId = Shop, LotId = lot.Id, Kind = kind, Quantity = quantity, Reason = reason, OrderId = order, ActorId = Actor });
    private async Task<InventoryItem> Item(StockLot lot) => await db.InventoryItems.SingleAsync(x => x.Id == lot.ItemId && x.ShopId == Shop);

    [HttpPost("stock/receive"), Authorize(Policy = Policies.AdminOnly)]
    public Task<IActionResult> Receive(ReceiveStock body) => Change("premium:receive", body, async () => {
        var branch = await Find<PremiumBranch>(body.BranchId); var quantity = Qty(body.Quantity);
        var currency = Currency(body.Currency); var cost = Money(body.UnitCost);
        var supplier = Text(body.Supplier, "Proveedor", 0, 120); var lotCode = Text(body.LotCode, "Lote", 1, 80);
        var serial = string.IsNullOrWhiteSpace(body.Serial) ? null : Text(body.Serial, "Serie", 1, 100).ToUpperInvariant();
        if (serial != null && quantity != 1) throw new DomainException("Un repuesto con número de serie debe tener cantidad 1.");
        if (serial != null && await Own<StockLot>().AnyAsync(x => x.Serial == serial)) throw new DomainException("Ese número de serie ya existe en el historial del taller.");
        if (body.OfferId is Guid offerId) {
            var offer = await Find<SupplierPrice>(offerId);
            if (offer.UnitCost != cost || offer.Currency != currency) throw new WorkflowConflict("El precio del proveedor cambió. Revisá el importe antes de recibir.");
            supplier = offer.Supplier;
        }
        InventoryItem item;
        if (body.ItemId is Guid itemId) item = await db.InventoryItems.SingleOrDefaultAsync(x => x.ShopId == Shop && x.Id == itemId) ?? throw new NotFoundException("Repuesto no encontrado.");
        else {
            var sku = Text(body.Sku, "SKU", 2, 60).ToUpperInvariant();
            if (await db.InventoryItems.AnyAsync(x => x.ShopId == Shop && x.Sku == sku)) throw new DomainException("El SKU ya existe. Seleccioná el repuesto del catálogo.");
            item = new InventoryItem(Shop, sku, Text(body.Name, "Nombre", 2, 120), 0, cost, currency, true, DateTime.UtcNow); db.InventoryItems.Add(item);
        }
        if (!item.IsActive) throw new DomainException("El repuesto está inactivo.");
        item.ApplyDelta(quantity, DateTime.UtcNow); item.SetCost(cost, currency, DateTime.UtcNow);
        var lot = new StockLot { ShopId = Shop, ItemId = item.Id, BranchId = branch.Id, Quantity = quantity, UnitCost = cost, Currency = currency, Supplier = supplier, LotCode = lotCode, Serial = serial };
        db.StockLots.Add(lot); Movement(lot, "Receipt", quantity, $"Recepción de {supplier}");
        db.InventoryAdjustments.Add(new InventoryAdjustment(Shop, item.Id, InventoryAdjustmentType.Purchase, quantity, lotCode, null, Actor, DateTime.UtcNow));
        return new { lot.Id, itemId = item.Id };
    });

    [HttpPost("stock/reserve")]
    public Task<IActionResult> Reserve(ReserveStock body) => Change("premium:reserve", body, async () => {
        var (w, _) = await Order(body.OrderId, true); var lot = await Find<StockLot>(body.LotId); var quantity = Qty(body.Quantity);
        if (await Available(lot) < quantity) throw new DomainException("No hay suficientes unidades disponibles; algunas pueden estar reservadas.");
        if (lot.Currency != await OrderCurrency(w.Id)) throw new DomainException("La moneda del lote debe coincidir con la de la orden.");
        var assignment = await Own<OrderBusiness>().SingleOrDefaultAsync(x => x.OrderId == w.Id);
        if (assignment is not null && assignment.BranchId != lot.BranchId) throw new DomainException("El lote está en otra sucursal. Transferilo antes de reservarlo.");
        if (assignment is null) db.OrderBusinesses.Add(new OrderBusiness { ShopId = Shop, OrderId = w.Id, BranchId = lot.BranchId });
        var reservation = new StockReservation { ShopId = Shop, LotId = lot.Id, OrderId = w.Id, Quantity = quantity, UnitCost = lot.UnitCost, Currency = lot.Currency };
        db.StockReservations.Add(reservation); lot.Version++; w.Version++;
        Movement(lot, "Reservation", quantity, "Reserva para reparación", w.Id); Note(w.Id, $"Se reservaron {quantity} unidades del lote {lot.LotCode}.");
        return new { reservation.Id };
    });

    [HttpPost("stock/reservations/{id:guid}")]
    public Task<IActionResult> Reservation(Guid id, ReservationAction body) => Change($"premium:reservation:{id}", body, async () => {
        var r = await Find<StockReservation>(id); var (w, o) = await Order(r.OrderId); Version(r, body.Version);
        if (r.Status != "Reserved") throw new DomainException("La reserva ya fue consumida o liberada.");
        var lot = await Find<StockLot>(r.LotId);
        if (body.Action == "release") { r.Status = "Released"; Movement(lot, "Release", r.Quantity, "Reserva liberada", w.Id); }
        else if (body.Action == "consume") {
            if (o.Status is not (RepairOrderStatus.InProgress or RepairOrderStatus.QualityCheck or RepairOrderStatus.WaitingParts)) throw new DomainException("Para consumir repuestos la orden debe estar en reparación, espera de repuesto o control de calidad.");
            var accepted = await db.WorkflowQuotes.Where(x => x.OrderId == w.Id && x.ShopId == Shop).OrderByDescending(x => x.Revision).FirstOrDefaultAsync();
            if (accepted?.Status != "Accepted") throw new DomainException("Primero necesitás un presupuesto aprobado.");
            if (accepted.Currency != r.Currency) throw new DomainException("La moneda del presupuesto no coincide con la del lote.");
            if (lot.Quantity < r.Quantity) throw new WorkflowConflict("El lote no tiene stock suficiente.");
            lot.Quantity -= r.Quantity; r.Status = "Consumed"; r.ConsumedAtUtc = DateTime.UtcNow;
            var item = await Item(lot); item.ApplyDelta(-r.Quantity, DateTime.UtcNow);
            db.RepairOrderPartUsages.Add(new RepairOrderPartUsage(Shop, w.Id, item.Id, r.Quantity, null, null, Actor, DateTime.UtcNow));
            db.InventoryAdjustments.Add(new InventoryAdjustment(Shop, item.Id, InventoryAdjustmentType.Consumption, -r.Quantity, lot.LotCode, w.Id, Actor, DateTime.UtcNow));
            Movement(lot, "Consumption", -r.Quantity, "Instalado en reparación", w.Id);
        } else throw new DomainException("Acción de reserva inválida.");
        lot.Version++; w.Version++; Note(w.Id, $"Reserva {lot.LotCode}: {(r.Status == "Consumed" ? "repuesto consumido" : "liberada")} ({r.Quantity}).");
        return new { r.Id, r.Status };
    });

    [HttpPost("stock/transfer"), Authorize(Policy = Policies.AdminOnly)]
    public Task<IActionResult> Transfer(TransferStock body) => Change("premium:transfer", body, async () => {
        var source = await Find<StockLot>(body.LotId); Version(source, body.Version);
        var target = await Find<PremiumBranch>(body.BranchId); var qty = Qty(body.Quantity);
        var from = await Find<PremiumBranch>(source.BranchId);
        if (target.Id == from.Id) throw new DomainException("Elegí una sucursal diferente.");
        if (await Available(source) < qty) throw new DomainException("No podés transferir stock reservado o inexistente.");
        if (source.Serial != null) {
            source.BranchId = target.Id; Movement(source, "Transfer", qty, $"{from.Name} → {target.Name}");
        } else {
            source.Quantity -= qty;
            var dest = new StockLot { ShopId = Shop, ItemId = source.ItemId, BranchId = target.Id, LotCode = source.LotCode, Supplier = source.Supplier, Quantity = qty, UnitCost = source.UnitCost, Currency = source.Currency };
            db.StockLots.Add(dest); Movement(source, "TransferOut", -qty, $"Hacia {target.Name}"); Movement(dest, "TransferIn", qty, $"Desde {from.Name}");
        }
        return new { source.Id };
    });

    [HttpPost("stock/adjust"), Authorize(Policy = Policies.AdminOnly)]
    public Task<IActionResult> Adjust(AdjustStock body) => Change("premium:adjust", body, async () => {
        var lot = await Find<StockLot>(body.LotId); Version(lot, body.Version);
        var reason = Text(body.Reason, "Motivo", 3, 300);
        if (body.Delta == 0 || Math.Abs((long)body.Delta) > 10000 || await Available(lot) + body.Delta < 0) throw new DomainException("El ajuste no puede dejar stock disponible negativo.");
        if (lot.Serial != null && (lot.Quantity + body.Delta is < 0 or > 1)) throw new DomainException("Un número de serie solo puede tener 0 o 1 unidades.");
        lot.Quantity += body.Delta; (await Item(lot)).ApplyDelta(body.Delta, DateTime.UtcNow);
        Movement(lot, "Adjustment", body.Delta, reason);
        db.InventoryAdjustments.Add(new InventoryAdjustment(Shop, lot.ItemId, InventoryAdjustmentType.Correction, body.Delta, reason, null, Actor, DateTime.UtcNow));
        return new { lot.Id };
    });

    [HttpPost("stock/minimum"), Authorize(Policy = Policies.AdminOnly)]
    public Task<IActionResult> Minimum(MinimumStock body) => Change("premium:minimum", body, async () => {
        await Find<PremiumBranch>(body.BranchId);
        if (!await db.InventoryItems.AnyAsync(x => x.ShopId == Shop && x.Id == body.ItemId)) throw new NotFoundException("Repuesto no encontrado.");
        if (body.Minimum is < 0 or > 10000) throw new DomainException("El mínimo debe estar entre 0 y 10.000.");
        var rule = await Own<StockMinimum>().SingleOrDefaultAsync(x => x.ItemId == body.ItemId && x.BranchId == body.BranchId);
        if (rule is null) { rule = new StockMinimum { ShopId = Shop, ItemId = body.ItemId, BranchId = body.BranchId }; db.StockMinimums.Add(rule); }
        rule.Minimum = body.Minimum; rule.Version++;
        return new { rule.Id };
    });
}
