using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RepairShop.Api.Common;
using RepairShop.Domain.Common;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Saas;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Api.Saas;

public static class Webhooks
{
    public static readonly string[] Events = ["order.created", "order.status_changed", "order.delivered", "quote.decided", "payment.created", "invoice.issued", "sale.created", "appointment.created", "survey.answered"];

    // Adds one delivery per subscribed endpoint to the current unit of work.
    public static void Enqueue(RepairShopDbContext db, Guid shop, string evt, object data)
    {
        var endpoints = db.WebhookEndpoints.Where(x => x.ShopId == shop && x.Active).Select(x => new { x.Id, x.Events }).ToList();
        if (endpoints.Count == 0) return;
        var payload = JsonSerializer.Serialize(new { id = Guid.NewGuid(), type = evt, createdAt = DateTime.UtcNow, shopId = shop, data }, Saas.Json);
        foreach (var e in endpoints.Where(e => e.Events == "*" || e.Events.Split(',').Contains(evt)))
            db.WebhookDeliveries.Add(new WebhookDelivery { ShopId = shop, EndpointId = e.Id, Event = evt, PayloadJson = payload });
    }

    public static string Sign(string secret, string timestamp, string payload) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"))).ToLowerInvariant();
}

public sealed class WebhookDispatcher(IServiceScopeFactory scopes, IHttpClientFactory clients, ILogger<WebhookDispatcher> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(7), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Tick(stoppingToken); }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { log.LogWarning(ex, "Webhook dispatch failed"); }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task Tick(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RepairShopDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector(IntegrationsController.Purpose);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        var batch = await db.WebhookDeliveries.FromSqlInterpolated($@"SELECT * FROM saas_webhook_delivery
            WHERE ""Status"" IN ('Pending','Failed') AND ""NextAttemptAtUtc"" <= {now} ORDER BY ""CreatedAtUtc"" LIMIT 20 FOR UPDATE SKIP LOCKED").ToListAsync(ct);
        var http = clients.CreateClient("webhooks");
        foreach (var d in batch)
        {
            var endpoint = await db.WebhookEndpoints.SingleOrDefaultAsync(x => x.Id == d.EndpointId, ct);
            if (endpoint is null || !endpoint.Active) { d.Status = "Dead"; d.LastError = "Endpoint desactivado."; continue; }
            d.Attempts++;
            try
            {
                var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                using var req = new HttpRequestMessage(HttpMethod.Post, endpoint.Url) { Content = new StringContent(d.PayloadJson, Encoding.UTF8, "application/json") };
                req.Headers.Add("X-RepairShop-Event", d.Event);
                req.Headers.Add("X-RepairShop-Timestamp", ts);
                req.Headers.Add("X-RepairShop-Signature", "v1=" + Webhooks.Sign(protector.Unprotect(endpoint.ProtectedSecret), ts, d.PayloadJson));
                using var res = await http.SendAsync(req, ct);
                d.ResponseCode = (int)res.StatusCode;
                if (res.IsSuccessStatusCode) { d.Status = "Sent"; d.LastError = ""; continue; }
                d.LastError = $"HTTP {(int)res.StatusCode}";
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { d.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message; }
            d.Status = d.Attempts >= 8 ? "Dead" : "Failed";
            d.NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(Math.Pow(2, d.Attempts));
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}

public sealed record ApiKeyRequest(string Name);
public sealed record WebhookRequest(string Url, string[] Events);
public sealed record WooSettingsRequest(string Url, string? ConsumerKey, string? ConsumerSecret);

[ApiController, Authorize, Route("api/saas/integrations")]
public sealed class IntegrationsController(RepairShopDbContext db, IDataProtectionProvider protection, IHttpClientFactory clients, IHostEnvironment env) : SaasController(db)
{
    public const string Purpose = "RepairShop.Integrations.v1";
    private IDataProtector Protector => protection.CreateProtector(Purpose);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var keys = await Own<ApiKey>().OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, x.Name, x.Prefix, x.CreatedAtUtc, x.LastUsedAtUtc, x.RevokedAtUtc }).ToListAsync();
        var hooks = await Own<WebhookEndpoint>().OrderBy(x => x.CreatedAtUtc).Select(x => new { x.Id, x.Url, x.Events, x.Active, x.CreatedAtUtc }).ToListAsync();
        var deliveries = await Own<WebhookDelivery>().OrderByDescending(x => x.CreatedAtUtc).Take(30).Select(x => new { x.Id, x.EndpointId, x.Event, x.Status, x.Attempts, x.ResponseCode, x.LastError, x.CreatedAtUtc }).ToListAsync();
        var woo = await Own<IntegrationSettings>().Select(x => new { x.WooUrl, HasKeys = x.ProtectedWooKey != "", x.LastSyncAtUtc, x.LastSyncResult }).SingleOrDefaultAsync();
        return Ok(new { data = new { keys, hooks, deliveries, woo, events = Webhooks.Events } });
    }

    [HttpPost("keys")]
    public async Task<IActionResult> CreateKey(ApiKeyRequest b)
    {
        RequireAdmin();
        var prefix = Saas.ShortCode(6).ToLowerInvariant();
        var secret = Saas.Token(24).ToLowerInvariant();
        var full = $"rs_{prefix}_{secret}";
        Db.ApiKeys.Add(new ApiKey { ShopId = Shop, Name = Saas.Text(b.Name, "Nombre", 2, 80), Prefix = prefix, Hash = Saas.Sha256(full) });
        await Db.SaveChangesAsync();
        // The full key is shown once; only its hash is stored.
        return Ok(new { data = new { key = full } });
    }

    [HttpDelete("keys/{id:guid}")]
    public async Task<IActionResult> RevokeKey(Guid id)
    {
        RequireAdmin();
        var k = await Find<ApiKey>(id); k.RevokedAtUtc ??= DateTime.UtcNow; k.Version++;
        await Db.SaveChangesAsync();
        return Ok(new { data = new { k.Id } });
    }

    private string ValidUrl(string url)
    {
        if (!Uri.TryCreate((url ?? "").Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != "https" && !(env.IsDevelopment() && uri.Scheme == "http")))
            throw new DomainException("Ingresá una URL https válida.");
        if (!env.IsDevelopment() && (uri.IsLoopback || uri.Host.EndsWith(".local") || uri.Host.EndsWith(".internal"))) throw new DomainException("La URL debe ser pública.");
        return uri.ToString();
    }

    [HttpPost("webhooks")]
    public async Task<IActionResult> CreateHook(WebhookRequest b)
    {
        RequireAdmin();
        if (await Own<WebhookEndpoint>().CountAsync() >= 10) throw new DomainException("Hasta 10 webhooks por taller.");
        var events = b.Events is { Length: > 0 } && !b.Events.Contains("*") ? b.Events.Distinct().ToArray() : ["*"];
        if (events.Any(e => e != "*" && !Webhooks.Events.Contains(e))) throw new DomainException("Evento inválido.");
        var secret = "whsec_" + Saas.Token(24).ToLowerInvariant();
        var hook = new WebhookEndpoint { ShopId = Shop, Url = ValidUrl(b.Url), Events = string.Join(',', events), ProtectedSecret = Protector.Protect(secret) };
        Db.WebhookEndpoints.Add(hook);
        await Db.SaveChangesAsync();
        return Ok(new { data = new { hook.Id, secret } });
    }

    [HttpDelete("webhooks/{id:guid}")]
    public async Task<IActionResult> DeleteHook(Guid id)
    {
        RequireAdmin();
        Db.WebhookEndpoints.Remove(await Find<WebhookEndpoint>(id));
        await Db.SaveChangesAsync();
        return Ok(new { data = new { id } });
    }

    [HttpPost("webhooks/{id:guid}/test")]
    public async Task<IActionResult> TestHook(Guid id)
    {
        RequireAdmin();
        var hook = await Find<WebhookEndpoint>(id);
        Db.WebhookDeliveries.Add(new WebhookDelivery { ShopId = Shop, EndpointId = hook.Id, Event = "ping", PayloadJson = JsonSerializer.Serialize(new { id = Guid.NewGuid(), type = "ping", createdAt = DateTime.UtcNow, shopId = Shop, data = new { message = "Webhook de prueba de RepairShop" } }, Saas.Json) });
        await Db.SaveChangesAsync();
        return Ok(new { data = new { queued = true } });
    }

    [HttpPut("woocommerce")]
    public async Task<IActionResult> SaveWoo(WooSettingsRequest b)
    {
        RequireAdmin();
        var s = await Own<IntegrationSettings>().SingleOrDefaultAsync();
        if (s is null) { s = new IntegrationSettings { ShopId = Shop }; Db.IntegrationSettings.Add(s); }
        s.WooUrl = string.IsNullOrWhiteSpace(b.Url) ? "" : ValidUrl(b.Url).TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(b.ConsumerKey)) s.ProtectedWooKey = Protector.Protect(b.ConsumerKey.Trim());
        if (!string.IsNullOrWhiteSpace(b.ConsumerSecret)) s.ProtectedWooSecret = Protector.Protect(b.ConsumerSecret.Trim());
        s.Version++;
        await Db.SaveChangesAsync();
        return Ok(new { data = new { s.WooUrl } });
    }

    // Publishes the parts catalog (SKU, name and available stock) to WooCommerce, creating or updating products by SKU.
    [HttpPost("woocommerce/sync")]
    public async Task<IActionResult> SyncWoo(CancellationToken ct)
    {
        RequireAdmin();
        var s = await Own<IntegrationSettings>().SingleOrDefaultAsync();
        if (s is null || s.WooUrl.Length == 0 || s.ProtectedWooKey.Length == 0) throw new DomainException("Configurá la URL y las claves de WooCommerce.");
        var http = clients.CreateClient("woocommerce");
        var auth = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Protector.Unprotect(s.ProtectedWooKey)}:{Protector.Unprotect(s.ProtectedWooSecret)}")));
        var remote = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        for (var page = 1; page <= 50; page++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{s.WooUrl}/wp-json/wc/v3/products?per_page=100&page={page}&_fields=id,sku");
            req.Headers.Authorization = auth;
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) throw new DomainException($"WooCommerce respondió {(int)res.StatusCode}. Revisá la URL y los permisos de lectura/escritura de la clave.");
            var items = await res.Content.ReadFromJsonAsync<List<JsonElement>>(cancellationToken: ct) ?? [];
            foreach (var p in items) if (p.GetPropertyOrDefault("sku") is { Length: > 0 } sku) remote[sku] = p.GetProperty("id").GetInt64();
            if (items.Count < 100) break;
        }
        var items2 = await Db.InventoryItems.Where(x => x.ShopId == Shop && x.IsActive).ToListAsync(ct);
        var lots = await Own<RepairShop.Domain.Premium.StockLot>().GroupBy(x => x.ItemId).Select(g => new { g.Key, Qty = g.Sum(x => x.Quantity) }).ToDictionaryAsync(x => x.Key, x => x.Qty, ct);
        var reserved = await (from r in Own<RepairShop.Domain.Premium.StockReservation>() join l in Own<RepairShop.Domain.Premium.StockLot>() on r.LotId equals l.Id where r.Status == "Reserved" group r by l.ItemId into g select new { g.Key, Qty = g.Sum(x => x.Quantity) }).ToDictionaryAsync(x => x.Key, x => x.Qty, ct);
        var create = new List<object>(); var update = new List<object>();
        foreach (var i in items2)
        {
            var stock = Math.Max(0, lots.GetValueOrDefault(i.Id, i.QuantityOnHand) - reserved.GetValueOrDefault(i.Id));
            if (remote.TryGetValue(i.Sku, out var id)) update.Add(new { id, name = i.Name, manage_stock = true, stock_quantity = stock });
            else create.Add(new { sku = i.Sku, name = i.Name, type = "simple", status = "draft", manage_stock = true, stock_quantity = stock });
        }
        foreach (var chunk in create.Cast<object>().Select(x => (kind: "create", x)).Concat(update.Select(x => (kind: "update", x))).Chunk(100))
        {
            var body = new Dictionary<string, object> { ["create"] = chunk.Where(c => c.kind == "create").Select(c => c.x).ToList(), ["update"] = chunk.Where(c => c.kind == "update").Select(c => c.x).ToList() };
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{s.WooUrl}/wp-json/wc/v3/products/batch") { Content = JsonContent.Create(body) };
            req.Headers.Authorization = auth;
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) throw new DomainException($"WooCommerce rechazó la sincronización ({(int)res.StatusCode}).");
        }
        s.LastSyncAtUtc = DateTime.UtcNow; s.LastSyncResult = $"{create.Count} productos nuevos (borrador) y {update.Count} actualizados."; s.Version++;
        await Db.SaveChangesAsync(ct);
        return Ok(new { data = new { s.LastSyncResult } });
    }
}

