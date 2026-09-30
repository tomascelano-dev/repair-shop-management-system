using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepairShop.Api.V2;
using RepairShop.Domain.Common;
using RepairShop.Domain.Inventory;
using RepairShop.Domain.Premium;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Saas;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Api.Saas;

public sealed record SaleLine(string Description, decimal Quantity, decimal UnitPrice, Guid? ItemId, Guid? ServiceId, List<LotAllocation>? Lots);
public sealed record LotAllocation(Guid LotId, int Quantity, decimal UnitCost);
public sealed record OpenCashRequest(decimal OpeningCash, decimal OpeningCashUsd, string? Notes);
public sealed record CloseCashRequest(int Version, decimal CountedCash, decimal CountedCashUsd, string? Notes);
public sealed record CashMovementRequest(string Kind, string Method, decimal Amount, string Currency, string Description);
public sealed record SaleLineRequest(string Description, decimal Quantity, decimal UnitPrice, Guid? ItemId, Guid? ServiceId);
public sealed record SaleRequest(Guid? CustomerId, string? CustomerName, List<SaleLineRequest> Lines, decimal Discount, string Currency, string Method);
public sealed record VoidSaleRequest(string Reason);
public sealed record AccountPaymentRequest(decimal Amount, string Currency, string Method, string? Description);
public sealed record AccountChargeRequest(decimal Amount, string Currency, string Description);
public sealed record OrderToAccountRequest(int Version);

[ApiController, Authorize, Route("api/saas")]
public sealed class CashController(RepairShopDbContext db) : SaasController(db)
{
    private async Task<CashSession?> OpenSession() =>
        await Db.CashSessions.FromSqlInterpolated($"SELECT * FROM saas_cash_session WHERE \"ShopId\"={Shop} AND \"Status\"='Open' ORDER BY \"CreatedAtUtc\" LIMIT 1 FOR UPDATE").FirstOrDefaultAsync();

    private async Task<CashSession> RequireSession() => await OpenSession() ?? throw new DomainException("Abrí la caja antes de registrar movimientos.");

    private static object Summary(IEnumerable<CashMovement> movements) =>
        movements.GroupBy(m => new { m.Currency, m.Method }).Select(g => new { g.Key.Currency, g.Key.Method, Total = g.Sum(x => x.Amount), Count = g.Count() }).OrderBy(x => x.Currency).ThenBy(x => x.Method).ToList();

    [HttpGet("cash")]
    public async Task<IActionResult> Current()
    {
        var session = await Own<CashSession>().Where(x => x.Status == "Open").OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync();
        var movements = session is null ? [] : await Own<CashMovement>().Where(x => x.SessionId == session.Id).OrderByDescending(x => x.CreatedAtUtc).ToListAsync();
        var history = await Own<CashSession>().Where(x => x.Status == "Closed").OrderByDescending(x => x.ClosedAtUtc).Take(30).ToListAsync();
        var users = await Db.Users.Where(x => x.ShopId == Shop).Select(x => new { x.Id, x.DisplayName }).ToListAsync();
        object? expected = session is null ? null : new
        {
            Cash = session.OpeningCash + movements.Where(m => m.Method == "Cash" && m.Currency == "ARS").Sum(m => m.Amount),
            CashUsd = session.OpeningCashUsd + movements.Where(m => m.Method == "Cash" && m.Currency == "USD").Sum(m => m.Amount),
        };
        return Ok(new { data = new { session, movements, summary = Summary(movements), expected, history, users } });
    }

    [HttpPost("cash/open")]
    public Task<IActionResult> Open(OpenCashRequest b) => Change("cash:open", b, async () =>
    {
        if (await OpenSession() is not null) throw new DomainException("Ya hay una caja abierta.");
        var session = new CashSession { ShopId = Shop, OpenedBy = Actor, OpeningCash = Saas.Money(b.OpeningCash), OpeningCashUsd = Saas.Money(b.OpeningCashUsd), Notes = Saas.Text(b.Notes, "Notas", 0, 500) };
        Db.CashSessions.Add(session);
        return new { session.Id };
    });

