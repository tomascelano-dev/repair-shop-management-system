using RepairShop.Domain.Billing;

namespace RepairShop.Application.Abstractions;

public interface ISubscriptionRepository
{
    Task<Subscription?> GetByOrganizationAsync(Guid organizationId, CancellationToken ct);
    Task<Subscription?> GetByProviderIdAsync(BillingProvider provider, string providerSubscriptionId, CancellationToken ct);
    Task AddAsync(Subscription subscription, CancellationToken ct);
}

/// <summary>Sets up what a brand-new shop needs to work (message templates and other defaults).</summary>
public interface IShopProvisioner
{
    Task ProvisionAsync(Guid shopId, CancellationToken ct);
}

/// <summary>A payment provider that charges the monthly subscription (Mercado Pago, Paddle).</summary>
public interface ISubscriptionGateway
{
    BillingProvider Provider { get; }

    /// <summary>Credentials are present: the gateway can be offered to customers.</summary>
    bool IsConfigured { get; }

    /// <summary>Creates the checkout and returns the URL where the customer pays.</summary>
    Task<GatewayCheckout> CreateCheckoutAsync(GatewayCheckoutRequest request, CancellationToken ct);

    /// <summary>Current state at the provider (never trust webhook bodies blindly when a lookup is available).</summary>
    Task<GatewaySubscriptionState?> GetSubscriptionAsync(string providerSubscriptionId, CancellationToken ct);

    /// <summary>Moves a running subscription to another plan from the next charge.</summary>
    Task ChangePlanAsync(string providerSubscriptionId, PlanId plan, decimal amount, string currency, CancellationToken ct);

    /// <summary>Cancels at the end of the paid period.</summary>
    Task CancelAsync(string providerSubscriptionId, CancellationToken ct);
}

public sealed record GatewayCheckoutRequest(
    Guid OrganizationId,
    PlanId Plan,
    decimal Amount,
    string Currency,
    string PayerEmail,
    string Description,
    string ReturnUrl,
    string NotificationUrl);

/// <summary>Checkout URL plus the provider's subscription id when it exists before payment (Mercado Pago).</summary>
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