public sealed class ApiKeyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, RepairShopDbContext db, SubscriptionService subscriptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public new const string Scheme = "ApiKey";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var raw = Request.Headers["X-Api-Key"].ToString();
        if (raw.Length == 0 && Request.Headers.Authorization.ToString() is { } h && h.StartsWith("Bearer rs_")) raw = h["Bearer ".Length..];
        if (raw.Length == 0) return AuthenticateResult.NoResult();
        var key = await db.ApiKeys.SingleOrDefaultAsync(x => x.Hash == Saas.Sha256(raw) && x.RevokedAtUtc == null);
        if (key is null) return AuthenticateResult.Fail("Clave de API inválida.");
        var state = await subscriptions.GetAsync(key.ShopId);
        if (!state.Modules.Contains("api")) return AuthenticateResult.Fail("El plan no incluye la API.");
        if (key.LastUsedAtUtc is null || key.LastUsedAtUtc < DateTime.UtcNow.AddMinutes(-5)) { key.LastUsedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(); }
        var identity = new ClaimsIdentity([new Claim("shop_id", key.ShopId.ToString()), new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString()), new Claim(ClaimTypes.Role, "Api"), new Claim("api_key", key.Id.ToString()), new Claim("read_only", state.ReadOnly.ToString())], Scheme);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme));
    }
}