    [HttpPost("cash/close")]
    public Task<IActionResult> Close(CloseCashRequest b) => Change("cash:close", b, async () =>
    {
        var session = await RequireSession();
        CheckVersion(session, b.Version);
        var movements = await Own<CashMovement>().Where(x => x.SessionId == session.Id).ToListAsync();
        session.ExpectedCash = session.OpeningCash + movements.Where(m => m.Method == "Cash" && m.Currency == "ARS").Sum(m => m.Amount);
        session.ExpectedCashUsd = session.OpeningCashUsd + movements.Where(m => m.Method == "Cash" && m.Currency == "USD").Sum(m => m.Amount);
        session.CountedCash = Saas.Money(b.CountedCash); session.CountedCashUsd = Saas.Money(b.CountedCashUsd);
        session.SummaryJson = JsonSerializer.Serialize(Summary(movements), Saas.Json);
        session.Notes = string.Join(" · ", new[] { session.Notes, Saas.Text(b.Notes, "Notas", 0, 500) }.Where(x => x.Length > 0));
        session.Status = "Closed"; session.ClosedAtUtc = DateTime.UtcNow; session.ClosedBy = Actor;
        var diff = session.CountedCash - session.ExpectedCash;
        if (diff != 0) Db.ShopAlerts.Add(new ShopAlert { ShopId = Shop, Kind = "Cash", Message = $"Cierre de caja con diferencia de ARS {diff:N2}.", Link = "/cash" });
        return new { session.Id, session.ExpectedCash, session.ExpectedCashUsd, Difference = diff, DifferenceUsd = session.CountedCashUsd - session.ExpectedCashUsd };
    });

    [HttpPost("cash/movements")]
    public Task<IActionResult> Movement(CashMovementRequest b) => Change("cash:movement", b, async () =>
    {
        var session = await RequireSession();
        if (b.Kind is not ("Income" or "Expense")) throw new DomainException("Elegí ingreso o egreso.");
        if (b.Kind == "Expense" && !IsAdmin) throw new ForbiddenException("Solo un administrador registra egresos de caja.");
        var amount = Saas.Money(b.Amount);
        if (amount <= 0) throw new DomainException("El importe debe ser mayor a cero.");
        var method = Saas.Method(b.Method);
        if (method == "Account") throw new DomainException("Usá cuentas corrientes para movimientos a cuenta.");
        var movement = new CashMovement { ShopId = Shop, SessionId = session.Id, Kind = b.Kind, Method = method, Amount = b.Kind == "Expense" ? -amount : amount, Currency = Saas.Currency(b.Currency), Description = Saas.Text(b.Description, "Descripción", 3, 300), ActorId = Actor };
        Db.CashMovements.Add(movement);
        return new { movement.Id };
    });

    [HttpGet("sales")]
    public async Task<IActionResult> Sales([FromQuery] int take = 100) =>
        Ok(new { data = (await Own<CounterSale>().OrderByDescending(x => x.Number).Take(Math.Clamp(take, 1, 500)).ToListAsync())
            .Select(s => new { s.Id, s.Version, s.Number, s.CreatedAtUtc, s.CustomerId, s.CustomerName, Lines = JsonSerializer.Deserialize<List<SaleLine>>(s.LinesJson, Saas.Json), s.Subtotal, s.Discount, s.Total, s.Currency, s.Method, s.Status, s.VoidReason, s.InvoiceId }) });

