using RepairShop.Domain.Common;

namespace RepairShop.Domain.Billing;

public enum SubscriptionStatus
{
    Trialing = 1,
    Active = 2,
    PastDue = 3,
    Canceled = 4,
    Expired = 5
}

public enum BillingProvider
{
    None = 0,
    /// <summary>Granted by the platform owner (existing shops, partners). Never expires on its own.</summary>
    Manual = 1,
    MercadoPago = 2,
    Paddle = 3,
    /// <summary>Development and tests only: activates without charging.</summary>
    Simulated = 4
}

/// <summary>
/// The SaaS subscription of one organization (all its branches). Starts as a free trial; a payment provider
/// (Mercado Pago in Argentina, Paddle elsewhere) moves it to Active and keeps it up to date through webhooks.
/// </summary>
public sealed class Subscription
{
    /// <summary>Days of full access after a missed renewal before the account becomes read-only.</summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(5);

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; private set; }

    public PlanId Plan { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public BillingProvider Provider { get; private set; }

    /// <summary>ISO 3166-1 alpha-2 country used to pick the provider and currency (AR => Mercado Pago in ARS).</summary>
    public string BillingCountry { get; private set; } = "AR";

    public string? ProviderSubscriptionId { get; private set; }
    public string? ProviderCustomerId { get; private set; }

    /// <summary>Plan the customer is paying for in an unfinished checkout.</summary>
    public PlanId? PendingPlan { get; private set; }

    public decimal? Amount { get; private set; }
    public string? Currency { get; private set; }

    public DateTime TrialEndsAtUtc { get; private set; }
    public DateTime? CurrentPeriodEndsAtUtc { get; private set; }
    public DateTime? CanceledAtUtc { get; private set; }

    /// <summary>Time of the last provider event applied (older, out-of-order events are ignored).</summary>
    public DateTime? LastEventAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Subscription() { } // EF

    private Subscription(Guid organizationId, PlanId plan, SubscriptionStatus status, BillingProvider provider, string country, DateTime trialEndsAtUtc, DateTime nowUtc)
    {
        if (organizationId == Guid.Empty) throw new DomainException("La organización es obligatoria.");
        if (!Enum.IsDefined(plan)) throw new DomainException("El plan es inválido.");
        OrganizationId = organizationId;
        Plan = plan;
        Status = status;
        Provider = provider;
        BillingCountry = NormalizeCountry(country);
        TrialEndsAtUtc = trialEndsAtUtc;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public static Subscription StartTrial(Guid organizationId, string country, int trialDays, DateTime nowUtc)
    {
        if (trialDays is < 1 or > 90) throw new DomainException("La prueba gratis debe durar entre 1 y 90 días.");
        // The trial unlocks every module so the shop sees the whole product before choosing.
        return new Subscription(organizationId, PlanId.Pro, SubscriptionStatus.Trialing, BillingProvider.None, country, nowUtc.AddDays(trialDays), nowUtc);
    }

    public static Subscription Complimentary(Guid organizationId, PlanId plan, string country, DateTime nowUtc)
        => new(organizationId, plan, SubscriptionStatus.Active, BillingProvider.Manual, country, nowUtc, nowUtc);

    public PlanDefinition PlanDefinition => Plans.Get(Plan);

    /// <summary>Full access means the shop can create and change data. Without it the account is read-only.</summary>
    public bool HasFullAccess(DateTime nowUtc) => EffectiveStatus(nowUtc) != SubscriptionStatus.Expired;

    /// <summary>Status with time applied: an ended trial or a lapsed period reads as Expired without waiting for a job.</summary>
    public SubscriptionStatus EffectiveStatus(DateTime nowUtc)
    {
        switch (Status)
        {
            case SubscriptionStatus.Trialing:
                return nowUtc < TrialEndsAtUtc ? SubscriptionStatus.Trialing : SubscriptionStatus.Expired;
            case SubscriptionStatus.Active:
                if (Provider == BillingProvider.Manual || CurrentPeriodEndsAtUtc is null) return SubscriptionStatus.Active;
                if (nowUtc < CurrentPeriodEndsAtUtc) return SubscriptionStatus.Active;
                // Renewal not confirmed yet (webhook late or payment retrying): grace period, then read-only.
                return nowUtc < CurrentPeriodEndsAtUtc + GracePeriod ? SubscriptionStatus.PastDue : SubscriptionStatus.Expired;
            case SubscriptionStatus.PastDue:
                var since = CurrentPeriodEndsAtUtc ?? UpdatedAtUtc;
                return nowUtc < since + GracePeriod ? SubscriptionStatus.PastDue : SubscriptionStatus.Expired;
            case SubscriptionStatus.Canceled:
                return CurrentPeriodEndsAtUtc > nowUtc ? SubscriptionStatus.Canceled : SubscriptionStatus.Expired;
            default:
                return SubscriptionStatus.Expired;
        }
    }

    public int TrialDaysLeft(DateTime nowUtc)
        => Status == SubscriptionStatus.Trialing && nowUtc < TrialEndsAtUtc ? (int)Math.Ceiling((TrialEndsAtUtc - nowUtc).TotalDays) : 0;

    /// <summary>True while a paid subscription is running at the provider (plan changes and cancellations go through it).</summary>
    public bool HasLiveProviderSubscription
        => Provider is BillingProvider.MercadoPago or BillingProvider.Paddle
           && ProviderSubscriptionId is not null
           && Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue;

    public void SetBillingCountry(string country, DateTime nowUtc)
    {
        BillingCountry = NormalizeCountry(country);
        UpdatedAtUtc = nowUtc;
    }

    public void BeginCheckout(PlanId plan, BillingProvider provider, string? providerSubscriptionId, DateTime nowUtc)
    {
        if (!Enum.IsDefined(plan)) throw new DomainException("El plan es inválido.");
        PendingPlan = plan;
        if (providerSubscriptionId is not null && !HasLiveProviderSubscription)
        {
            Provider = provider;
            ProviderSubscriptionId = providerSubscriptionId;
        }
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Applies the state reported by the payment provider (webhook or lookup). Events older than the last one
    /// applied are ignored, so retries and out-of-order deliveries are harmless.
    /// </summary>
    public bool ApplyProviderState(
        BillingProvider provider,
        string providerSubscriptionId,
        PlanId? plan,
        SubscriptionStatus status,
        DateTime? currentPeriodEndsAtUtc,
        decimal? amount,
        string? currency,
        string? customerId,
        DateTime eventAtUtc,
        DateTime nowUtc)
    {
        if (provider is BillingProvider.None or BillingProvider.Manual) throw new DomainException("Proveedor de cobro inválido.");
        if (string.IsNullOrWhiteSpace(providerSubscriptionId)) throw new DomainException("Falta el id de la suscripción del proveedor.");
        if (LastEventAtUtc is not null && eventAtUtc < LastEventAtUtc) return false;

        // A different provider subscription only replaces the current one when that one is not live anymore
        // (e.g. a canceled Mercado Pago subscription must not override the new active one).
        var sameSubscription = Provider == provider && string.Equals(ProviderSubscriptionId, providerSubscriptionId, StringComparison.Ordinal);
        if (!sameSubscription && HasLiveProviderSubscription && status is not SubscriptionStatus.Active) return false;

        Provider = provider;
        ProviderSubscriptionId = providerSubscriptionId.Trim();
        if (!string.IsNullOrWhiteSpace(customerId)) ProviderCustomerId = customerId.Trim();
        if (plan is not null && Enum.IsDefined(plan.Value)) Plan = plan.Value;
        else if (status == SubscriptionStatus.Active && PendingPlan is not null) Plan = PendingPlan.Value;
        if (status == SubscriptionStatus.Active) PendingPlan = null;

        Status = status;
        if (currentPeriodEndsAtUtc is not null) CurrentPeriodEndsAtUtc = currentPeriodEndsAtUtc;
        if (amount is not null) Amount = amount;
        if (!string.IsNullOrWhiteSpace(currency)) Currency = currency.Trim().ToUpperInvariant();
        CanceledAtUtc = status == SubscriptionStatus.Canceled ? CanceledAtUtc ?? nowUtc : null;

        LastEventAtUtc = eventAtUtc;
        UpdatedAtUtc = nowUtc;
        return true;
    }

    public void ChangePlanLocally(PlanId plan, DateTime nowUtc)
    {
        if (!Enum.IsDefined(plan)) throw new DomainException("El plan es inválido.");
        Plan = plan;
        PendingPlan = null;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Platform owner grants the plan for free (admin command).</summary>
    public void GrantComplimentary(PlanId plan, DateTime nowUtc)
    {
        ChangePlanLocally(plan, nowUtc);
        Status = SubscriptionStatus.Active;
        Provider = BillingProvider.Manual;
        ProviderSubscriptionId = null;
        CurrentPeriodEndsAtUtc = null;
        CanceledAtUtc = null;
    }

    public void MarkCanceled(DateTime nowUtc)
    {
        Status = SubscriptionStatus.Canceled;
        CanceledAtUtc ??= nowUtc;
        PendingPlan = null;
        UpdatedAtUtc = nowUtc;
    }

    public static string NormalizeCountry(string? country)
    {
        var c = (country ?? "").Trim().ToUpperInvariant();
        if (c.Length != 2 || !c.All(char.IsAsciiLetterUpper)) throw new DomainException("El país debe ser un código de 2 letras (ej. AR, MX, ES).");
        return c;
    }
}