public sealed record ExternalAppointment(string CustomerName, string Phone, string? Email, string? DeviceLabel, string Reason, DateTime StartsAtUtc, int? DurationMinutes);

// Public REST API for Zapier, Make, n8n or custom integrations.
[ApiController, Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.Scheme), Route("api/ext/v1")]
public sealed class ExternalApiController(RepairShopDbContext db) : ControllerBase
{
    private Guid Shop => CurrentUser.GetShopId(User);

    [HttpGet("orders")]
    public async Task<IActionResult> Orders([FromQuery] DateTime? updatedSince, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200); page = Math.Max(1, page);
        var shop = Shop; var since = updatedSince ?? DateTime.MinValue;
        var q = from o in db.RepairOrders join w in db.Workflows on o.Id equals w.Id where o.ShopId == shop && o.UpdatedAtUtc >= since orderby o.UpdatedAtUtc
                select new { o.Id, Number = w.Number, w.CustomerName, w.CustomerPhone, w.DeviceLabel, w.Identifier, o.IssueDescription, Status = o.Status.ToString(), o.CreatedAtUtc, o.UpdatedAtUtc, w.HandedOverAtUtc };
        return Ok(new { data = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(), page, pageSize, total = await q.CountAsync() });
    }

    [HttpGet("orders/{id:guid}")]
    public async Task<IActionResult> Order(Guid id)
    {
        var shop = Shop;
        var order = await (from o in db.RepairOrders join w in db.Workflows on o.Id equals w.Id where o.ShopId == shop && o.Id == id
                           select new { o.Id, w.Number, w.CustomerName, w.CustomerPhone, w.DeviceLabel, w.Identifier, o.IssueDescription, w.Diagnosis, Status = o.Status.ToString(), o.CreatedAtUtc, o.UpdatedAtUtc, w.HandedOverAtUtc }).SingleOrDefaultAsync()
            ?? throw new RepairShop.Application.Common.NotFoundException("Orden no encontrada.");
        var quotes = await db.WorkflowQuotes.Where(x => x.ShopId == shop && x.OrderId == id).OrderByDescending(x => x.Revision).Select(x => new { x.Revision, x.Total, x.Currency, x.Status, x.CreatedAtUtc }).ToListAsync();
        var payments = await db.RepairOrderPayments.Where(x => x.ShopId == shop && x.RepairOrderId == id).Select(x => new { x.Id, x.Amount, x.Currency, Method = x.Method.ToString(), x.CreatedAtUtc }).ToListAsync();
        return Ok(new { data = new { order, quotes, payments } });
    }

    [HttpGet("customers")]
    public async Task<IActionResult> Customers([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200); page = Math.Max(1, page);
        var q = db.Customers.Where(x => x.ShopId == Shop).OrderBy(x => x.CreatedAtUtc).Select(x => new { x.Id, x.FullName, x.Phone, x.CreatedAtUtc });
        return Ok(new { data = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(), page, pageSize, total = await q.CountAsync() });
    }

    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog() =>
        Ok(new { data = await db.ServiceCatalog.Where(x => x.ShopId == Shop && x.Active).OrderBy(x => x.Name).Select(x => new { x.Id, x.Code, x.Name, x.Category, x.Price, x.Currency, x.EstimatedMinutes, x.WarrantyDays }).ToListAsync() });

    [HttpPost("appointments")]
    public async Task<IActionResult> Appointment(ExternalAppointment b)
    {
        if (User.FindFirst("read_only")?.Value == "True") throw new SubscriptionInactiveException("La suscripción del taller no está activa.");
        var a = new Appointment { ShopId = Shop, CustomerName = Saas.Text(b.CustomerName, "Cliente", 3, 120), Phone = Saas.Text(b.Phone, "Teléfono", 6, 40), Email = Saas.Email(b.Email),
            DeviceLabel = Saas.Text(b.DeviceLabel, "Equipo", 0, 120), Reason = Saas.Text(b.Reason, "Motivo", 3, 500), StartsAtUtc = DateTime.SpecifyKind(b.StartsAtUtc, DateTimeKind.Utc),
            DurationMinutes = b.DurationMinutes is >= 10 and <= 600 ? b.DurationMinutes.Value : 30, Source = "Api" };
        if (a.StartsAtUtc < DateTime.UtcNow.AddHours(-1)) throw new DomainException("El turno debe ser futuro.");
        db.Appointments.Add(a);
        db.ShopAlerts.Add(new ShopAlert { ShopId = Shop, Kind = "Booking", Message = $"Turno recibido por API: {a.CustomerName}, {Saas.When(SaasScheduler.Local(a.StartsAtUtc), false)}.", Link = "/agenda" });
        await db.SaveChangesAsync();
        return Ok(new { data = new { a.Id } });
    }
}
