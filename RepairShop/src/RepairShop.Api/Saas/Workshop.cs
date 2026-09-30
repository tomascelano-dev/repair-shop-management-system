using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepairShop.Api.V2;
using RepairShop.Domain.Common;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Saas;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Api.Saas;

public sealed record AssignRequest(Guid TechnicianId, string Mode, string? FieldAddress, DateTime? ScheduledAtUtc);
public sealed record TimerRequest(int Version, string Action, string? Report);
public sealed record DiagramMark(decimal X, decimal Y, string Kind, string Note);
public sealed record DiagramRequest(int Version, string Template, List<DiagramMark> Marks);
public sealed record CatalogRequest(int Version, string Code, string Name, string? Category, decimal Price, decimal EstimatedCost, string Currency, int EstimatedMinutes, int WarrantyDays, bool Active);
public sealed record BulkPriceRequest(string? Category, string Currency, decimal Percent, string Rounding);
public sealed record CatalogImportRequest(string Csv);

[ApiController, Authorize, Route("api/saas/tech")]
public sealed class TechnicianController(RepairShopDbContext db) : SaasController(db)
{
    // Work queue for the technician app: own assignments first, then unassigned open orders (admins see everyone's).
    [HttpGet]
    public async Task<IActionResult> Queue()
    {
        var shop = Shop;
        var assignments = await Own<OrderAssignment>().Where(x => x.State != "Finished" && (IsAdmin || x.TechnicianId == Actor)).ToListAsync();
        var ids = assignments.Select(x => x.OrderId).ToList();
        var orders = await (from o in Db.RepairOrders join w in Db.Workflows on o.Id equals w.Id
                            where o.ShopId == shop && w.ShopId == shop && o.Status != RepairOrderStatus.Delivered && o.Status != RepairOrderStatus.Cancelled
                            orderby w.Priority descending, o.CreatedAtUtc
                            select new { o.Id, w.Number, w.CustomerName, w.CustomerPhone, w.DeviceLabel, o.IssueDescription, Status = o.Status.ToString(), w.Priority, w.Version, o.CreatedAtUtc }).Take(300).ToListAsync();
        var techs = await Db.Users.Where(x => x.ShopId == shop).Select(x => new { x.Id, x.DisplayName, Role = x.Role.ToString() }).ToListAsync();
        var field = await Own<Appointment>().Where(x => x.Kind == "Field" && (x.Status == "Booked" || x.Status == "Confirmed") && (IsAdmin || x.TechnicianId == Actor) && x.StartsAtUtc > DateTime.UtcNow.AddHours(-12))
            .OrderBy(x => x.StartsAtUtc).Take(50).ToListAsync();
        return Ok(new { data = new { me = Actor, assignments, orders = orders.Select(o => new { o, assignment = assignments.FirstOrDefault(a => a.OrderId == o.Id) }), unassigned = orders.Where(o => !ids.Contains(o.Id)).Select(o => o.Id), technicians = techs, field } });
    }

    [HttpPost("orders/{id:guid}/assign")]
    public Task<IActionResult> Assign(Guid id, AssignRequest b) => Change($"tech:assign:{id}", b, async () =>
    {
        var (w, o) = await LockOrder(id);
        if (o.Status is RepairOrderStatus.Delivered or RepairOrderStatus.Cancelled) throw new DomainException("La orden está cerrada.");
        if (!IsAdmin && b.TechnicianId != Actor) throw new ForbiddenException("Solo un administrador asigna órdenes a otros técnicos.");
        var tech = await Db.Users.SingleOrDefaultAsync(x => x.Id == b.TechnicianId && x.ShopId == Shop) ?? throw new DomainException("Técnico no encontrado.");
        var a = await Own<OrderAssignment>().SingleOrDefaultAsync(x => x.OrderId == id);
        if (a is null) { a = new OrderAssignment { ShopId = Shop, OrderId = id }; Db.OrderAssignments.Add(a); }
        else if (a.State == "Working") throw new DomainException("Pausá el trabajo antes de reasignar la orden.");
        a.TechnicianId = tech.Id; a.Mode = b.Mode is "Field" ? "Field" : "Workshop"; a.FieldAddress = Saas.Text(b.FieldAddress, "Dirección", a.Mode == "Field" ? 5 : 0, 250);
        a.ScheduledAtUtc = b.ScheduledAtUtc; if (a.State == "Finished") a.State = "Assigned"; a.Version++;
        w.Version++;
        Note(id, $"Orden asignada a {tech.DisplayName}{(a.Mode == "Field" ? $" (servicio a domicilio en {a.FieldAddress})" : "")}.");
        if (tech.Id != Actor) Db.ShopAlerts.Add(new ShopAlert { ShopId = Shop, UserId = tech.Id, Kind = "Assignment", Message = $"Te asignaron {SaasEvents.Code(w.Number)} · {w.DeviceLabel}.", OrderId = id, Link = $"/tech" });
        return new { a.Id, a.Version };
    });

