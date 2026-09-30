using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepairShop.Api.Security;
using RepairShop.Domain.Common;
using RepairShop.Domain.Premium;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Api.V2;

public sealed record AddExpense(Guid OrderId, string Kind, string Description, int Minutes, decimal HourlyRate, decimal Amount, string Currency);
public sealed record OpenWarranty(Guid OrderId, Guid? ReservationId, string Problem, string FailureCode, string Supplier);
public sealed record EditWarranty(int Version, string Status, string SupplierClaim, decimal Cost, decimal Recovered, string Resolution);
public sealed record AcquireDevice(string Model, string Identifier, string Seller, string Acquisition, string Grade, string Diagnosis, decimal PurchasePrice, decimal TargetPrice, string Currency, Guid BranchId);
public sealed record EditRefurb(int Version, string Status, string Grade, string Diagnosis, decimal TargetPrice, Dictionary<string, bool> QualityChecks, decimal? SalePrice, string? Buyer);
public sealed record RefurbCost(int Version, string Description, decimal Amount);

public sealed partial class PremiumController
{
    [HttpPost("expenses")]
    public Task<IActionResult> Expense(AddExpense body) => Change("premium:expense", body, async () => {
        var (w, _) = await Order(body.OrderId);
        if (body.Kind is not ("Labor" or "Commission" or "Other")) throw new DomainException("Tipo de costo inválido.");
        if (!User.IsInRole("Admin") && body.Kind != "Labor") throw new DomainException("Solo el administrador registra comisiones y otros gastos.");
        var currency = Currency(body.Currency);
        if (currency != await OrderCurrency(w.Id)) throw new DomainException("El costo debe usar la moneda de la orden.");
        var minutes = body.Kind == "Labor" ? body.Minutes : 0; var rate = body.Kind == "Labor" ? Money(body.HourlyRate) : 0;
        if (body.Kind == "Labor" && minutes is < 1 or > 1440) throw new DomainException("Registrá entre 1 y 1.440 minutos por entrada.");
        var expense = new OrderExpense { ShopId = Shop, OrderId = w.Id, Kind = body.Kind, Description = Text(body.Description, "Descripción", 3, 300), Minutes = minutes, HourlyRate = rate,
            Amount = body.Kind == "Labor" ? Money(minutes / 60m * rate) : Money(body.Amount), Currency = currency, ActorId = Actor };
        db.OrderExpenses.Add(expense); w.Version++; Note(w.Id, $"Costo registrado: {expense.Description} · {expense.Amount:0.00} {currency}.");
        return new { expense.Id, expense.Amount };
    });

    [HttpPost("warranties")]
    public Task<IActionResult> Warranty(OpenWarranty body) => Change("premium:warranty", body, async () => {
        var (w, o) = await Order(body.OrderId);
        if (o.Status != RepairOrderStatus.Delivered || w.HandedOverAtUtc == null) throw new DomainException("Las garantías se abren sobre reparaciones entregadas.");
        var q = await db.WorkflowQuotes.Where(x => x.ShopId == Shop && x.OrderId == w.Id && x.Status == "Accepted").OrderByDescending(x => x.Revision).FirstOrDefaultAsync();
        var supplier = Text(body.Supplier, "Proveedor", 0, 120);
        if (body.ReservationId is Guid reservationId) {
            var reservation = await Find<StockReservation>(reservationId);
            if (reservation.OrderId != w.Id || reservation.Status != "Consumed") throw new DomainException("Seleccioná un repuesto consumido en esta reparación.");
            supplier = (await Find<StockLot>(reservation.LotId)).Supplier;
        }
        var expires = w.HandedOverAtUtc.Value.AddDays(q?.WarrantyDays ?? 0);
        var claim = new WarrantyCase { ShopId = Shop, OrderId = w.Id, ReservationId = body.ReservationId, Problem = Text(body.Problem, "Falla", 5, 1000),
            FailureCode = Text(body.FailureCode, "Categoría de falla", 2, 80).ToUpperInvariant(), Supplier = supplier, Currency = q?.Currency ?? "ARS",
            WarrantyEndsAtUtc = expires, CoveredAtIntake = q?.WarrantyDays > 0 && DateTime.UtcNow <= expires };
        db.WarrantyCases.Add(claim); w.Version++; Note(w.Id, $"Garantía abierta: {claim.Problem}.");
        return new { claim.Id };
    });

    [HttpPost("warranties/{id:guid}"), Authorize(Policy = Policies.AdminOnly)]
    public Task<IActionResult> UpdateWarranty(Guid id, EditWarranty body) => Change($"premium:warranty:{id}", body, async () => {
        var claim = await Find<WarrantyCase>(id); var (w, _) = await Order(claim.OrderId); Version(claim, body.Version);
        if (body.Status is not ("Open" or "SupplierReview" or "Resolved" or "Rejected")) throw new DomainException("Estado de garantía inválido.");
        claim.Resolution = Text(body.Resolution, "Resolución", body.Status is "Resolved" or "Rejected" ? 5 : 0, 1000);
        claim.SupplierClaim = Text(body.SupplierClaim, "Reclamo al proveedor", 0, 500);
        claim.Cost = Money(body.Cost); claim.Recovered = Money(body.Recovered);
        if (claim.Recovered > claim.Cost) throw new DomainException("La recuperación no puede superar el costo registrado de la garantía.");
        claim.Status = body.Status; w.Version++;
        Note(w.Id, $"Garantía actualizada: {claim.Status}. Costo neto: {claim.Cost - claim.Recovered:0.00} {claim.Currency}. {claim.Resolution}");
        return new { claim.Id };
    });

