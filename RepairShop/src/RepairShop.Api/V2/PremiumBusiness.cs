using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepairShop.Api.Security;
using RepairShop.Domain.Common;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Devices;
using RepairShop.Domain.Premium;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Api.V2;

public sealed record BranchInput(string Name, string Address);
public sealed record AssignBranch(Guid OrderId, Guid BranchId);
public sealed record ContractInput(string CompanyName, string Contact, string Phone, decimal MonthlyFee, decimal ExtraOrderRate, int IncludedOrders, int SlaHours, string Currency, string StartsOn, string EndsOn, string Terms);
public sealed record ContractStatus(int Version, string Status);
public sealed record EquipmentInput(Guid ContractId, string Brand, string Model, string Identifier);
public sealed record BusinessIntake(Guid ContractId, Guid BranchId, List<Guid> EquipmentIds, string Issue, string Reference);
public sealed record SettlementInput(Guid ContractId, string Period);
public sealed record PaySettlement(int Version);

public sealed partial class PremiumController
{
    [HttpPost("branches"), Authorize(Policy = Policies.AdminOnly)]
    public Task<IActionResult> Branch(BranchInput body) => Change("premium:branch", body, async () => {
        var name = Text(body.Name, "Nombre de sucursal", 2, 100);
        if ((await Own<PremiumBranch>().ToListAsync()).Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) throw new DomainException("La sucursal ya existe.");
        var branch = new PremiumBranch { ShopId = Shop, Name = name, Address = Text(body.Address, "Dirección", 0, 200) }; db.PremiumBranches.Add(branch);
        return new { branch.Id };
    });

    [HttpPost("branches/assign"), Authorize(Policy = Policies.AdminOnly)]
    public Task<IActionResult> Assign(AssignBranch body) => Change("premium:assign", body, async () => {
        var (w, _) = await Order(body.OrderId, true); var branch = await Find<PremiumBranch>(body.BranchId);
        var assignment = await Own<OrderBusiness>().SingleOrDefaultAsync(x => x.OrderId == w.Id);
        if (assignment?.BranchId != branch.Id && await Own<StockReservation>().AnyAsync(x => x.OrderId == w.Id && (x.Status == "Reserved" || x.Status == "Consumed"))) throw new DomainException("La orden ya tiene repuestos reservados o consumidos. Mantené su sucursal de trabajo.");
        if (assignment is null) { assignment = new OrderBusiness { ShopId = Shop, OrderId = w.Id }; db.OrderBusinesses.Add(assignment); }
        assignment.BranchId = branch.Id; assignment.Version++; w.Version++; Note(w.Id, $"Sucursal asignada: {branch.Name}.");
        return new { assignment.Id };
    });

