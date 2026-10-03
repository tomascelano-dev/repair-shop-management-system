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
/// Plans, checkout and the subscription lifecycle. Argentina pays in pesos through Mercado Pago; every other
/// country pays in dollars through Paddle (merchant of record: it charges and remits each country's taxes).
/// </summary>
public sealed class SubscriptionService
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IShopRepository _shops;
    private readonly IUserRepository _users;
    private readonly IEnumerable<ISubscriptionGateway> _gateways;
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
        IEnumerable<ISubscriptionGateway> gateways,
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
        _gateways = gateways;
        _uow = uow;
        _clock = clock;
        _links = links;
        _audit = audit;
        _conversions = conversions;
        _options = options.Value;
        _logger = logger;
    }

    public static string CurrencyFor(string country) => string.Equals(country, "AR", StringComparison.OrdinalIgnoreCase) ? "ARS" : "USD";

    public static BillingProvider ProviderFor(string country) => string.Equals(country, "AR", StringComparison.OrdinalIgnoreCase) ? BillingProvider.MercadoPago : BillingProvider.Paddle;

    public PlansResponse ListPlans(string? country)
    {
        var c = string.IsNullOrWhiteSpace(country) ? "US" : Subscription.NormalizeCountry(country);
        var currency = CurrencyFor(c);
        return new PlansResponse(c, currency, _options.TrialDays, Plans.All.Select(p => ToResponse(p, currency)).ToList());
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

        var currency = CurrencyFor(sub.BillingCountry);
        var price = _options.PriceFor(planId, currency) ?? throw new DomainException("Ese plan no tiene precio configurado.");

        if (sub.HasLiveProviderSubscription)
        {
            if (sub.Plan == planId && sub.EffectiveStatus(now) == SubscriptionStatus.Active) throw new DomainException($"Ya estás en el plan {plan.Name}.");
            var live = Gateway(sub.Provider) ?? throw new DomainException("El cobro de suscripciones no está disponible en este momento.");
            await live.ChangePlanAsync(sub.ProviderSubscriptionId!, planId, price, currency, ct);
            var from = sub.Plan;
            sub.ChangePlanLocally(planId, now);
            await AuditAsync(organizationId, "subscription_plan_changed", actor, new { from = from.ToString(), to = planId.ToString() }, ct);
            await _uow.SaveChangesAsync(ct);
            return new CheckoutResponse(null, true);
        }

        var gateway = Gateway(ProviderFor(sub.BillingCountry));
        if (gateway is null)
        {
            if (!_options.AllowSimulated)
                throw new DomainException("El cobro de suscripciones todavía no está habilitado. Escribinos y activamos tu plan a mano.");

            // Development/tests: no provider configured, activate right away for one month.
            sub.ApplyProviderState(BillingProvider.Simulated, $"sim_{Guid.NewGuid():N}", planId, SubscriptionStatus.Active,
                now.AddMonths(1), price, currency, null, now, now);
            await AuditAsync(organizationId, "subscription_simulated", actor, new { plan = planId.ToString() }, ct);
            await _uow.SaveChangesAsync(ct);
            return new CheckoutResponse(_links.Billing(), true);
        }

        var user = await _users.GetByIdAsync(userId, ct) ?? throw new UnauthorizedException("Sesión inválida.");
        var checkout = await gateway.CreateCheckoutAsync(new GatewayCheckoutRequest(
            organizationId, planId, price, currency, user.Email,
            $"RepairShop {plan.Name} (mensual)",
            _links.Billing(),
            $"{_links.ApiBase}/api/v1/billing/webhooks/{gateway.Provider.ToString().ToLowerInvariant()}"), ct);

        sub.BeginCheckout(planId, gateway.Provider, checkout.ProviderSubscriptionId, now);
        await AuditAsync(organizationId, "subscription_checkout_started", actor, new { plan = planId.ToString(), provider = gateway.Provider.ToString() }, ct);
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
            var gateway = Gateway(sub.Provider) ?? throw new DomainException("El cobro de suscripciones no está disponible en este momento.");
            await gateway.CancelAsync(sub.ProviderSubscriptionId!, ct);
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

    /// <summary>Mercado Pago preapproval notifications. Not accepted when the signature is invalid.</summary>
    public async Task<WebhookOutcome> HandleMercadoPagoWebhookAsync(MercadoPagoNotification n, CancellationToken ct)
    {
        var gateway = Gateway(BillingProvider.MercadoPago) as IMercadoPagoSubscriptionGateway;
        if (gateway is null) return WebhookOutcome.Rejected;
        if (!MercadoPagoSignature.IsValid(_options.MercadoPago.WebhookSecret, n.Signature, n.RequestId, n.DataId, _clock.UtcNow, TimeSpan.FromMinutes(15)))
        {
            _logger.LogWarning("Rejected Mercado Pago subscription webhook with an invalid signature.");
            return WebhookOutcome.Rejected;
        }

        string? preapprovalId = n.Type switch
        {
            "subscription_preapproval" or "preapproval" => n.DataId,
            "subscription_authorized_payment" or "authorized_payment" => await gateway.GetPreapprovalIdForPaymentAsync(n.DataId!, ct),
            _ => null
        };
        if (preapprovalId is null) return WebhookOutcome.Ignored; // other topics: acknowledged, nothing to do

        var state = await gateway.GetSubscriptionAsync(preapprovalId, ct);
        if (state is null) return WebhookOutcome.Ignored;
        return new WebhookOutcome(true, await _uow.RetryOnConflictAsync(c => ApplyAsync(BillingProvider.MercadoPago, state, c), ct, maxAttempts: 6));
    }

    /// <summary>Paddle Billing notifications (subscription.*). Not accepted when the signature is invalid.</summary>
    public async Task<WebhookOutcome> HandlePaddleWebhookAsync(string rawBody, string? signature, CancellationToken ct)
    {
        var gateway = Gateway(BillingProvider.Paddle) as IPaddleSubscriptionGateway;
        if (gateway is null) return WebhookOutcome.Rejected;
        if (!PaddleSignature.IsValid(_options.Paddle.WebhookSecret, signature, rawBody, _clock.UtcNow))
        {
            _logger.LogWarning("Rejected Paddle webhook with an invalid signature.");
            return WebhookOutcome.Rejected;
        }

        var state = gateway.ParseWebhook(rawBody);
        if (state is null) return WebhookOutcome.Ignored;
        return new WebhookOutcome(true, await _uow.RetryOnConflictAsync(c => ApplyAsync(BillingProvider.Paddle, state, c), ct, maxAttempts: 6));
    }

    /// <summary>
    /// Applies the provider state and returns the organization it belongs to (null when unknown). Providers send
    /// several events for one subscription at the same moment (Paddle: subscription.created and .activated), so
    /// callers retry on a row version conflict instead of failing the webhook.
    /// </summary>
    private async Task<Guid?> ApplyAsync(BillingProvider provider, GatewaySubscriptionState state, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var sub = await _subscriptions.GetByProviderIdAsync(provider, state.ProviderSubscriptionId, ct);
        if (sub is null && state.OrganizationId is { } orgId) sub = await _subscriptions.GetByOrganizationAsync(orgId, ct);
        if (sub is null)
        {
            _logger.LogWarning("{Provider} subscription {Id} does not match any organization.", provider, state.ProviderSubscriptionId);
            return null;
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
            try { await Gateway(provider)!.CancelAsync(previous, ct); }
            catch (Exception ex) { _logger.LogError(ex, "Could not cancel replaced {Provider} subscription {Id}.", provider, previous); }
        }
        return sub.OrganizationId;
    }

    // ---------------------------------------------------------------------------------------------

    private ISubscriptionGateway? Gateway(BillingProvider provider)
        => _gateways.FirstOrDefault(g => g.Provider == provider && g.IsConfigured);

    private async Task<Subscription> GetSubscriptionAsync(Guid organizationId, CancellationToken ct)
        => await _subscriptions.GetByOrganizationAsync(organizationId, ct) ?? throw new NotFoundException("La organización no tiene suscripción.");

    private async Task<int> CountActiveBranchesAsync(Guid organizationId, CancellationToken ct)
        => (await _shops.ListByOrganizationAsync(organizationId, ct)).Count(s => s.IsActive);

    private async Task<SubscriptionResponse> ToResponseAsync(Subscription sub, bool emailVerified, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var plan = sub.PlanDefinition;
        var currency = CurrencyFor(sub.BillingCountry);
        var checkoutAvailable = _options.AllowSimulated || Gateway(ProviderFor(sub.BillingCountry)) is not null;
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
            Plans.All.Select(p => ToResponse(p, currency)).ToList(),
            AdConversionService.PurchaseEventId(sub));
    }

    private PlanResponse ToResponse(PlanDefinition p, string currency)
        => new(p.Id.ToString(), p.Name, p.MaxBranches, p.Modules, _options.PriceFor(p.Id, currency), currency);

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

public interface IMercadoPagoSubscriptionGateway : ISubscriptionGateway
{
    /// <summary>Preapproval (subscription) id of a recurring charge notification.</summary>
    Task<string?> GetPreapprovalIdForPaymentAsync(string authorizedPaymentId, CancellationToken ct);
}

public interface IPaddleSubscriptionGateway : ISubscriptionGateway
{
    /// <summary>Subscription state from a (signature-checked) webhook body; null for events that don't matter.</summary>
    GatewaySubscriptionState? ParseWebhook(string rawBody);
}