    private static string Grade(string value) => value is "A" or "B" or "C" ? value : throw new DomainException("Elegí grado A, B o C.");
    private void RefurbLog(RefurbDevice device, string message) => db.RefurbEvents.Add(new RefurbEvent { ShopId = Shop, DeviceId = device.Id, Message = message });

    [HttpPost("refurbs"), Authorize(Policy = Policies.AdminOnly)]
    public Task<IActionResult> Acquire(AcquireDevice body) => Change("premium:refurb", body, async () => {
        await Find<PremiumBranch>(body.BranchId);
        if (body.Acquisition is not ("Buy" or "TradeIn")) throw new DomainException("Elegí compra o canje.");
        var identifier = Text(body.Identifier, "IMEI o serie", 5, 80).ToUpperInvariant();
        if (await Own<RefurbDevice>().AnyAsync(x => x.Identifier == identifier)) throw new DomainException("El IMEI/serie ya está registrado. Consultá su historial.");
        var device = new RefurbDevice { ShopId = Shop, BranchId = body.BranchId, Model = Text(body.Model, "Modelo", 3, 120), Identifier = identifier,
            Seller = Text(body.Seller, "Vendedor o titular del canje", 3, 120), Acquisition = body.Acquisition, Grade = Grade(body.Grade), Diagnosis = Text(body.Diagnosis, "Diagnóstico", 0, 1000),
            PurchasePrice = Money(body.PurchasePrice), TargetPrice = Money(body.TargetPrice), Currency = Currency(body.Currency) };
        db.RefurbDevices.Add(device); RefurbLog(device, $"Ingreso por {(body.Acquisition == "Buy" ? "compra" : "canje")}. Valor reconocido: {device.PurchasePrice:0.00} {device.Currency}.");
        return new { device.Id };
    });

    [HttpPost("refurbs/{id:guid}"), Authorize(Policy = Policies.AdminOnly)]
    public Task<IActionResult> UpdateRefurb(Guid id, EditRefurb body) => Change($"premium:refurb:{id}", body, async () => {
        var d = await Find<RefurbDevice>(id); Version(d, body.Version);
        if (d.Status == "Sold") throw new DomainException("El equipo ya fue vendido; su liquidación está cerrada.");
        var valid = body.Status == d.Status || (d.Status, body.Status) is ("Received", "Repairing") or ("Repairing", "Ready") or ("Ready", "Repairing") or ("Ready", "Sold");
        if (!valid) throw new DomainException("Pasá el equipo por reparación y control de calidad antes de venderlo.");
        d.Grade = Grade(body.Grade); d.Diagnosis = Text(body.Diagnosis, "Diagnóstico", 0, 1000); d.TargetPrice = Money(body.TargetPrice);
        string[] checks = ["Pantalla", "Batería", "Carga", "Cámaras", "Audio", "Conectividad"];
        var quality = body.QualityChecks ?? new Dictionary<string, bool>();
        if (quality.Keys.Any(x => !checks.Contains(x))) throw new DomainException("Control de calidad inválido.");
        if (body.Status is "Ready" or "Sold" && (d.Diagnosis.Length < 5 || checks.Any(x => !quality.GetValueOrDefault(x)))) throw new DomainException("Completá el diagnóstico y los seis controles de calidad.");
        d.QualityChecksJson = JsonSerializer.Serialize(quality, Json);
        if (body.Status == "Sold") {
            d.SalePrice = Money(body.SalePrice ?? 0);
            if (d.SalePrice <= 0) throw new DomainException("Ingresá el importe de venta.");
            d.Buyer = Text(body.Buyer, "Comprador", 3, 120); d.SoldAtUtc = DateTime.UtcNow;
        }
        d.Status = body.Status; RefurbLog(d, $"Estado: {d.Status}. {d.Diagnosis}" + (d.Status == "Sold" ? $" Venta: {d.SalePrice:0.00} {d.Currency}. Comprador: {d.Buyer}." : ""));
        return new { d.Id };
    });

    [HttpPost("refurbs/{id:guid}/expenses"), Authorize(Policy = Policies.AdminOnly)]
    public Task<IActionResult> AddRefurbCost(Guid id, RefurbCost body) => Change($"premium:refurb-cost:{id}", body, async () => {
        var d = await Find<RefurbDevice>(id); Version(d, body.Version);
        if (d.Status == "Sold") throw new DomainException("La venta está cerrada.");
        var expense = new RefurbExpense { ShopId = Shop, DeviceId = id, Description = Text(body.Description, "Descripción", 3, 300), Amount = Money(body.Amount) };
        db.RefurbExpenses.Add(expense); RefurbLog(d, $"Costo: {expense.Description} · {expense.Amount:0.00} {d.Currency}.");
        return new { expense.Id };
    });
}
