using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Common;
using RepairShop.Domain.Common;
using RepairShop.Domain.Inventory;
using RepairShop.Domain.Premium;
using RepairShop.Domain.RepairOrders;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Api.V2;

[ApiController, Authorize(Policy = Policies.StaffOnly), Route("api/v2/premium")]
public sealed partial class PremiumController(RepairShopDbContext db) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private Guid Shop => CurrentUser.GetShopId(User) is var id && id != Guid.Empty ? id : throw new UnauthorizedAccessException();
    private Guid Actor => CurrentUser.GetUserId(User);
    private static string Text(string? value, string label, int min = 1, int max = 200) {
        value = (value ?? "").Trim();
        if (value.Length < min || value.Length > max) throw new DomainException($"{label}: ingresá entre {min} y {max} caracteres.");
        return value;
    }
    private static decimal Money(decimal value) {
        if (value < 0 || value > 1000000000m) throw new DomainException("El importe debe estar entre 0 y 1.000.000.000.");
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
    private static string Currency(string? currency) {
        currency = (currency ?? "ARS").Trim().ToUpperInvariant();
        if (currency is not ("ARS" or "USD")) throw new DomainException("Elegí ARS o USD. No se convierten monedas automáticamente.");
        return currency;
    }
    private static int Qty(int quantity) {
        if (quantity is < 1 or > 10000) throw new DomainException("La cantidad debe estar entre 1 y 10.000.");
        return quantity;
    }
    private static void Version(IPremiumRecord entity, int version) {
        if (entity.Version != version) throw new WorkflowConflict("El registro cambió. Actualizá la pantalla y volvé a intentar.");
        entity.Version++;
    }
    private IQueryable<T> Own<T>() where T : class, IPremiumRecord => db.Set<T>().Where(x => x.ShopId == Shop);
    private async Task<T> Find<T>(Guid id) where T : class, IPremiumRecord =>
        await Own<T>().SingleOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Registro no encontrado en este taller.");
    private async Task<(WorkshopWorkflow W, RepairOrder O)> Order(Guid id, bool open = false) {
        var shop = Shop;
        var w = await db.Workflows.FromSqlInterpolated($"SELECT * FROM workflows WHERE \"Id\"={id} AND \"ShopId\"={shop} FOR UPDATE").SingleOrDefaultAsync()
            ?? throw new NotFoundException("Orden no encontrada en este taller.");
        var o = await db.RepairOrders.SingleAsync(x => x.Id == id && x.ShopId == shop);
        if (open && o.Status is RepairOrderStatus.Delivered or RepairOrderStatus.Cancelled) throw new DomainException("Esta orden está cerrada.");
        return (w, o);
    }
    private void Note(Guid order, string message) => db.RepairOrderNotes.Add(new RepairOrderNote(Shop, order, message, Actor, DateTime.UtcNow));
    private async Task<string> OrderCurrency(Guid id) => await db.WorkflowQuotes.Where(x => x.ShopId == Shop && x.OrderId == id).OrderByDescending(x => x.Revision).Select(x => x.Currency).FirstOrDefaultAsync() ?? "ARS";
    private async Task<IActionResult> Change(string operation, object body, Func<Task<object>> action) {
        var shop = Shop; var key = Request.Headers["Idempotency-Key"].ToString();
        if (key.Length is < 8 or > 100) throw new DomainException("Falta una clave válida de idempotencia.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body, Json))));
        await using var tx = await db.Database.BeginTransactionAsync();
        // Serialize stock, costs and business commands for this tenant. Order row locks coordinate with v2 workflow commands.
        var lockName = $"premium:{shop}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockName}, 0))");
        var receipt = await db.WorkflowRequests.SingleOrDefaultAsync(x => x.ShopId == shop && x.Operation == operation && x.Key == key);
        if (receipt is not null) {
            if (receipt.Hash != hash) throw new WorkflowConflict("Esta clave ya se usó con otros datos.");
            return Ok(new { data = JsonSerializer.Deserialize<JsonElement>(receipt.ResponseJson) });
        }
        var result = await action();
        db.WorkflowRequests.Add(new WorkflowRequest { ShopId = shop, Operation = operation, Key = key, Hash = hash, ResponseJson = JsonSerializer.Serialize(result, Json) });
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return Ok(new { data = result });
    }

    [HttpGet("workspace")]
    public async Task<IActionResult> Workspace() {
        var shop = Shop;
        var branches = await Own<PremiumBranch>().OrderBy(x => x.Name).ToListAsync();
        var offers = await Own<SupplierPrice>().OrderBy(x => x.Compatibility).ThenBy(x => x.UnitCost).ToListAsync();
        var items = await db.InventoryItems.Where(x => x.ShopId == shop).OrderBy(x => x.Name).ToListAsync();
        var lots = await Own<StockLot>().OrderBy(x => x.CreatedAtUtc).ToListAsync();
        var reservations = await Own<StockReservation>().OrderByDescending(x => x.CreatedAtUtc).ToListAsync();
        var movements = await Own<StockMovement>().OrderByDescending(x => x.CreatedAtUtc).Take(300).ToListAsync();
        var minimums = await Own<StockMinimum>().ToListAsync();
        var expenses = await Own<OrderExpense>().OrderByDescending(x => x.CreatedAtUtc).ToListAsync();
        var warranties = await Own<WarrantyCase>().OrderByDescending(x => x.CreatedAtUtc).ToListAsync();
        var refurbs = await Own<RefurbDevice>().OrderByDescending(x => x.CreatedAtUtc).ToListAsync();
        var refurbExpenses = await Own<RefurbExpense>().ToListAsync();
        var refurbEvents = await Own<RefurbEvent>().OrderByDescending(x => x.CreatedAtUtc).ToListAsync();
        var contracts = await Own<CompanyContract>().OrderBy(x => x.CompanyName).ToListAsync();
        var equipment = await Own<CompanyEquipment>().ToListAsync();
        var assignments = await Own<OrderBusiness>().ToListAsync();
        var settlements = await Own<ContractSettlement>().OrderByDescending(x => x.Period).ToListAsync();
        var orders = await (from w in db.Workflows join o in db.RepairOrders on w.Id equals o.Id where w.ShopId == shop && o.ShopId == shop
            orderby w.Number descending select new { w.Id, w.Number, w.CustomerName, w.DeviceLabel, w.Identifier, w.LaborCost, w.HandedOverAtUtc, w.IsDemo, o.CreatedAtUtc, Status = o.Status.ToString() }).ToListAsync();
        var quotes = await db.WorkflowQuotes.Where(x => x.ShopId == shop).OrderByDescending(x => x.Revision).ToListAsync();
        var payments = await db.RepairOrderPayments.Where(x => x.ShopId == shop).ToListAsync();
        var refunds = await db.WorkflowRefunds.Where(x => x.ShopId == shop).ToListAsync();
        var profitability = orders.Select(o => {
            var q = quotes.FirstOrDefault(x => x.OrderId == o.Id && x.Status == "Accepted");
            var currency = q?.Currency ?? "ARS";
            var consumed = reservations.Where(x => x.OrderId == o.Id && x.Status == "Consumed").ToList();
            var estimated = q == null ? 0 : (JsonSerializer.Deserialize<List<QuoteItem>>(q.LinesJson, Json) ?? []).Sum(x => x.UnitCost * x.Quantity);
            var parts = consumed.Count > 0 ? consumed.Sum(x => x.UnitCost * x.Quantity) : estimated;
            var laborRows = expenses.Where(x => x.OrderId == o.Id && x.Kind == "Labor").ToList();
            var labor = laborRows.Count > 0 ? laborRows.Sum(x => x.Amount) : o.LaborCost;
            var other = expenses.Where(x => x.OrderId == o.Id && x.Kind != "Labor").Sum(x => x.Amount);
            var warranty = warranties.Where(x => x.OrderId == o.Id).Sum(x => x.Cost - x.Recovered);
            var returned = refunds.Where(x => x.OrderId == o.Id).Sum(x => x.Amount);
            var revenue = (q?.Total ?? 0) - returned;
            var net = revenue - parts - labor - other - warranty;
            return new { OrderId = o.Id, o.Number, o.CustomerName, o.DeviceLabel, o.Status, Currency = currency, Revenue = revenue,
                Collected = payments.Where(x => x.RepairOrderId == o.Id).Sum(x => x.Amount) - returned,
                Parts = parts, Labor = labor, Other = other, Warranty = warranty, Net = net,
                Margin = revenue > 0 ? decimal.Round(net / revenue * 100, 1) : 0, HasQuote = q != null,
                PartsSource = consumed.Count > 0 ? "Consumos registrados" : "Costo estimado del presupuesto",
                LaborSource = laborRows.Count > 0 ? "Horas registradas" : "Costo manual de la orden",
                Minutes = laborRows.Sum(x => x.Minutes), SuggestedPrice = decimal.Round((parts + labor + other + warranty) / 0.7m, 2) };
        }).ToList();
        return Ok(new { data = new { branches, offers, items, lots, reservations, movements, minimums, expenses, warranties, refurbs, refurbExpenses, refurbEvents, contracts, equipment, assignments, settlements, orders, profitability } });
    }
}