    [HttpPost("contracts"), Authorize(Policy = Policies.AdminOnly)]
    public Task<IActionResult> Contract(ContractInput body) => Change("premium:contract", body, async () => {
        if (body.IncludedOrders is < 0 or > 10000 || body.SlaHours is < 1 or > 8760) throw new DomainException("Revisá la cantidad incluida y el plazo de atención.");
        if (!DateTime.TryParseExact(body.StartsOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
            !DateTime.TryParseExact(body.EndsOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end) || end < start) throw new DomainException("Revisá las fechas del contrato.");
        var name = Text(body.CompanyName, "Empresa", 3, 120);
        if ((await Own<CompanyContract>().ToListAsync()).Any(x => x.CompanyName.Equals(name, StringComparison.OrdinalIgnoreCase) && x.Status == "Active")) throw new DomainException("La empresa ya tiene un contrato activo.");
        var customer = new Customer(Shop, name, Text(body.Phone, "Teléfono de contacto", 6, 40), "Cliente empresarial", DateTime.UtcNow); db.Customers.Add(customer);
        var contract = new CompanyContract { ShopId = Shop, CustomerId = customer.Id, CompanyName = name, Phone = customer.Phone, Contact = Text(body.Contact, "Contacto", 3, 120),
            MonthlyFee = Money(body.MonthlyFee), ExtraOrderRate = Money(body.ExtraOrderRate), IncludedOrders = body.IncludedOrders, SlaHours = body.SlaHours, Currency = Currency(body.Currency),
            StartsAtUtc = DateTime.SpecifyKind(start.AddHours(3), DateTimeKind.Utc), EndsAtUtc = DateTime.SpecifyKind(end.AddDays(1).AddHours(3), DateTimeKind.Utc), Terms = Text(body.Terms, "Condiciones", 0, 2000) };
        db.CompanyContracts.Add(contract); return new { contract.Id };
    });

    [HttpPost("contracts/{id:guid}/status"), Authorize(Policy = Policies.AdminOnly)]
    public Task<IActionResult> SetContractStatus(Guid id, ContractStatus body) => Change($"premium:contract-status:{id}", body, async () => {
        var c = await Find<CompanyContract>(id); Version(c, body.Version);
        if (body.Status is not ("Active" or "Paused" or "Closed")) throw new DomainException("Estado de contrato inválido.");
        c.Status = body.Status; return new { c.Id };
    });

    [HttpPost("equipment"), Authorize(Policy = Policies.AdminOnly)]
    public Task<IActionResult> Equipment(EquipmentInput body) => Change("premium:equipment", body, async () => {
        var c = await Find<CompanyContract>(body.ContractId); var identifier = Text(body.Identifier, "Serie o identificador", 3, 80).ToUpperInvariant();
        if (await Own<CompanyEquipment>().AnyAsync(x => x.ContractId == c.Id && x.Identifier == identifier)) throw new DomainException("El equipo ya está registrado en esta empresa.");
        var device = new Device(Shop, c.CustomerId, Text(body.Brand, "Marca", 2, 60), Text(body.Model, "Modelo", 2, 60), null, identifier, "Equipo de contrato empresarial", DateTime.UtcNow); db.Devices.Add(device);
        var equipment = new CompanyEquipment { ShopId = Shop, ContractId = c.Id, DeviceId = device.Id, Identifier = identifier, Label = $"{device.Brand} {device.Model}" }; db.CompanyEquipments.Add(equipment);
        return new { equipment.Id };
    });

    [HttpPost("business/intake")]
    public Task<IActionResult> Batch(BusinessIntake body) => Change("premium:batch", body, async () => {
        var c = await Find<CompanyContract>(body.ContractId); var branch = await Find<PremiumBranch>(body.BranchId);
        var now = DateTime.UtcNow;
        if (c.Status != "Active" || now < c.StartsAtUtc || now >= c.EndsAtUtc) throw new DomainException("El contrato debe estar activo y vigente.");
        if (body.EquipmentIds is null || body.EquipmentIds.Count is < 1 or > 30 || body.EquipmentIds.Distinct().Count() != body.EquipmentIds.Count) throw new DomainException("Seleccioná entre 1 y 30 equipos distintos.");
        var issue = Text(body.Issue, "Trabajo solicitado", 5, 500); var reference = Text(body.Reference, "Referencia del lote", 2, 100); var ids = new List<Guid>();
        foreach (var id in body.EquipmentIds) {
            var e = await Find<CompanyEquipment>(id);
            if (e.ContractId != c.Id) throw new DomainException("Todos los equipos deben pertenecer a esta empresa.");
            var active = await (from a in db.OrderBusinesses join current in db.RepairOrders on a.OrderId equals current.Id
                where a.ShopId == Shop && current.ShopId == Shop && a.EquipmentId == id && current.Status != RepairOrderStatus.Delivered && current.Status != RepairOrderStatus.Cancelled select a).AnyAsync();
            if (active) throw new DomainException($"{e.Identifier} ya tiene una orden abierta.");
            var o = new RepairOrder(Shop, c.CustomerId, e.DeviceId, issue, $"Lote empresarial {reference}", now); db.RepairOrders.Add(o);
            db.Workflows.Add(new WorkshopWorkflow { Id = o.Id, ShopId = Shop, CustomerName = c.CompanyName, CustomerPhone = c.Phone, DeviceLabel = e.Label, Identifier = e.Identifier,
                Condition = "Ingreso empresarial. Completar revisión inicial.", Priority = "Normal" });
            db.OrderBusinesses.Add(new OrderBusiness { ShopId = Shop, OrderId = o.Id, BranchId = branch.Id, ContractId = c.Id, EquipmentId = e.Id, BatchReference = reference, DueAtUtc = now.AddHours(c.SlaHours) });
            Note(o.Id, $"Ingreso por contrato {c.CompanyName}. Lote: {reference}. Plazo: {c.SlaHours} horas corridas."); ids.Add(o.Id);
        }
        return new { orderIds = ids };
    });

    [HttpPost("settlements"), Authorize(Policy = Policies.AdminOnly)]
    public Task<IActionResult> Settlement(SettlementInput body) => Change("premium:settlement", body, async () => {
        var c = await Find<CompanyContract>(body.ContractId);
        if (!DateTime.TryParseExact(body.Period, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month)) throw new DomainException("El período debe ser AAAA-MM.");
        var start = DateTime.SpecifyKind(month.AddHours(3), DateTimeKind.Utc); var end = start.AddMonths(1);
        if (end > DateTime.UtcNow) throw new DomainException("La liquidación se genera al terminar el mes. El mes actual todavía no puede liquidarse.");
        if (start >= c.EndsAtUtc || end <= c.StartsAtUtc) throw new DomainException("El período no pertenece a la vigencia del contrato.");
        if (await Own<ContractSettlement>().AnyAsync(x => x.ContractId == c.Id && x.Period == body.Period)) throw new DomainException("Ese período ya está liquidado.");
        var ids = await (from a in db.OrderBusinesses join w in db.Workflows on a.OrderId equals w.Id join o in db.RepairOrders on w.Id equals o.Id
            where a.ShopId == Shop && w.ShopId == Shop && o.ShopId == Shop && a.ContractId == c.Id && o.Status == RepairOrderStatus.Delivered && w.HandedOverAtUtc >= start && w.HandedOverAtUtc < end select w.Id).ToListAsync();
        var extras = Math.Max(0, ids.Count - c.IncludedOrders);
        var settlement = new ContractSettlement { ShopId = Shop, ContractId = c.Id, Period = body.Period, CompletedOrders = ids.Count, ExtraOrders = extras, MonthlyFee = c.MonthlyFee, ExtraRate = c.ExtraOrderRate,
            Total = Money(c.MonthlyFee + extras * c.ExtraOrderRate), Currency = c.Currency, OrderIdsJson = JsonSerializer.Serialize(ids) };
        db.ContractSettlements.Add(settlement); return new { settlement.Id, settlement.Total };
    });

    [HttpPost("settlements/{id:guid}/paid"), Authorize(Policy = Policies.AdminOnly)]
    public Task<IActionResult> MarkPaid(Guid id, PaySettlement body) => Change($"premium:settlement-pay:{id}", body, async () => {
        var s = await Find<ContractSettlement>(id); Version(s, body.Version);
        if (s.Status == "Paid") throw new DomainException("La liquidación ya fue marcada como cobrada.");
        s.Status = "Paid"; s.PaidAtUtc = DateTime.UtcNow; return new { s.Id };
    });
}
