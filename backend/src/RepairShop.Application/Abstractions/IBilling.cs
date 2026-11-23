using RepairShop.Domain.Billing;

namespace RepairShop.Application.Abstractions;

public interface ISubscriptionRepository
{
    Task<Subscription?> GetByOrganizationAsync(Guid organizationId, CancellationToken ct);
    Task<Subscription?> GetByProviderIdAsync(BillingProvider provider, string providerSubscriptionId, CancellationToken ct);
    Task AddAsync(Subscription subscription, CancellationToken ct);

    /// <summary>Provider subscriptions whose period ends (or ended) between the two instants, or that have no known end.</summary>
    Task<List<Subscription>> ListForReconciliationAsync(BillingProvider provider, DateTime fromUtc, DateTime toUtc, int take, CancellationToken ct);
}

/// <summary>Sets up what a brand-new shop needs to work (message templates and other defaults).</summary>
public interface IShopProvisioner
{
    Task ProvisionAsync(Guid shopId, CancellationToken ct);
}

/// <summary>
/// Charges the monthly subscription with Mercado Pago. Mercado Pago accounts are per country, so every call names
/// the country whose platform account it uses (the subscription's billing country).
/// </summary>
public interface ISubscriptionGateway
{
    /// <summary>The country's account has credentials: checkouts can be offered there.</summary>
    bool IsConfigured(string country);

    /// <summary>Creates the checkout and returns the URL where the customer pays.</summary>
    Task<GatewayCheckout> CreateCheckoutAsync(string country, GatewayCheckoutRequest request, CancellationToken ct);

    /// <summary>Current state at the provider (never trust webhook bodies blindly).</summary>
    Task<GatewaySubscriptionState?> GetSubscriptionAsync(string country, string providerSubscriptionId, CancellationToken ct);

    /// <summary>Moves a running subscription to another plan from the next charge.</summary>
    Task ChangePlanAsync(string country, string providerSubscriptionId, PlanId plan, decimal amount, string currency, CancellationToken ct);

    /// <summary>Cancels the subscription (no more charges).</summary>
    Task CancelAsync(string country, string providerSubscriptionId, CancellationToken ct);

    /// <summary>Subscription (preapproval) id of a recurring charge notification.</summary>
    Task<string?> GetPreapprovalIdForPaymentAsync(string country, string authorizedPaymentId, CancellationToken ct);
}

public sealed record GatewayCheckoutRequest(
    Guid OrganizationId,
    PlanId Plan,
    decimal Amount,
    string Currency,
    string PayerEmail,
    string Description,
    string ReturnUrl);

/// <summary>Checkout URL plus the provider's subscription id (Mercado Pago creates it before payment).</summary>
public sealed record GatewayCheckout(string Url, string? ProviderSubscriptionId);

public sealed record GatewaySubscriptionState(
    string ProviderSubscriptionId,
    Guid? OrganizationId,
    PlanId? Plan,
    SubscriptionStatus? Status,
    DateTime? CurrentPeriodEndsAtUtc,
    decimal? Amount,
    string? Currency,
    string? CustomerId,
    DateTime? EventAtUtc);

/// <summary>Campaign data of self-service signups and the conversions queued for the ad platforms.</summary>
public interface IAdTrackingRepository
{
    Task<SignupAttribution?> GetAttributionAsync(Guid organizationId, CancellationToken ct);
    Task AddAttributionAsync(SignupAttribution attribution, CancellationToken ct);
    Task<bool> ConversionExistsAsync(string platform, string eventId, CancellationToken ct);
    Task AddConversionAsync(AdConversion conversion, CancellationToken ct);
}