    [HttpPost("sales")]
    public Task<IActionResult> Sell(SaleRequest b) => Change("sale", b, async () =>
    {
        var session = await RequireSession();
        var currency = Saas.Currency(b.Currency);
        var method = Saas.Method(b.Method);
        if (b.Lines is not { Count: > 0 and <= 60 }) throw new DomainException("Agregá entre 1 y 60 productos o servicios.");
        Guid? customerId = null; var customerName = Saas.Text(b.CustomerName, "Cliente", 0, 120);
        if (b.CustomerId is Guid cid)
        {
            var customer = await Db.Customers.SingleOrDefaultAsync(x => x.Id == cid && x.ShopId == Shop) ?? throw new DomainException("Cliente no disponible.");
            customerId = customer.Id; customerName = customer.FullName;
        }
        if (method == "Account" && customerId is null) throw new DomainException("Para vender a cuenta corriente elegí un cliente.");
        var lines = new List<SaleLine>();
        foreach (var l in b.Lines)
        {
            if (l.Quantity <= 0 || l.Quantity > 10000) throw new DomainException("Cantidad inválida.");
            var price = Saas.Money(l.UnitPrice, "El precio");
            var description = Saas.Text(l.Description, "Descripción", 2, 200);
            List<LotAllocation>? allocations = null;
            if (l.ItemId is Guid itemId)
            {
                if (l.Quantity != decimal.Truncate(l.Quantity)) throw new DomainException("Los repuestos se venden por unidad.");
                allocations = await Allocate(itemId, (int)l.Quantity, currency, description);
            }
            if (l.ServiceId is Guid sid && !await Own<ServiceCatalogItem>().AnyAsync(x => x.Id == sid)) throw new DomainException("Servicio no encontrado.");
            lines.Add(new SaleLine(description, l.Quantity, price, l.ItemId, l.ServiceId, allocations));
        }
        var subtotal = lines.Sum(l => decimal.Round(l.Quantity * l.UnitPrice, 2, MidpointRounding.AwayFromZero));
        var discount = Saas.Money(b.Discount, "El descuento");
        if (discount > subtotal) throw new DomainException("El descuento no puede superar el subtotal.");
        if (discount > 0 && discount > subtotal * 0.3m && !IsAdmin) throw new ForbiddenException("Descuentos mayores al 30 % requieren un administrador.");
        var number = (await Own<CounterSale>().MaxAsync(x => (int?)x.Number) ?? 0) + 1;
        var sale = new CounterSale { ShopId = Shop, Number = number, SessionId = session.Id, CustomerId = customerId, CustomerName = customerName.Length > 0 ? customerName : "Consumidor final",
            LinesJson = JsonSerializer.Serialize(lines, Saas.Json), Subtotal = subtotal, Discount = discount, Total = subtotal - discount, Currency = currency, Method = method, ActorId = Actor };
        Db.CounterSales.Add(sale);
        if (method == "Account") Db.AccountEntries.Add(new AccountEntry { ShopId = Shop, CustomerId = customerId!.Value, Kind = "Charge", Amount = sale.Total, Currency = currency, Description = $"Venta mostrador #{number}", ReferenceId = sale.Id, ActorId = Actor });
        else Db.CashMovements.Add(new CashMovement { ShopId = Shop, SessionId = session.Id, Kind = "Sale", Method = method, Amount = sale.Total, Currency = currency, Description = $"Venta mostrador #{number}", ReferenceId = sale.Id, ActorId = Actor });
        Webhooks.Enqueue(Db, Shop, "sale.created", new { sale.Id, sale.Number, sale.Total, sale.Currency, sale.Method });
        return new { sale.Id, sale.Number, sale.Total };
    });

    // FIFO over the shop's lots for this item; reserved units stay untouched.
    private async Task<List<LotAllocation>> Allocate(Guid itemId, int quantity, string currency, string description)
    {
        var item = await Db.InventoryItems.SingleOrDefaultAsync(x => x.Id == itemId && x.ShopId == Shop) ?? throw new DomainException("Repuesto no encontrado.");
        var lots = await Own<StockLot>().Where(x => x.ItemId == itemId && x.Quantity > 0).OrderBy(x => x.CreatedAtUtc).ToListAsync();
        var lotIds = lots.Select(l => l.Id).ToList();
        var reserved = await Own<StockReservation>().Where(x => x.Status == "Reserved" && lotIds.Contains(x.LotId)).GroupBy(x => x.LotId).Select(g => new { g.Key, Qty = g.Sum(x => x.Quantity) }).ToDictionaryAsync(x => x.Key, x => x.Qty);
        var result = new List<LotAllocation>(); var pending = quantity;
        foreach (var lot in lots)
        {
            var free = lot.Quantity - reserved.GetValueOrDefault(lot.Id);
            if (free <= 0) continue;
            var take = Math.Min(free, pending);
            lot.Quantity -= take; lot.Version++;
            Db.StockMovements.Add(new StockMovement { ShopId = Shop, LotId = lot.Id, Kind = "Sale", Quantity = -take, Reason = $"Venta de mostrador: {description}", ActorId = Actor });
            result.Add(new LotAllocation(lot.Id, take, lot.UnitCost));
            pending -= take;
            if (pending == 0) break;
        }
        if (pending > 0) throw new DomainException($"No hay stock disponible suficiente de {item.Name}.");
        item.ApplyDelta(-quantity, DateTime.UtcNow);
        Db.InventoryAdjustments.Add(new InventoryAdjustment(Shop, item.Id, InventoryAdjustmentType.Sale, -quantity, "Venta de mostrador", null, Actor, DateTime.UtcNow));
        return result;
    }

