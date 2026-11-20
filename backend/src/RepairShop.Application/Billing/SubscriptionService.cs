using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Application.Payments;
using RepairShop.Domain.Billing;
using RepairShop.Domain.Common;

namespace RepairShop.Application.Billing;

/// <summary>What the current organization may do, derived from its subscription (cached briefly by the API).</summary>
public sealed record SubscriptionAccess(bool HasFullAccess, PlanDefinition Plan, SubscriptionStatus Status)
{
    public bool Includes(string module) => Plan.Includes(module);
}

/// <summary>
/// Plans, checkout and the subscription lifecycle. Every offered country pays in its own currency through the
/// platform's Mercado Pago account in that country (see <see cref="BillingOptions.Countries"/>).
/// </summary>
public sealed class SubscriptionService
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IShopRepository _shops;
    private readonly IUserRepository _users;
    private readonly ISubscriptionGateway _gateway;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;
    private readonly IAppLinks _links;
    private readonly IAuditLog _audit;
    private readonly AdConversionService _conversions;
    private readonly BillingOptions _options;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(
        ISubscriptionRepository subscriptions,
        IShopRepository shops,
        IUserRepository users,
        ISubscriptionGateway gateway,
        IUnitOfWork uow,
        IDateTimeProvider clock,
        IAppLinks links,
        IAuditLog audit,
        AdConversionService conversions,
        IOptions<BillingOptions> options,
        ILogger<SubscriptionService> logger)
    {
        _subscriptions = subscriptions;
        _shops = shops;
        _users = users;
        _gateway = gateway;
        _uow = uow;
        _clock = clock;
        _links = links;
        _audit = audit;
        _conversions = conversions;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Currency the country pays in (Mercado Pago countries); USD only labels legacy accounts from elsewhere.</summary>
    public static string CurrencyFor(string country) => MercadoPagoCountries.CurrencyFor(country) ?? "USD";

    /// <summary>Public price list. Countries where RepairShop is not sold get the first offered country (Argentina).</summary>
    public PlansResponse ListPlans(string? country)
    {
        var requested = (country ?? "").Trim().ToUpperInvariant();
        var c = _options.IsOffered(requested) ? requested : _options.OfferedCountries().FirstOrDefault() ?? "AR";
        return new PlansResponse(c, CurrencyFor(c), _options.TrialDays, Plans.All.Select(p => ToResponse(p, c)).ToList());
    }

    public async Task<SubscriptionAccess> GetAccessAsync(Guid organizationId, CancellationToken ct)
    {
        var sub = await _subscriptions.GetByOrganizationAsync(organizationId, ct);
        var now = _clock.UtcNow;
        // Organizations without a row predate subscriptions (the migration grants them one); fail closed anyway.
        return sub is null
            ? new SubscriptionAccess(false, Plans.Basic, SubscriptionStatus.Expired)
            : new SubscriptionAccess(sub.HasFullAccess(now), sub.PlanDefinition, sub.EffectiveStatus(now));
    }

    public async Task<SubscriptionResponse> GetAsync(Guid organizationId, Guid userId, CancellationToken ct)
    {
        var sub = await GetSubscriptionAsync(organizationId, ct);
        var user = await _users.GetByIdAsync(userId, ct);
        return await ToResponseAsync(sub, user?.IsEmailVerified ?? true, ct);
    }

    /// <summary>Starts paying for a plan, or switches the plan of a running paid subscription.</summary>
    public async Task<CheckoutResponse> CheckoutAsync(Guid organizationId, Guid userId, CheckoutRequest req, Actor actor, CancellationToken ct)
    {
        if (!Plans.TryParse(req.Plan, out var planId)) throw new DomainException("Elegí un plan válido.");
        var plan = Plans.Get(planId);
        var now = _clock.UtcNow;
        var sub = await GetSubscriptionAsync(organizationId, ct);

        var branches = await CountActiveBranchesAsync(organizationId, ct);
        if (branches > plan.MaxBranches)
            throw new DomainException($"Tenés {branches} sucursales activas y el plan {plan.Name} permite {plan.MaxBranches}. Desactivá sucursales o elegí un plan mayor.");

        if (sub.Provider == BillingProvider.Manual && sub.Status == SubscriptionStatus.Active)
            throw new DomainException("Tu plan está bonificado. Escribinos si querés cambiarlo.");

        var country = sub.BillingCountry;
        var currency = CurrencyFor(country);
        if (!_options.IsOffered(country))
            throw new DomainException("Todavía no cobramos online en tu país. Escribinos y activamos tu plan a mano.");
        var price = _options.PriceFor(planId, country) ?? throw new DomainException("Ese plan no tiene precio configurado.");

        if (sub.HasLiveProviderSubscription)
        {
            if (sub.Plan == planId && sub.EffectiveStatus(now) == SubscriptionStatus.Active) throw new DomainException($"Ya estás en el plan {plan.Name}.");
            if (!_gateway.IsConfigured(country)) throw new DomainException("El cobro de suscripciones no está disponible en este momento.");
            await _gateway.ChangePlanAsync(country, sub.ProviderSubscriptionId!, planId, price, currency, ct);
            var from = sub.Plan;
            sub.ChangePlanLocally(planId, now);
            await AuditAsync(organizationId, "subscription_plan_changed", actor, new { from = from.ToString(), to = planId.ToString() }, ct);
            await _uow.SaveChangesAsync(ct);
            return new CheckoutResponse(null, true);
        }

        if (!_gateway.IsConfigured(country))
        {
            // IsOffered without credentials only happens with AllowSimulated (development/tests): activate right away for one month.
            if (!_options.AllowSimulated) throw new DomainException("El cobro de suscripciones no está disponible en este momento.");
            sub.ApplyProviderState(BillingProvider.Simulated, $"sim_{Guid.NewGuid():N}", planId, SubscriptionStatus.Active,
                now.AddMonths(1), price, currency, null, now, now);
            await AuditAsync(organizationId, "subscription_simulated", actor, new { plan = planId.ToString() }, ct);
            await _uow.SaveChangesAsync(ct);
            return new CheckoutResponse(_links.Billing(), true);
        }

        var user = await _users.GetByIdAsync(userId, ct) ?? throw new UnauthorizedException("Sesión inválida.");
        var checkout = await _gateway.CreateCheckoutAsync(country, new GatewayCheckoutRequest(
            organizationId, planId, price, currency, user.Email,
            $"RepairShop {plan.Name} (mensual)",
            _links.Billing()), ct);

        sub.BeginCheckout(planId, BillingProvider.MercadoPago, checkout.ProviderSubscriptionId, now);
        await AuditAsync(organizationId, "subscription_checkout_started", actor, new { plan = planId.ToString(), provider = nameof(BillingProvider.MercadoPago), country }, ct);
        await _uow.SaveChangesAsync(ct);
        return new CheckoutResponse(checkout.Url, false);
    }

    /// <summary>Cancels the paid subscription; the shop keeps full access until the end of the paid period.</summary>
    public async Task<SubscriptionResponse> CancelAsync(Guid organizationId, Guid userId, Actor actor, CancellationToken ct)
    {
        var sub = await GetSubscriptionAsync(organizationId, ct);
        if (!sub.HasLiveProviderSubscription && sub.Provider != BillingProvider.Simulated)
            throw new DomainException("No tenés una suscripción paga para cancelar.");
        if (sub.Status == SubscriptionStatus.Canceled) throw new DomainException("La suscripción ya está cancelada.");

        if (sub.HasLiveProviderSubscription)
        {
            if (!_gateway.IsConfigured(sub.BillingCountry)) throw new DomainException("El cobro de suscripciones no está disponible en este momento.");
            await _gateway.CancelAsync(sub.BillingCountry, sub.ProviderSubscriptionId!, ct);
        }

        sub.MarkCanceled(_clock.UtcNow);
        await AuditAsync(organizationId, "subscription_canceled", actor, new { plan = sub.Plan.ToString(), until = sub.CurrentPeriodEndsAtUtc }, ct);
        await _uow.SaveChangesAsync(ct);
        var user = await _users.GetByIdAsync(userId, ct);
        return await ToResponseAsync(sub, user?.IsEmailVerified ?? true, ct);
    }

    /// <summary>Branch limit of the plan (creating or re-activating branches).</summary>
    public async Task EnsureBranchCapacityAsync(Guid organizationId, CancellationToken ct)
    {
        var sub = await GetSubscriptionAsync(organizationId, ct);
        var plan = sub.PlanDefinition;
        var active = await CountActiveBranchesAsync(organizationId, ct);
        if (active >= plan.MaxBranches)
            throw new PaymentRequiredException(PaymentRequiredException.PlanUpgradeRequired,
                plan.MaxBranches == 1
                    ? $"El plan {plan.Name} incluye una sola sucursal. Pasate a un plan mayor para agregar más."
                    : $"El plan {plan.Name} permite hasta {plan.MaxBranches} sucursales activas.",
                "branches");
    }

    // ===== Webhooks =====

    /// <summary>
    /// Mercado Pago preapproval notifications sent by the platform's account in <paramref name="country"/>.
    /// Not accepted when that account is not configured or the signature is invalid.
    /// </summary>
    public async Task<WebhookOutcome> HandleMercadoPagoWebhookAsync(string country, MercadoPagoNotification n, CancellationToken ct)
    {
        country = (country ?? "").Trim().ToUpperInvariant();
        var account = _options.Account(country);
        if (account is null || !_gateway.IsConfigured(country)) return WebhookOutcome.Rejected;
        if (!MercadoPagoSignature.IsValid(account.WebhookSecret, n.Signature, n.RequestId, n.DataId, _clock.UtcNow, TimeSpan.FromMinutes(15)))
        {
            // Enough to tell a wrong secret from a missing header or a stale timestamp, without logging the signature.
            var (hasTs, hasV1, ageSeconds) = MercadoPagoSignature.Describe(n.Signature, _clock.UtcNow);
            _logger.LogWarning("Rejected Mercado Pago subscription webhook for {Country} with an invalid signature " +
                               "(x-signature: {HasHeader}, ts: {HasTs}, v1: {HasV1}, ts age: {AgeSeconds}s, x-request-id: {HasRequestId}, type: {Type}, data.id: {DataId}).",
                country, !string.IsNullOrWhiteSpace(n.Signature), hasTs, hasV1, ageSeconds, !string.IsNullOrWhiteSpace(n.RequestId), n.Type, n.DataId);
            return WebhookOutcome.Rejected;
        }

        string? preapprovalId = n.Type switch
        {
            "subscription_preapproval" or "preapproval" => n.DataId,
            "subscription_authorized_payment" or "authorized_payment" => await _gateway.GetPreapprovalIdForPaymentAsync(country, n.DataId!, ct),
            _ => null
        };
        if (preapprovalId is null) return WebhookOutcome.Ignored; // other topics: acknowledged, nothing to do

        // The state is read from Mercado Pago with the country's own token: the webhook body is never trusted.
        var state = await _gateway.GetSubscriptionAsync(country, preapprovalId, ct);
        if (state is null) return WebhookOutcome.Ignored;
        return new WebhookOutcome(true, await _uow.RetryOnConflictAsync(c => ApplyAsync(country, state, c), ct, maxAttempts: 6));
    }

    /// <summary>
    /// Applies the provider state and returns the organization it belongs to (null when unknown). Mercado Pago can
    /// send several notifications for one subscription at the same moment, so callers retry on a row version
    /// conflict instead of failing the webhook.
    /// </summary>
    private async Task<Guid?> ApplyAsync(string country, GatewaySubscriptionState state, CancellationToken ct)
    {
        const BillingProvider provider = BillingProvider.MercadoPago;
        var now = _clock.UtcNow;
        var sub = await _subscriptions.GetByProviderIdAsync(provider, state.ProviderSubscriptionId, ct);
        if (sub is null && state.OrganizationId is { } orgId) sub = await _subscriptions.GetByOrganizationAsync(orgId, ct);
        if (sub is null)
        {
            _logger.LogWarning("{Provider} subscription {Id} does not match any organization.", provider, state.ProviderSubscriptionId);
            return null;
        }
        if (!string.Equals(sub.BillingCountry, country, StringComparison.OrdinalIgnoreCase))
        {
            // Plan changes and cancellations go through the billing country's account: never link another country's.
            _logger.LogWarning("Mercado Pago {Country} subscription {Id} belongs to an organization billed in {BillingCountry}; ignored.",
                country, state.ProviderSubscriptionId, sub.BillingCountry);
            return sub.OrganizationId;
        }
        if (state.Status is null) return sub.OrganizationId;

        var previous = sub.ProviderSubscriptionId;
        var wasLive = sub.HasLiveProviderSubscription;
        var applied = sub.ApplyProviderState(provider, state.ProviderSubscriptionId, state.Plan, state.Status.Value,
            state.CurrentPeriodEndsAtUtc, state.Amount, state.Currency, state.CustomerId, state.EventAtUtc ?? now, now);
        if (!applied) return sub.OrganizationId;

        await AuditAsync(sub.OrganizationId, "subscription_updated", Actor.System,
            new { provider = provider.ToString(), status = state.Status.ToString(), plan = sub.Plan.ToString(), until = sub.CurrentPeriodEndsAtUtc }, ct);
        // First payment of this subscription (not a renewal or a plan change): the conversion ads optimize for.
        if (!wasLive && sub.HasLiveProviderSubscription && sub.Status == SubscriptionStatus.Active)
            await _conversions.QueuePurchaseAsync(sub, ct);
        await _uow.SaveChangesAsync(ct);

        // A new subscription replaced a running one (paid again from a fresh checkout): stop the old charges.
        if (wasLive && previous is not null && previous != state.ProviderSubscriptionId && state.Status == SubscriptionStatus.Active)
        {
            try { await _gateway.CancelAsync(country, previous, ct); }
            catch (Exception ex) { _logger.LogError(ex, "Could not cancel replaced {Provider} subscription {Id}.", provider, previous); }
        }
        return sub.OrganizationId;
    }

    // ---------------------------------------------------------------------------------------------

    private async Task<Subscription> GetSubscriptionAsync(Guid organizationId, CancellationToken ct)
        => await _subscriptions.GetByOrganizationAsync(organizationId, ct) ?? throw new NotFoundException("La organización no tiene suscripción.");

    private async Task<int> CountActiveBranchesAsync(Guid organizationId, CancellationToken ct)
        => (await _shops.ListByOrganizationAsync(organizationId, ct)).Count(s => s.IsActive);

    private async Task<SubscriptionResponse> ToResponseAsync(Subscription sub, bool emailVerified, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var plan = sub.PlanDefinition;
        var checkoutAvailable = _options.IsOffered(sub.BillingCountry);
        return new SubscriptionResponse(
            sub.Plan.ToString(),
            plan.Name,
            sub.EffectiveStatus(now).ToString(),
            sub.Provider.ToString(),
            sub.BillingCountry,
            sub.HasFullAccess(now),
            sub.TrialEndsAtUtc,
            sub.TrialDaysLeft(now),
            sub.CurrentPeriodEndsAtUtc,
            sub.CanceledAtUtc,
            sub.Amount,
            sub.Currency,
            sub.PendingPlan?.ToString(),
            plan.Modules,
            plan.MaxBranches,
            await CountActiveBranchesAsync(sub.OrganizationId, ct),
            sub.HasLiveProviderSubscription,
            checkoutAvailable,
            emailVerified,
            Plans.All.Select(p => ToResponse(p, sub.BillingCountry)).ToList(),
            AdConversionService.PurchaseEventId(sub));
    }

    /// <summary>
    /// Plan with its configured price in the country's currency; no price outside Mercado Pago countries. Whether the
    /// shop can pay right now is a separate flag (checkoutAvailable), so the public prices show before the account is set up.
    /// </summary>
    private PlanResponse ToResponse(PlanDefinition p, string country)
        => new(p.Id.ToString(), p.Name, p.MaxBranches, p.Modules,
            MercadoPagoCountries.IsSupported(country) ? _options.PriceFor(p.Id, country) : null, CurrencyFor(country));

    private async Task AuditAsync(Guid organizationId, string action, Actor actor, object data, CancellationToken ct)
    {
        // Audit events are per shop: record them on the organization's first branch (its id is the organization id).
        var shops = await _shops.ListByOrganizationAsync(organizationId, ct);
        var shop = shops.FirstOrDefault(s => s.Id == organizationId) ?? shops.OrderBy(s => s.CreatedAtUtc).FirstOrDefault();
        if (shop is null) return;
        await _audit.AddAsync(shop.Id, "subscription", organizationId, action, actor, data, ct);
    }
}

/// <summary>Webhook result: Accepted=false answers 401 (bad signature or provider off).</summary>
public sealed record WebhookOutcome(bool Accepted, Guid? OrganizationId)
{
    public static readonly WebhookOutcome Rejected = new(false, null);
    public static readonly WebhookOutcome Ignored = new(true, null);
}
