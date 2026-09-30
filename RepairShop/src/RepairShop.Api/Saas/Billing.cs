using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RepairShop.Api.Security;
using RepairShop.Domain.Common;
using RepairShop.Domain.Saas;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Api.Saas;

public sealed class BillingOptions
{
    public const string Section = "Saas:Billing";
    // Simulated | MercadoPago
    public string Provider { get; set; } = "Simulated";
    public string AccessToken { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public string ApiBaseUrl { get; set; } = "https://api.mercadopago.com";
}

public sealed record CheckoutResult(string SubscriptionId, string RedirectUrl);
public sealed record ProviderSubscription(string Id, string Status, Guid? ShopId, string? Plan, DateTime? NextPaymentUtc);

public interface IBillingProvider
{
    string Name { get; }
    Task<CheckoutResult> CreateCheckoutAsync(Guid shop, PlanInfo plan, string payerEmail, string backUrl, CancellationToken ct);
    Task<ProviderSubscription> GetAsync(string subscriptionId, CancellationToken ct);
    Task CancelAsync(string subscriptionId, CancellationToken ct);
}

// Local/demo provider: the checkout page is served by the app itself and confirmed with one click.
public sealed class SimulatedBillingProvider(IOptions<SaasOptions> saas) : IBillingProvider
{
    public string Name => "Simulated";
    public Task<CheckoutResult> CreateCheckoutAsync(Guid shop, PlanInfo plan, string payerEmail, string backUrl, CancellationToken ct)
    {
        var id = $"sim_{shop:N}_{plan.Id}_{Saas.ShortCode(6)}";
        return Task.FromResult(new CheckoutResult(id, $"{saas.Value.PublicAppUrl.TrimEnd('/')}/billing/simulated?subscription={Uri.EscapeDataString(id)}"));
    }
    public Task<ProviderSubscription> GetAsync(string subscriptionId, CancellationToken ct)
    {
        var parts = subscriptionId.Split('_');
        return Task.FromResult(new ProviderSubscription(subscriptionId, "authorized", Guid.TryParse(parts.ElementAtOrDefault(1), out var shop) ? shop : null, parts.ElementAtOrDefault(2), DateTime.UtcNow.AddMonths(1)));
    }
    public Task CancelAsync(string subscriptionId, CancellationToken ct) => Task.CompletedTask;
}

// Mercado Pago "suscripciones sin plan asociado" (preapproval) with a monthly ARS charge.
public sealed class MercadoPagoBillingProvider(HttpClient http, IOptions<BillingOptions> options) : IBillingProvider
{
    public string Name => "MercadoPago";

    private HttpRequestMessage Request(HttpMethod method, string path, object? body = null)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.AccessToken)) throw new DomainException("Mercado Pago no está configurado (Saas:Billing:AccessToken).");
        var req = new HttpRequestMessage(method, $"{o.ApiBaseUrl.TrimEnd('/')}{path}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.AccessToken);
        if (body is not null) req.Content = JsonContent.Create(body);
        return req;
    }

    public async Task<CheckoutResult> CreateCheckoutAsync(Guid shop, PlanInfo plan, string payerEmail, string backUrl, CancellationToken ct)
    {
        var body = new
        {
            reason = $"RepairShop · Plan {plan.Name}",
            external_reference = $"{shop:N}:{plan.Id}",
            payer_email = payerEmail,
            back_url = backUrl,
            status = "pending",
            auto_recurring = new { frequency = 1, frequency_type = "months", transaction_amount = plan.Price, currency_id = plan.Currency },
        };
        using var res = await http.SendAsync(Request(HttpMethod.Post, "/preapproval", body), ct);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        if (!res.IsSuccessStatusCode) throw new DomainException($"Mercado Pago rechazó la suscripción: {json.GetPropertyOrDefault("message")}");
        return new CheckoutResult(json.GetProperty("id").GetString()!, json.GetProperty("init_point").GetString()!);
    }

    public async Task<ProviderSubscription> GetAsync(string subscriptionId, CancellationToken ct)
    {
        using var res = await http.SendAsync(Request(HttpMethod.Get, $"/preapproval/{Uri.EscapeDataString(subscriptionId)}"), ct);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        if (!res.IsSuccessStatusCode) throw new DomainException("No se pudo consultar la suscripción en Mercado Pago.");
        var reference = json.GetPropertyOrDefault("external_reference").Split(':');
        DateTime? next = DateTime.TryParse(json.GetPropertyOrDefault("next_payment_date"), null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var d) ? d : null;
        return new ProviderSubscription(subscriptionId, json.GetPropertyOrDefault("status"), Guid.TryParse(reference.ElementAtOrDefault(0), out var shop) ? shop : null, reference.ElementAtOrDefault(1), next);
    }

    public async Task CancelAsync(string subscriptionId, CancellationToken ct)
    {
        using var res = await http.SendAsync(Request(HttpMethod.Put, $"/preapproval/{Uri.EscapeDataString(subscriptionId)}", new { status = "cancelled" }), ct);
        if (!res.IsSuccessStatusCode) throw new DomainException("Mercado Pago no permitió cancelar la suscripción.");
    }

    // x-signature: "ts=...,v1=..." signed over "id:{data.id};request-id:{x-request-id};ts:{ts};"
    public static bool VerifySignature(string secret, string? signature, string? requestId, string dataId)
    {
        if (string.IsNullOrWhiteSpace(secret)) return false;
        if (string.IsNullOrWhiteSpace(signature)) return false;
        var parts = signature.Split(',').Select(p => p.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0].Trim(), p => p[1].Trim());
        if (!parts.TryGetValue("ts", out var ts) || !parts.TryGetValue("v1", out var v1)) return false;
        var manifest = $"id:{dataId.ToLowerInvariant()};request-id:{requestId};ts:{ts};";
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(v1.ToLowerInvariant()));
    }
}