    [HttpPost("sales/{id:guid}/void")]
    public Task<IActionResult> Void(Guid id, VoidSaleRequest b) => Change($"sale:void:{id}", b, async () =>
    {
        RequireAdmin();
        var sale = await Find<CounterSale>(id);
        if (sale.Status != "Completed") throw new DomainException("La venta ya está anulada.");
        if (sale.InvoiceId is not null) throw new DomainException("La venta tiene un comprobante. Emití una nota de crédito antes de anularla.");
        var reason = Saas.Text(b.Reason, "Motivo", 3, 300);
        foreach (var line in JsonSerializer.Deserialize<List<SaleLine>>(sale.LinesJson, Saas.Json) ?? [])
        {
            if (line.ItemId is not Guid itemId || line.Lots is null) continue;
            foreach (var a in line.Lots)
            {
                var lot = await Find<StockLot>(a.LotId);
                lot.Quantity += a.Quantity; lot.Version++;
                Db.StockMovements.Add(new StockMovement { ShopId = Shop, LotId = lot.Id, Kind = "Adjustment", Quantity = a.Quantity, Reason = $"Anulación venta #{sale.Number}: {reason}", ActorId = Actor });
            }
            var item = await Db.InventoryItems.SingleAsync(x => x.Id == itemId && x.ShopId == Shop);
            item.ApplyDelta((int)line.Quantity, DateTime.UtcNow);
        }
        if (sale.Method == "Account")
            Db.AccountEntries.Add(new AccountEntry { ShopId = Shop, CustomerId = sale.CustomerId!.Value, Kind = "Payment", Amount = sale.Total, Currency = sale.Currency, Description = $"Anulación venta #{sale.Number}", ReferenceId = sale.Id, ActorId = Actor });
        else
        {
            var session = await RequireSession();
            Db.CashMovements.Add(new CashMovement { ShopId = Shop, SessionId = session.Id, Kind = "Void", Method = sale.Method, Amount = -sale.Total, Currency = sale.Currency, Description = $"Anulación venta #{sale.Number}: {reason}", ReferenceId = sale.Id, ActorId = Actor });
        }
        sale.Status = "Voided"; sale.VoidReason = reason; sale.Version++;
        return new { sale.Id, sale.Status };
    });

    [HttpGet("accounts")]
    public async Task<IActionResult> Accounts()
    {
        var balances = await Own<AccountEntry>().GroupBy(x => new { x.CustomerId, x.Currency })
            .Select(g => new { g.Key.CustomerId, g.Key.Currency, Balance = g.Sum(x => x.Kind == "Charge" ? x.Amount : -x.Amount), Last = g.Max(x => x.CreatedAtUtc) }).ToListAsync();
        var ids = balances.Select(x => x.CustomerId).Distinct().ToList();
        var customers = await Db.Customers.Where(x => x.ShopId == Shop && ids.Contains(x.Id)).Select(x => new { x.Id, x.FullName, x.Phone }).ToDictionaryAsync(x => x.Id);
        return Ok(new { data = balances.Select(b => new { b.CustomerId, customers.GetValueOrDefault(b.CustomerId)?.FullName, customers.GetValueOrDefault(b.CustomerId)?.Phone, b.Currency, b.Balance, b.Last }).OrderByDescending(x => x.Balance) });
    }