    [HttpPost("orders/{id:guid}/timer")]
    public Task<IActionResult> Timer(Guid id, TimerRequest b) => Change($"tech:timer:{id}", b, async () =>
    {
        var a = await Own<OrderAssignment>().SingleOrDefaultAsync(x => x.OrderId == id) ?? throw new DomainException("Asignate la orden antes de iniciar el trabajo.");
        if (!IsAdmin && a.TechnicianId != Actor) throw new ForbiddenException("La orden está asignada a otro técnico.");
        CheckVersion(a, b.Version);
        var now = DateTime.UtcNow;
        void Stop() { if (a.RunningSinceUtc is DateTime since) a.WorkedMinutes += (int)Math.Ceiling((now - since).TotalMinutes); a.RunningSinceUtc = null; }
        switch (b.Action)
        {
            case "start" when a.State is "Assigned" or "Paused":
                if (await Own<OrderAssignment>().AnyAsync(x => x.TechnicianId == a.TechnicianId && x.State == "Working" && x.Id != a.Id)) throw new DomainException("Ya tenés otra orden en curso. Pausala primero.");
                a.State = "Working"; a.RunningSinceUtc = now; break;
            case "pause" when a.State == "Working":
                Stop(); a.State = "Paused"; break;
            case "finish" when a.State is "Working" or "Paused" or "Assigned":
                Stop(); a.State = "Finished"; a.FinishedAtUtc = now; a.ClosingReport = Saas.Text(b.Report, "Informe", 0, 2000);
                Note(id, $"Trabajo técnico finalizado · {a.WorkedMinutes} min.{(a.ClosingReport.Length > 0 ? " " + a.ClosingReport : "")}");
                break;
            default: throw new DomainException("Acción no disponible en el estado actual.");
        }
        return new { a.Id, a.State, a.WorkedMinutes, a.Version };
    });
}

[ApiController, Authorize, Route("api/saas/diagram")]
public sealed class DiagramController(RepairShopDbContext db) : SaasController(db)
{
    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> Get(Guid orderId)
    {
        var d = await Own<OrderDiagram>().SingleOrDefaultAsync(x => x.OrderId == orderId);
        return Ok(new { data = d is null ? new { Version = 0, Template = "phone", Marks = JsonSerializer.Deserialize<JsonElement>("[]") } : new { d.Version, d.Template, Marks = JsonSerializer.Deserialize<JsonElement>(d.MarksJson) } });
    }

    [HttpPut("{orderId:guid}")]
    public Task<IActionResult> Save(Guid orderId, DiagramRequest b) => Change($"diagram:{orderId}", b, async () =>
    {
        var (w, _) = await LockOrder(orderId);
        if (b.Template is not ("phone" or "tablet" or "laptop" or "console" or "watch")) throw new DomainException("Plantilla inválida.");
        if (b.Marks is not { Count: <= 40 }) throw new DomainException("Hasta 40 marcas por equipo.");
        string[] kinds = ["scratch", "crack", "dent", "missing", "other"];
        var marks = b.Marks.Select(m => m.X is < 0 or > 100 || m.Y is < 0 or > 100 || !kinds.Contains(m.Kind) ? throw new DomainException("Marca inválida.") : m with { Note = Saas.Text(m.Note, "Nota", 0, 200) }).ToList();
        var d = await Own<OrderDiagram>().SingleOrDefaultAsync(x => x.OrderId == orderId);
        if (d is null) { d = new OrderDiagram { ShopId = Shop, OrderId = orderId, Version = 0 }; Db.OrderDiagrams.Add(d); }
        CheckVersion(d, b.Version);
        d.Template = b.Template; d.MarksJson = JsonSerializer.Serialize(marks, Saas.Json);
        Note(orderId, $"Diagrama de daños actualizado ({marks.Count} marcas).");
        return new { d.Version };
    });
}