public static class JsonElementExtensions
{
    public static string GetPropertyOrDefault(this JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) ? (v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString()) : "";
}

public sealed record CheckoutRequest(string Plan, string? PayerEmail);
public sealed record SimulatedConfirm(string SubscriptionId);

[ApiController, Route("api/saas/billing")]
public sealed class BillingController(RepairShopDbContext db, IBillingProvider provider, SubscriptionService subscriptions, IOptions<SaasOptions> saas, IOptions<BillingOptions> billing, ILogger<BillingController> log) : SaasController(db)
{
    [HttpGet, Authorize]
    public async Task<IActionResult> Status()
    {
        var sub = await subscriptions.EnsureAsync(Shop);
        var state = SubscriptionService.Evaluate(sub, saas.Value, DateTime.UtcNow);
        var events = await Own<BillingEvent>().OrderByDescending(x => x.CreatedAtUtc).Take(20).ToListAsync();
        return Ok(new { data = new {
            subscription = new { sub.Plan, sub.Status, sub.TrialEndsAtUtc, sub.CurrentPeriodEndsAtUtc, sub.Price, sub.Currency, sub.PendingPlan, sub.Provider, sub.PayerEmail },
            state.ReadOnly, state.Reason, EnabledModules = state.Modules,
            plans = Plans.All(saas.Value), catalog = Plans.Modules, provider = provider.Name, events } });
    }

