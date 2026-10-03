using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Billing;
using RepairShop.Domain.Billing;
using RepairShop.Domain.Common;

namespace RepairShop.Infrastructure.Billing;

/// <summary>
/// Monthly subscriptions in Argentine pesos with Mercado Pago "suscripciones sin plan asociado" (preapproval),
/// charged to the platform's own Mercado Pago account.
/// </summary>
public sealed class MercadoPagoSubscriptionGateway : IMercadoPagoSubscriptionGateway
{
    public const string HttpClientName = "mercadopago-billing";

    private readonly IHttpClientFactory _http;
    private readonly BillingOptions _options;
    private readonly ILogger<MercadoPagoSubscriptionGateway> _logger;

    public MercadoPagoSubscriptionGateway(IHttpClientFactory http, IOptions<BillingOptions> options, ILogger<MercadoPagoSubscriptionGateway> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public BillingProvider Provider => BillingProvider.MercadoPago;
    public bool IsConfigured => _options.MercadoPago.IsConfigured;

    public static string ExternalReference(Guid organizationId, PlanId plan) => $"org:{organizationId:N}:plan:{plan}";

    public static (Guid? OrganizationId, PlanId? Plan) ParseExternalReference(string? value)
    {
        var parts = (value ?? "").Split(':');
        if (parts.Length != 4 || parts[0] != "org" || parts[2] != "plan") return (null, null);
        Guid? org = Guid.TryParseExact(parts[1], "N", out var g) ? g : null;
        PlanId? plan = Plans.TryParse(parts[3], out var p) ? p : null;
        return (org, plan);
    }

    public async Task<GatewayCheckout> CreateCheckoutAsync(GatewayCheckoutRequest r, CancellationToken ct)
    {
        using var req = Request(HttpMethod.Post, "preapproval", new
        {
            reason = r.Description,
            external_reference = ExternalReference(r.OrganizationId, r.Plan),
            payer_email = r.PayerEmail,
            auto_recurring = new { frequency = 1, frequency_type = "months", transaction_amount = r.Amount, currency_id = r.Currency },
            back_url = r.ReturnUrl,
            status = "pending"
        });
        req.Headers.Add("X-Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var doc = await SendAsync(req, "crear la suscripción", ct);
        var root = doc!.RootElement;
        return new GatewayCheckout(root.GetProperty("init_point").GetString()!, root.GetProperty("id").GetString());
    }

    public async Task<GatewaySubscriptionState?> GetSubscriptionAsync(string id, CancellationToken ct)
    {
        if (!IsSafeId(id)) return null;
        using var doc = await SendAsync(Request(HttpMethod.Get, $"preapproval/{id}"), "consultar la suscripción", ct, allowNotFound: true);
        return doc is null ? null : ToState(doc.RootElement);
    }

    public async Task ChangePlanAsync(string id, PlanId plan, decimal amount, string currency, CancellationToken ct)
    {
        var current = await GetSubscriptionAsync(id, ct) ?? throw new DomainException("No encontramos la suscripción en Mercado Pago.");
        using var _ = await SendAsync(Request(HttpMethod.Put, $"preapproval/{id}", new
        {
            reason = $"RepairShop {Plans.Get(plan).Name} (mensual)",
            external_reference = ExternalReference(current.OrganizationId ?? Guid.Empty, plan),
            auto_recurring = new { transaction_amount = amount, currency_id = currency }
        }), "cambiar el plan", ct);
    }

    public async Task CancelAsync(string id, CancellationToken ct)
    {
        if (!IsSafeId(id)) return;
        using var _ = await SendAsync(Request(HttpMethod.Put, $"preapproval/{id}", new { status = "cancelled" }), "cancelar la suscripción", ct);
    }

    public async Task<string?> GetPreapprovalIdForPaymentAsync(string authorizedPaymentId, CancellationToken ct)
    {
        if (!IsSafeId(authorizedPaymentId)) return null;
        using var doc = await SendAsync(Request(HttpMethod.Get, $"authorized_payments/{authorizedPaymentId}"), "consultar el cobro", ct, allowNotFound: true);
        return doc is not null && doc.RootElement.TryGetProperty("preapproval_id", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    }

    public static GatewaySubscriptionState ToState(JsonElement p)
    {
        var (org, plan) = ParseExternalReference(Str(p, "external_reference"));
        SubscriptionStatus? status = Str(p, "status") switch
        {
            "authorized" => SubscriptionStatus.Active,
            "paused" => SubscriptionStatus.PastDue,
            "cancelled" => SubscriptionStatus.Canceled,
            _ => null // pending: the customer has not paid yet
        };
        decimal? amount = null;
        string? currency = null;
        if (p.TryGetProperty("auto_recurring", out var ar) && ar.ValueKind == JsonValueKind.Object)
        {
            if (ar.TryGetProperty("transaction_amount", out var a) && a.ValueKind == JsonValueKind.Number) amount = a.GetDecimal();
            currency = Str(ar, "currency_id");
        }
        return new GatewaySubscriptionState(
            p.GetProperty("id").GetString()!,
            org,
            plan,
            status,
            Date(p, "next_payment_date"),
            amount,
            currency,
            p.TryGetProperty("payer_id", out var payer) && payer.ValueKind != JsonValueKind.Null ? payer.ToString() : null,
            Date(p, "last_modified"));
    }

    private HttpRequestMessage Request(HttpMethod method, string path, object? body = null)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.MercadoPago.AccessToken);
        if (body is not null) req.Content = JsonContent.Create(body);
        return req;
    }

    private async Task<JsonDocument?> SendAsync(HttpRequestMessage req, string what, CancellationToken ct, bool allowNotFound = false)
    {
        using (req)
        {
            using var res = await _http.CreateClient(HttpClientName).SendAsync(req, ct);
            if (allowNotFound && res.StatusCode == HttpStatusCode.NotFound) return null;
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                var (code, detail) = ErrorOf(body);
                _logger.LogWarning("MercadoPago: {Action} failed with {Status}: {Code} {Detail}", what, (int)res.StatusCode, code, detail);
                // 401/403 mean the platform's credentials are wrong, which retrying will not fix.
                if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    throw new DomainException("El cobro con Mercado Pago no está bien configurado todavía. Escribinos y lo resolvemos.");
                throw new DomainException($"Mercado Pago no pudo {what} ({(int)res.StatusCode}). Probá de nuevo en unos minutos.");
            }
            return JsonDocument.Parse(body);
        }
    }

    /// <summary>Mercado Pago errors: {"error":"unauthorized","message":"..."}.</summary>
    private static (string? Code, string? Detail) ErrorOf(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                ? (Str(doc.RootElement, "error"), Str(doc.RootElement, "message"))
                : (null, null);
        }
        catch (JsonException) { return (null, null); }
    }

    private static bool IsSafeId(string? id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 64 && id.All(char.IsLetterOrDigit);

    private static string? Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static DateTime? Date(JsonElement e, string name)
        => Str(e, name) is { } s && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.UtcDateTime : null;
}