[ApiController, Authorize, Route("api/saas/catalog")]
public sealed class CatalogController(RepairShopDbContext db) : SaasController(db)
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(new { data = await Own<ServiceCatalogItem>().OrderBy(x => x.Category).ThenBy(x => x.Name).ToListAsync() });

    private void Fill(ServiceCatalogItem s, CatalogRequest b)
    {
        s.Code = Saas.Text(b.Code, "Código", 1, 40).ToUpperInvariant(); s.Name = Saas.Text(b.Name, "Nombre", 2, 160); s.Category = Saas.Text(b.Category, "Categoría", 0, 80);
        s.Price = Saas.Money(b.Price, "El precio"); s.EstimatedCost = Saas.Money(b.EstimatedCost, "El costo"); s.Currency = Saas.Currency(b.Currency);
        s.EstimatedMinutes = b.EstimatedMinutes is >= 0 and <= 10000 ? b.EstimatedMinutes : throw new DomainException("Minutos inválidos.");
        s.WarrantyDays = b.WarrantyDays is >= 0 and <= 730 ? b.WarrantyDays : throw new DomainException("Garantía inválida.");
        s.Active = b.Active; s.UpdatedAtUtc = DateTime.UtcNow;
    }

    [HttpPost]
    public Task<IActionResult> Create(CatalogRequest b) => Change("catalog", b, async () =>
    {
        RequireAdmin();
        var s = new ServiceCatalogItem { ShopId = Shop }; Fill(s, b);
        if (await Own<ServiceCatalogItem>().AnyAsync(x => x.Code == s.Code)) throw new DomainException("Ya existe un servicio con ese código.");
        Db.ServiceCatalog.Add(s);
        return new { s.Id };
    });

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, CatalogRequest b) => Change($"catalog:{id}", b, async () =>
    {
        RequireAdmin();
        var s = await Find<ServiceCatalogItem>(id); CheckVersion(s, b.Version); Fill(s, b);
        if (await Own<ServiceCatalogItem>().AnyAsync(x => x.Code == s.Code && x.Id != id)) throw new DomainException("Ya existe un servicio con ese código.");
        return new { s.Id, s.Version };
    });

    [HttpPost("bulk-price")]
    public Task<IActionResult> Bulk(BulkPriceRequest b) => Change("catalog:bulk", b, async () =>
    {
        RequireAdmin();
        if (b.Percent is < -90 or > 500 or 0) throw new DomainException("El ajuste va de -90 % a 500 %.");
        var currency = Saas.Currency(b.Currency);
        var step = b.Rounding switch { "none" => 0.01m, "10" => 10m, "100" => 100m, "1000" => 1000m, _ => throw new DomainException("Redondeo inválido.") };
        var q = Own<ServiceCatalogItem>().Where(x => x.Currency == currency && x.Active);
        if (!string.IsNullOrWhiteSpace(b.Category)) q = q.Where(x => x.Category == b.Category);
        var rows = await q.ToListAsync();
        foreach (var s in rows)
        {
            s.Price = Math.Max(0, Math.Round(s.Price * (1 + b.Percent / 100m) / step, MidpointRounding.AwayFromZero) * step);
            s.Version++; s.UpdatedAtUtc = DateTime.UtcNow;
        }
        return new { updated = rows.Count };
    });

    // CSV: codigo;nombre;categoria;precio;costo;moneda;minutos;garantia (header optional; ; , or tab).
    [HttpPost("import")]
    public Task<IActionResult> Import(CatalogImportRequest b) => Change("catalog:import", b, async () =>
    {
        RequireAdmin();
        var lines = (b.Csv ?? "").Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length is 0 or > 2001) throw new DomainException("Importá entre 1 y 2000 filas.");
        var sep = lines[0].Contains(';') ? ';' : lines[0].Contains('\t') ? '\t' : ',';
        var created = 0; var updated = 0; var errors = new List<string>();
        var existing = await Own<ServiceCatalogItem>().ToDictionaryAsync(x => x.Code);
        for (var i = 0; i < lines.Length; i++)
        {
            var cols = lines[i].Split(sep).Select(c => c.Trim().Trim('"')).ToArray();
            if (i == 0 && cols[0].Equals("codigo", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                decimal Dec(int idx) => cols.Length > idx && cols[idx].Length > 0 ? decimal.Parse(cols[idx].Replace("$", "").Replace(" ", ""), NumberStyles.Number, cols[idx].LastIndexOf(',') > cols[idx].LastIndexOf('.') ? Saas.ArgentineNumbers : CultureInfo.InvariantCulture) : 0;
                int Int(int idx, int fallback) => cols.Length > idx && int.TryParse(cols[idx], out var v) ? v : fallback;
                var code = (cols.ElementAtOrDefault(0) ?? "").ToUpperInvariant();
                var request = new CatalogRequest(0, code, cols.ElementAtOrDefault(1) ?? "", cols.ElementAtOrDefault(2), Dec(3), Dec(4), cols.ElementAtOrDefault(5) is { Length: 3 } c ? c : "ARS", Int(6, 0), Int(7, 90), true);
                if (existing.TryGetValue(code, out var row)) { Fill(row, request); row.Version++; updated++; }
                else { row = new ServiceCatalogItem { ShopId = Shop }; Fill(row, request); Db.ServiceCatalog.Add(row); existing[row.Code] = row; created++; }
            }
            catch (Exception ex) when (ex is DomainException or FormatException or OverflowException) { errors.Add($"Fila {i + 1}: {ex.Message}"); }
        }
        if (errors.Count > 0) throw new DomainException("No se importó nada. " + string.Join(" · ", errors.Take(8)));
        return new { created, updated };
    });
}