    [HttpPost("checkout"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Checkout(CheckoutRequest body, CancellationToken ct)
    {
        var plan = Plans.Get(body.Plan, saas.Value);
        var sub = await subscriptions.EnsureAsync(Shop);
        var email = Saas.Email(body.PayerEmail ?? RepairShop.Api.Common.CurrentUser.GetEmail(User), required: true);
        var result = await provider.CreateCheckoutAsync(Shop, plan, email, $"{saas.Value.PublicAppUrl.TrimEnd('/')}/settings/plan?checkout=done", ct);
        sub.PendingPlan = plan.Id; sub.PayerEmail = email; sub.UpdatedAtUtc = DateTime.UtcNow; sub.Version++;
        Db.BillingEvents.Add(new BillingEvent { ShopId = Shop, Kind = "CheckoutStarted", Detail = $"Plan {plan.Name} · {plan.Price:N0} {plan.Currency}/mes", ProviderReference = result.SubscriptionId });
        await Db.SaveChangesAsync();
        subscriptions.Invalidate(Shop);
        return Ok(new { data = new { url = result.RedirectUrl, result.SubscriptionId } });
    }

    [HttpPost("cancel"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Cancel(CancellationToken ct)
    {
        var sub = await subscriptions.EnsureAsync(Shop);
        if (sub.ProviderSubscriptionId is { } id) await provider.CancelAsync(id, ct);
        sub.CurrentPeriodEndsAtUtc ??= sub.Status == "Trialing" ? sub.TrialEndsAtUtc : DateTime.UtcNow;
        sub.Status = "Cancelled"; sub.UpdatedAtUtc = DateTime.UtcNow; sub.Version++;
        Db.BillingEvents.Add(new BillingEvent { ShopId = Shop, Kind = "Cancelled", Detail = "Cancelada por el administrador. El acceso sigue hasta el fin del período pago.", ProviderReference = sub.ProviderSubscriptionId });
        await Db.SaveChangesAsync();
        subscriptions.Invalidate(Shop);
        return Ok(new { data = new { sub.Status, sub.CurrentPeriodEndsAtUtc } });
    }

    // Only available with the simulated provider; the real flow is confirmed by the Mercado Pago webhook.
    [HttpPost("simulated/confirm"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> ConfirmSimulated(SimulatedConfirm body, CancellationToken ct)
    {
        if (provider is not SimulatedBillingProvider) throw new ForbiddenException("El cobro simulado está deshabilitado.");
        var remote = await provider.GetAsync(body.SubscriptionId, ct);
        if (remote.ShopId != Shop) throw new DomainException("La suscripción no corresponde a este taller.");
        await Apply(remote);
        return Ok(new { data = new { ok = true } });
    }

    [HttpPost("webhook"), AllowAnonymous]
    public async Task<IActionResult> Webhook([FromQuery(Name = "data.id")] string? queryId, [FromQuery] string? type, CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var raw = await reader.ReadToEndAsync(ct);
        JsonElement payload = default;
        try { payload = JsonSerializer.Deserialize<JsonElement>(raw); } catch (JsonException) { }
        var dataId = queryId ?? (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("data", out var data) ? data.GetPropertyOrDefault("id") : "");
        type ??= payload.GetPropertyOrDefault("type");
        if (string.IsNullOrWhiteSpace(dataId)) return Ok();
        if (provider is MercadoPagoBillingProvider && !MercadoPagoBillingProvider.VerifySignature(billing.Value.WebhookSecret, Request.Headers["x-signature"], Request.Headers["x-request-id"], dataId))
        {
            log.LogWarning("Rejected Mercado Pago webhook with an invalid signature for {DataId}", dataId);
            return Unauthorized();
        }
        // Payments (subscription_authorized_payment) also refresh the preapproval they belong to.
        if (type is not ("subscription_preapproval" or "preapproval")) return Ok();
        var remote = await provider.GetAsync(dataId, ct);
        await Apply(remote);
        return Ok();
    }

    private async Task Apply(ProviderSubscription remote)
    {
        if (remote.ShopId is not Guid shop) return;
        var sub = await Db.ShopSubscriptions.SingleOrDefaultAsync(x => x.ShopId == shop);
        if (sub is null) return;
        var status = remote.Status switch { "authorized" => "Active", "paused" => "PastDue", "cancelled" => "Cancelled", _ => sub.Status };
        if (remote.Status == "authorized" && remote.Plan is { } plan && Plans.Ids.Contains(plan))
        {
            sub.Plan = plan; sub.Price = Plans.Get(plan, saas.Value).Price; sub.PendingPlan = null;
        }
        // A new authorization replaces the previous preapproval; cancel the old one so the shop is not charged twice.
        if (remote.Status == "authorized" && sub.ProviderSubscriptionId is { } previous && previous != remote.Id)
        {
            try { await provider.CancelAsync(previous, CancellationToken.None); }
            catch (Exception ex) { log.LogWarning(ex, "Could not cancel previous subscription {Previous}", previous); }
        }
        if (remote.Status == "authorized" || sub.ProviderSubscriptionId == remote.Id)
        {
            sub.ProviderSubscriptionId = remote.Id;
            sub.Status = status;
            sub.CurrentPeriodEndsAtUtc = remote.NextPaymentUtc ?? sub.CurrentPeriodEndsAtUtc;
        }
        sub.Provider = provider.Name; sub.UpdatedAtUtc = DateTime.UtcNow; sub.Version++;
        Db.BillingEvents.Add(new BillingEvent { ShopId = shop, Kind = "ProviderUpdate", Detail = $"Estado {remote.Status} · plan {sub.Plan}", ProviderReference = remote.Id });
        if (status == "PastDue") Db.ShopAlerts.Add(new ShopAlert { ShopId = shop, Kind = "Billing", Message = "No pudimos cobrar la suscripción. Revisá el medio de pago en Mercado Pago.", Link = "/settings/plan" });
        await Db.SaveChangesAsync();
        subscriptions.Invalidate(shop);
    }
}
