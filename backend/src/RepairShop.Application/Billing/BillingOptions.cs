using RepairShop.Domain.Billing;

namespace RepairShop.Application.Billing;

public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    /// <summary>Days of free trial for new signups (no card required).</summary>
    public int TrialDays { get; set; } = 14;

    /// <summary>
    /// Development and tests: activate subscriptions without charging when the country's Mercado Pago account is not
    /// configured. Never enable it on a public server (anyone could pick a paid plan for free); Production refuses to start.
    /// </summary>
    public bool AllowSimulated { get; set; }

    /// <summary>Allow new shops to sign up from the website.</summary>
    public bool SignupEnabled { get; set; } = true;

    /// <summary>Public support email shown in the Terms, Privacy and Refund pages.</summary>
    public string ContactEmail { get; set; } = "";

    /// <summary>Name of the person or company that sells the service, shown in the Terms.</summary>
    public string LegalName { get; set; } = "";

    /// <summary>
    /// Countries where RepairShop is sold, keyed by ISO code (AR, MX...). Each one charges in its own currency through
    /// the platform's Mercado Pago account in that country (Mercado Pago accounts are per country).
    /// </summary>
    public Dictionary<string, MercadoPagoAccountOptions> Countries { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public MercadoPagoAccountOptions? Account(string? country)
    {
        if (string.IsNullOrWhiteSpace(country)) return null;
        var key = country.Trim();
        if (Countries.TryGetValue(key, out var account)) return account;
        // The configuration binder may rebuild the dictionary with the default (case-sensitive) comparer.
        foreach (var (k, v) in Countries)
            if (string.Equals(k.Trim(), key, StringComparison.OrdinalIgnoreCase)) return v;
        return null;
    }

    /// <summary>
    /// Shops from this country can sign up and pay: Mercado Pago sells subscriptions there, the plans have prices and
    /// the account has credentials (or, outside Production, charges are simulated).
    /// </summary>
    public bool IsOffered(string? country)
    {
        var account = Account(country);
        return MercadoPagoCountries.IsSupported(country) && account is not null && account.HasAllPrices && (account.IsConfigured || AllowSimulated);
    }

    /// <summary>Offered countries, Argentina first.</summary>
    public IReadOnlyList<string> OfferedCountries()
        => Countries.Keys
            .Select(c => c.Trim().ToUpperInvariant())
            .Where(IsOffered)
            .Distinct()
            .OrderBy(c => c == "AR" ? 0 : 1)
            .ThenBy(c => c, StringComparer.Ordinal)
            .ToList();

    /// <summary>Monthly price of the plan in the country's currency (null when not configured).</summary>
    public decimal? PriceFor(PlanId plan, string? country)
        => Account(country)?.PriceFor(plan);
}

/// <summary>The platform's own Mercado Pago account in one country (not a shop's).</summary>
public sealed class MercadoPagoAccountOptions
{
    /// <summary>Production access token of the account (APP_USR-...).</summary>
    public string AccessToken { get; set; } = "";

    /// <summary>Secret of the webhook configured in that account (Tus integraciones → Webhooks).</summary>
    public string WebhookSecret { get; set; } = "";

    /// <summary>Monthly price per plan (Basic, Standard, Pro) in the country's currency. Final prices, taxes included.</summary>
    public Dictionary<string, decimal> Prices { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(AccessToken) && !string.IsNullOrWhiteSpace(WebhookSecret);

    public bool HasAllPrices => Plans.All.All(p => PriceFor(p.Id) is not null);

    public decimal? PriceFor(PlanId plan)
    {
        foreach (var (k, price) in Prices)
            if (string.Equals(k.Trim(), plan.ToString(), StringComparison.OrdinalIgnoreCase)) return price > 0 ? price : null;
        return null;
    }
}

/// <summary>Countries where Mercado Pago sells recurring subscriptions (preapproval), with the currency it charges in.</summary>
public static class MercadoPagoCountries
{
    private static readonly Dictionary<string, string> Currencies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AR"] = "ARS",
        ["BR"] = "BRL",
        ["CL"] = "CLP",
        ["CO"] = "COP",
        ["MX"] = "MXN",
        ["PE"] = "PEN",
        ["UY"] = "UYU",
    };

    public static IReadOnlyCollection<string> All => Currencies.Keys;

    public static bool IsSupported(string? country) => country is not null && Currencies.ContainsKey(country.Trim());

    /// <summary>Currency Mercado Pago charges in for the country; null outside Mercado Pago countries.</summary>
    public static string? CurrencyFor(string? country) => country is not null && Currencies.TryGetValue(country.Trim(), out var c) ? c : null;
}