[ApiController, Authorize, Route("api/saas/alerts")]
public sealed class AlertsController(RepairShopDbContext db) : SaasController(db)
{
    [HttpGet]
    public async Task<IActionResult> List() =>
        Ok(new { data = await Own<ShopAlert>().Where(x => x.UserId == null || x.UserId == Actor).OrderByDescending(x => x.CreatedAtUtc).Take(50).ToListAsync() });

    [HttpPost("read")]
    public async Task<IActionResult> ReadAll()
    {
        var now = DateTime.UtcNow;
        await Own<ShopAlert>().Where(x => x.ReadAtUtc == null && (x.UserId == null || x.UserId == Actor)).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReadAtUtc, now));
        return Ok(new { data = new { ok = true } });
    }
}

[ApiController, Authorize, Route("api/saas/reports")]
public sealed class ReportsController(RepairShopDbContext db) : SaasController(db)
{
    // Operational summary for a date range, plus satisfaction and technician productivity.
    [HttpGet]
    public async Task<IActionResult> Summary([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var shop = Shop; var start = from ?? DateTime.UtcNow.Date.AddDays(-30); var end = to ?? DateTime.UtcNow.AddDays(1);
        var surveys = await Own<SatisfactionSurvey>().Where(x => x.AnsweredAtUtc >= start && x.AnsweredAtUtc < end && x.Score != null).ToListAsync();
        var scores = surveys.Select(x => x.Score!.Value).ToList();
        var ordersCreated = await Db.RepairOrders.CountAsync(x => x.ShopId == shop && x.CreatedAtUtc >= start && x.CreatedAtUtc < end);
        var delivered = await Db.Workflows.CountAsync(x => x.ShopId == shop && x.HandedOverAtUtc >= start && x.HandedOverAtUtc < end);
        var payments = await Db.RepairOrderPayments.Where(x => x.ShopId == shop && x.CreatedAtUtc >= start && x.CreatedAtUtc < end).GroupBy(x => x.Currency).Select(g => new { Currency = g.Key, Total = g.Sum(x => x.Amount) }).ToListAsync();
        var sales = await Own<CounterSale>().Where(x => x.CreatedAtUtc >= start && x.CreatedAtUtc < end && x.Status == "Completed").GroupBy(x => x.Currency).Select(g => new { Currency = g.Key, Total = g.Sum(x => x.Total), Count = g.Count() }).ToListAsync();
        var users = await Db.Users.Where(x => x.ShopId == shop).Select(x => new { x.Id, x.DisplayName }).ToListAsync();
        var work = await Own<OrderAssignment>().Where(x => x.FinishedAtUtc >= start && x.FinishedAtUtc < end).GroupBy(x => x.TechnicianId).Select(g => new { TechnicianId = g.Key, Orders = g.Count(), Minutes = g.Sum(x => x.WorkedMinutes) }).ToListAsync();
        var invoices = await Own<FiscalInvoice>().Where(x => x.CreatedAtUtc >= start && x.CreatedAtUtc < end && x.Status != "Rejected").GroupBy(x => new { x.Currency, x.VoucherType }).Select(g => new { g.Key.Currency, g.Key.VoucherType, Total = g.Sum(x => x.Total), Count = g.Count() }).ToListAsync();
        return Ok(new { data = new {
            from = start, to = end, ordersCreated, delivered, payments, sales, invoices,
            satisfaction = new { responses = scores.Count, nps = SaasScheduler.Nps(scores), average = scores.Count == 0 ? 0 : Math.Round(scores.Average(), 1),
                comments = surveys.Where(x => x.Comment.Length > 0).OrderByDescending(x => x.AnsweredAtUtc).Take(20).Select(x => new { x.OrderId, x.Score, x.Comment, x.AnsweredAtUtc }) },
            technicians = work.Select(w => new { w.TechnicianId, users.FirstOrDefault(u => u.Id == w.TechnicianId)?.DisplayName, w.Orders, w.Minutes }) } });
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string kind, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var shop = Shop; var start = from ?? DateTime.UtcNow.Date.AddDays(-30); var end = to ?? DateTime.UtcNow.AddDays(1);
        var sb = new StringBuilder();
        static string C(object? v) => "\"" + (Convert.ToString(v, CultureInfo.InvariantCulture) ?? "").Replace("\"", "\"\"") + "\"";
        if (kind == "orders")
        {
            sb.AppendLine("numero;fecha;cliente;telefono;equipo;estado;entregado");
            var rows = await (from o in Db.RepairOrders join w in Db.Workflows on o.Id equals w.Id where o.ShopId == shop && o.CreatedAtUtc >= start && o.CreatedAtUtc < end orderby w.Number
                              select new { w.Number, o.CreatedAtUtc, w.CustomerName, w.CustomerPhone, w.DeviceLabel, o.Status, w.HandedOverAtUtc }).ToListAsync();
            foreach (var r in rows) sb.AppendLine(string.Join(';', C(SaasEvents.Code(r.Number)), C(r.CreatedAtUtc.ToString("yyyy-MM-dd")), C(r.CustomerName), C(r.CustomerPhone), C(r.DeviceLabel), C(r.Status), C(r.HandedOverAtUtc?.ToString("yyyy-MM-dd"))));
        }
        else if (kind == "cash")
        {
            sb.AppendLine("fecha;tipo;medio;moneda;importe;detalle");
            foreach (var m in await Own<CashMovement>().Where(x => x.CreatedAtUtc >= start && x.CreatedAtUtc < end).OrderBy(x => x.CreatedAtUtc).ToListAsync())
                sb.AppendLine(string.Join(';', C(m.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm")), C(m.Kind), C(m.Method), C(m.Currency), C(m.Amount), C(m.Description)));
        }
        else if (kind == "invoices")
        {
            sb.AppendLine("fecha;comprobante;numero;cliente;documento;neto;iva;total;moneda;cae;estado");
            foreach (var i in await Own<FiscalInvoice>().Where(x => x.CreatedAtUtc >= start && x.CreatedAtUtc < end).OrderBy(x => x.CreatedAtUtc).ToListAsync())
                sb.AppendLine(string.Join(';', C(i.CreatedAtUtc.ToString("yyyy-MM-dd")), C(FiscalRules.Name(i.VoucherType)), C($"{i.PointOfSale:00000}-{i.Number:00000000}"), C(i.CustomerName), C(i.DocNumber), C(i.Net), C(i.Vat), C(i.Total), C(i.Currency), C(i.Cae), C(i.Status)));
        }
        else throw new DomainException("Reporte inválido.");
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv", $"{kind}-{start:yyyyMMdd}-{end:yyyyMMdd}.csv");
    }
}

[ApiController, Authorize(Policy = RepairShop.Api.Security.Policies.AdminOnly), Route("api/saas/notifications")]
public sealed class NotificationsController(RepairShopDbContext db) : SaasController(db)
{
    // Message log: what was sent to customers, by which channel, and why a delivery failed.
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int take = 100) =>
        Ok(new { data = await Db.NotificationOutbox.Where(x => x.ShopId == Shop).OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(take, 1, 500))
            .Select(x => new { x.Id, Channel = x.Channel.ToString(), x.Recipient, x.Title, x.Body, Status = x.Status.ToString(), x.AttemptCount, x.LastError, x.RelatedEntityType, x.RelatedEntityId, x.CreatedAtUtc, x.UpdatedAtUtc }).ToListAsync() });
}
