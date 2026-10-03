using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Billing;
using RepairShop.Domain.Billing;
using RepairShop.Domain.Common;

namespace RepairShop.Infrastructure.Billing;

/// <summary>
/// Paddle Billing (merchant of record): charges in US dollars, collects and remits each country's taxes and
/// pays out to the platform. Checkout runs with Paddle.js on the app's billing page (the "default payment link").
/// </summary>
public sealed class PaddleSubscriptionGateway : IPaddleSubscriptionGateway
{
    public const string HttpClientName = "paddle-billing";

    private readonly IHttpClientFactory _http;
    private readonly BillingOptions _options;

    public PaddleSubscriptionGateway(IHttpClientFactory http, IOptions<BillingOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public BillingProvider Provider => BillingProvider.Paddle;
    public bool IsConfigured => _options.Paddle.IsConfigured;

    public static string BaseUrl(PaddleBillingOptions o) => o.IsProduction ? "https://api.paddle.com/" : "https://sandbox-api.paddle.com/";

    public async Task<GatewayCheckout> CreateCheckoutAsync(GatewayCheckoutRequest r, CancellationToken ct)
    {
        var priceId = _options.Paddle.PriceIdFor(r.Plan) ?? throw new DomainException("Ese plan no tiene precio en Paddle.");
        using var doc = await SendAsync(Request(HttpMethod.Post, "transactions", new
        {
            items = new[] { new { price_id = priceId, quantity = 1 } },
            collection_mode = "automatic",
            custom_data = new { organization_id = r.OrganizationId.ToString(), plan = r.Plan.ToString() }
        }), "crear el pago", ct);

        var data = doc!.RootElement.GetProperty("data");
        // checkout.url is the default payment link (our billing page) with ?_ptxn=txn_...; Paddle.js opens the checkout there.
        var url = data.TryGetProperty("checkout", out var checkout) && checkout.ValueKind == JsonValueKind.Object ? Str(checkout, "url") : null;
        if (string.IsNullOrWhiteSpace(url))
            throw new DomainException("Paddle no devolvió el link de pago. Revisá que la cuenta tenga configurado el \"default payment link\".");
        return new GatewayCheckout(url, null); // the subscription id arrives with the subscription.created webhook
    }

    public async Task<GatewaySubscriptionState?> GetSubscriptionAsync(string id, CancellationToken ct)
    {
        if (!IsSafeId(id)) return null;
        using var doc = await SendAsync(Request(HttpMethod.Get, $"subscriptions/{id}"), "consultar la suscripción", ct, allowNotFound: true);
        return doc is null ? null : ToState(doc.RootElement.GetProperty("data"), null);
    }

    public async Task ChangePlanAsync(string id, PlanId plan, decimal amount, string currency, CancellationToken ct)
    {
        var priceId = _options.Paddle.PriceIdFor(plan) ?? throw new DomainException("Ese plan no tiene precio en Paddle.");
        using var _ = await SendAsync(Request(HttpMethod.Patch, $"subscriptions/{id}", new
        {
            items = new[] { new { price_id = priceId, quantity = 1 } },
            proration_billing_mode = "prorated_immediately",
            custom_data = new { plan = plan.ToString() }
        }), "cambiar el plan", ct);
    }

    public async Task CancelAsync(string id, CancellationToken ct)
    {
        if (!IsSafeId(id)) return;
        using var _ = await SendAsync(Request(HttpMethod.Post, $"subscriptions/{id}/cancel", new { effective_from = "next_billing_period" }), "cancelar la suscripción", ct);
    }

    public GatewaySubscriptionState? ParseWebhook(string rawBody)
    {
        using var doc = JsonDocument.Parse(rawBody);
        var root = doc.RootElement;
        var type = Str(root, "event_type") ?? "";
        if (!type.StartsWith("subscription.", StringComparison.Ordinal) || !root.TryGetProperty("data", out var data)) return null;
        return ToState(data, Date(root, "occurred_at"));
    }

    private GatewaySubscriptionState ToState(JsonElement d, DateTime? occurredAt)
    {
        Guid? org = null;
        PlanId? plan = null;
        if (d.TryGetProperty("custom_data", out var custom) && custom.ValueKind == JsonValueKind.Object)
        {
            if (Guid.TryParse(Str(custom, "organization_id"), out var g)) org = g;
            if (Plans.TryParse(Str(custom, "plan"), out var p)) plan = p;
        }

        decimal? amount = null;
        string? currency = Str(d, "currency_code");
        if (d.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0
            && items[0].TryGetProperty("price", out var price))
        {
            // The price id is the source of truth for the plan (custom data can lag behind a plan change).
            plan = _options.Paddle.PlanForPriceId(Str(price, "id")) ?? plan;
            if (price.TryGetProperty("unit_price", out var unit))
            {
                if (decimal.TryParse(Str(unit, "amount"), NumberStyles.Number, CultureInfo.InvariantCulture, out var minor)) amount = minor / 100m;
                currency = Str(unit, "currency_code") ?? currency;
            }
        }

        var cancelScheduled = d.TryGetProperty("scheduled_change", out var change) && change.ValueKind == JsonValueKind.Object
                              && Str(change, "action") == "cancel";
        SubscriptionStatus? status = Str(d, "status") switch
        {
            "active" or "trialing" => cancelScheduled ? SubscriptionStatus.Canceled : SubscriptionStatus.Active,
            "past_due" or "paused" => SubscriptionStatus.PastDue,
            "canceled" => SubscriptionStatus.Canceled,
            _ => null
        };

        DateTime? periodEnd = null;
        if (d.TryGetProperty("current_billing_period", out var period) && period.ValueKind == JsonValueKind.Object) periodEnd = Date(period, "ends_at");
        if (periodEnd is null && cancelScheduled) periodEnd = Date(change, "effective_at");

        return new GatewaySubscriptionState(Str(d, "id")!, org, plan, status, periodEnd, amount, currency, Str(d, "customer_id"), occurredAt ?? Date(d, "updated_at"));
    }

    private HttpRequestMessage Request(HttpMethod method, string path, object? body = null)
    {
        var req = new HttpRequestMessage(method, new Uri(new Uri(BaseUrl(_options.Paddle)), path));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Paddle.ApiKey);
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
            if (!res.IsSuccessStatusCode) throw new DomainException($"Paddle no pudo {what} ({(int)res.StatusCode}). Probá de nuevo en unos minutos.");
            return JsonDocument.Parse(body);
        }
    }

    private static bool IsSafeId(string? id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 64 && id.All(c => char.IsLetterOrDigit(c) || c == '_');

    private static string? Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static DateTime? Date(JsonElement e, string name)
        => Str(e, name) is { } s && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.UtcDateTime : null;
}
