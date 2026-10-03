using RepairShop.Domain.Billing;

namespace RepairShop.Application.Billing;

public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    /// <summary>Days of free trial for new signups (no card required).</summary>
    public int TrialDays { get; set; } = 14;

    /// <summary>
    /// Development and tests: activate subscriptions without charging when the provider is not configured.
    /// Never enable it on a public server (anyone could pick a paid plan for free).
    /// </summary>
    public bool AllowSimulated { get; set; }

    /// <summary>Allow new shops to sign up from the website.</summary>
    public bool SignupEnabled { get; set; } = true;

    /// <summary>Monthly price per plan in US dollars (Paddle, every country except Argentina). Must match the Paddle prices.</summary>
    public Dictionary<string, decimal> UsdPrices { get; set; } = new() { ["Basic"] = 25m, ["Standard"] = 45m, ["Pro"] = 79m };

    /// <summary>Monthly price per plan in Argentine pesos (Mercado Pago).</summary>
    public Dictionary<string, decimal> ArsPrices { get; set; } = new() { ["Basic"] = 29900m, ["Standard"] = 44900m, ["Pro"] = 74900m };

    public MercadoPagoBillingOptions MercadoPago { get; set; } = new();
    public PaddleBillingOptions Paddle { get; set; } = new();

    public decimal? PriceFor(PlanId plan, string currency)
    {
        var table = string.Equals(currency, "ARS", StringComparison.OrdinalIgnoreCase) ? ArsPrices : UsdPrices;
        return table.TryGetValue(plan.ToString(), out var price) && price > 0 ? price : null;
    }
}

public sealed class MercadoPagoBillingOptions
{
    /// <summary>Access token of the platform's own Mercado Pago account (not a shop's).</summary>
    public string AccessToken { get; set; } = "";

    /// <summary>Secret of the webhook configured in Mercado Pago (Tus integraciones → Webhooks).</summary>
    public string WebhookSecret { get; set; } = "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(AccessToken) && !string.IsNullOrWhiteSpace(WebhookSecret);
}

public sealed class PaddleBillingOptions
{
    /// <summary>"sandbox" (default) or "production".</summary>
    public string Environment { get; set; } = "sandbox";

    /// <summary>Server-side API key (Paddle → Developer tools → Authentication).</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Client-side token for Paddle.js (Paddle → Developer tools → Authentication → client-side tokens).</summary>
    public string ClientToken { get; set; } = "";

    /// <summary>Secret key of the notification destination (Paddle → Developer tools → Notifications).</summary>
    public string WebhookSecret { get; set; } = "";

    /// <summary>Paddle price id per plan (pri_...), each a monthly recurring price in USD.</summary>
    public Dictionary<string, string> PriceIds { get; set; } = new();

    public bool IsProduction => string.Equals(Environment, "production", StringComparison.OrdinalIgnoreCase);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey)
                                && !string.IsNullOrWhiteSpace(ClientToken)
                                && !string.IsNullOrWhiteSpace(WebhookSecret)
                                && Plans.All.All(p => PriceIds.TryGetValue(p.Id.ToString(), out var id) && !string.IsNullOrWhiteSpace(id));

    public string? PriceIdFor(PlanId plan) => PriceIds.TryGetValue(plan.ToString(), out var id) && !string.IsNullOrWhiteSpace(id) ? id.Trim() : null;

    public PlanId? PlanForPriceId(string? priceId)
    {
        if (string.IsNullOrWhiteSpace(priceId)) return null;
        foreach (var (key, value) in PriceIds)
            if (string.Equals(value?.Trim(), priceId, StringComparison.Ordinal) && Plans.TryParse(key, out var plan)) return plan;
        return null;
    }
}