    [HttpGet("accounts/{customerId:guid}")]
    public async Task<IActionResult> Account(Guid customerId)
    {
        var customer = await Db.Customers.Where(x => x.ShopId == Shop && x.Id == customerId).Select(x => new { x.Id, x.FullName, x.Phone }).SingleOrDefaultAsync()
            ?? throw new RepairShop.Application.Common.NotFoundException("Cliente no encontrado.");
        var entries = await Own<AccountEntry>().Where(x => x.CustomerId == customerId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync();
        return Ok(new { data = new { customer, entries, balances = entries.GroupBy(x => x.Currency).Select(g => new { Currency = g.Key, Balance = g.Sum(x => x.Kind == "Charge" ? x.Amount : -x.Amount) }) } });
    }

    [HttpPost("accounts/{customerId:guid}/payments")]
    public Task<IActionResult> AccountPayment(Guid customerId, AccountPaymentRequest b) => Change($"account:payment:{customerId}", b, async () =>
    {
        if (!await Db.Customers.AnyAsync(x => x.ShopId == Shop && x.Id == customerId)) throw new RepairShop.Application.Common.NotFoundException("Cliente no encontrado.");
        var session = await RequireSession();
        var currency = Saas.Currency(b.Currency); var method = Saas.Method(b.Method);
        if (method == "Account") throw new DomainException("Elegí cómo paga el cliente.");
        var amount = Saas.Money(b.Amount);
        var balance = await Own<AccountEntry>().Where(x => x.CustomerId == customerId && x.Currency == currency).SumAsync(x => x.Kind == "Charge" ? x.Amount : -x.Amount);
        if (amount <= 0 || amount > balance) throw new DomainException("El pago debe ser mayor a cero y no superar el saldo de la cuenta.");
        var entry = new AccountEntry { ShopId = Shop, CustomerId = customerId, Kind = "Payment", Amount = amount, Currency = currency, Description = Saas.Text(b.Description, "Detalle", 0, 200) is { Length: > 0 } d ? d : "Pago a cuenta", ActorId = Actor };
        Db.AccountEntries.Add(entry);
        Db.CashMovements.Add(new CashMovement { ShopId = Shop, SessionId = session.Id, Kind = "AccountPayment", Method = method, Amount = amount, Currency = currency, Description = "Pago de cuenta corriente", ReferenceId = entry.Id, ActorId = Actor });
        return new { entry.Id };
    });

    [HttpPost("accounts/{customerId:guid}/charges")]
    public Task<IActionResult> AccountCharge(Guid customerId, AccountChargeRequest b) => Change($"account:charge:{customerId}", b, async () =>
    {
        RequireAdmin();
        if (!await Db.Customers.AnyAsync(x => x.ShopId == Shop && x.Id == customerId)) throw new RepairShop.Application.Common.NotFoundException("Cliente no encontrado.");
        var amount = Saas.Money(b.Amount);
        if (amount <= 0) throw new DomainException("El importe debe ser mayor a cero.");
        var entry = new AccountEntry { ShopId = Shop, CustomerId = customerId, Kind = "Charge", Amount = amount, Currency = Saas.Currency(b.Currency), Description = Saas.Text(b.Description, "Detalle", 3, 200), ActorId = Actor };
        Db.AccountEntries.Add(entry);
        return new { entry.Id };
    });

    // Moves the unpaid balance of an order to the customer's account so it can be handed over without debt.
    [HttpPost("accounts/orders/{orderId:guid}")]
    public Task<IActionResult> OrderToAccount(Guid orderId, OrderToAccountRequest b) => Change($"account:order:{orderId}", b, async () =>
    {
        RequireAdmin();
        var (w, o) = await LockOrder(orderId);
        if (w.Version != b.Version) throw new WorkflowConflict("La orden cambió. Actualizá la pantalla e intentá nuevamente.");
        var quote = await Db.WorkflowQuotes.Where(x => x.ShopId == Shop && x.OrderId == orderId).OrderByDescending(x => x.Revision).FirstOrDefaultAsync();
        if (quote?.Status != "Accepted") throw new DomainException("La orden necesita un presupuesto aprobado.");
        var paid = await Db.RepairOrderPayments.Where(x => x.ShopId == Shop && x.RepairOrderId == orderId).SumAsync(x => x.Amount)
            - await Db.WorkflowRefunds.Where(x => x.ShopId == Shop && x.OrderId == orderId).SumAsync(x => x.Amount);
        var due = quote.Total - paid;
        if (due <= 0) throw new DomainException("La orden no tiene saldo pendiente.");
        var payment = new RepairOrderPayment(Shop, orderId, due, quote.Currency, PaymentMethod.Other, "Cuenta corriente", Actor, DateTime.UtcNow);
        Db.RepairOrderPayments.Add(payment);
        Db.AccountEntries.Add(new AccountEntry { ShopId = Shop, CustomerId = o.CustomerId, Kind = "Charge", Amount = due, Currency = quote.Currency, Description = $"Saldo de {SaasEvents.Code(w.Number)}", ReferenceId = orderId, ActorId = Actor });
        w.Version++;
        Note(orderId, $"Saldo de {quote.Currency} {due:N2} pasado a la cuenta corriente del cliente.");
        return new { w.Version, due };
    });
}
