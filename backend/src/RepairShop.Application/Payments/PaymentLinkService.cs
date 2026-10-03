using Microsoft.Extensions.Logging;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Application.RepairOrders;
using RepairShop.Domain.Common;
using RepairShop.Domain.Payments;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Users;

namespace RepairShop.Application.Payments;

/// <summary>
/// Mercado Pago Checkout Pro links to collect order balances (from the counter or the customer portal),
/// reconciled automatically through signed webhooks.
/// </summary>
public sealed class PaymentLinkService
{
    private const string EntityType = RepairOrderService.EntityType;
    private const string ReferencePrefix = "pl:";

    private readonly IPaymentLinkRepository _links;
    private readonly IRepairOrderRepository _orders;
    private readonly IShopIntegrationRepository _integrations;
    private readonly ICustomerRepository _customers;
    private readonly IUserRepository _users;
    private readonly IMercadoPagoClient _mp;
    private readonly ISecretProtector _protector;
    private readonly OrderPaymentService _payments;
    private readonly IAppLinks _appLinks;
    private readonly IAuditLog _audit;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<PaymentLinkService> _logger;

    public PaymentLinkService(
        IPaymentLinkRepository links,
        IRepairOrderRepository orders,
        IShopIntegrationRepository integrations,
        ICustomerRepository customers,
        IUserRepository users,
        IMercadoPagoClient mp,
        ISecretProtector protector,
        OrderPaymentService payments,
        IAppLinks appLinks,
        IAuditLog audit,
        IUnitOfWork uow,
        IDateTimeProvider clock,
        ILogger<PaymentLinkService> logger)
    {
        _links = links;
        _orders = orders;
        _integrations = integrations;
        _customers = customers;
        _users = users;
        _mp = mp;
        _protector = protector;
        _payments = payments;
        _appLinks = appLinks;
        _audit = audit;
        _uow = uow;
        _clock = clock;
        _logger = logger;
    }

    public async Task<bool> IsEnabledAsync(Guid shopId, CancellationToken ct)
    {
        var integration = await _integrations.GetAsync(shopId, ct);
        return integration is { MercadoPagoEnabled: true, MercadoPagoAccessTokenProtected: not null };
    }

    public async Task<List<PaymentLinkResponse>> ListForOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => (await _links.ListByEntityAsync(shopId, EntityType, orderId, ct)).Select(ToResponse).ToList();

    public async Task<PaymentLinkResponse> CreateForOrderAsync(Guid shopId, Guid orderId, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var integration = await _integrations.GetAsync(shopId, ct);
        if (integration is not { MercadoPagoEnabled: true, MercadoPagoAccessTokenProtected: not null })
            throw new DomainException("Mercado Pago no está configurado para esta sucursal.");

        var order = await _orders.GetByIdAsync(shopId, orderId, ct) ?? throw new NotFoundException("Orden no encontrada.");
        if (order.Status == RepairOrderStatus.Cancelled) throw new DomainException("La orden está cancelada.");

        var money = await _payments.GetFinancialsAsync(shopId, orderId, ct);
        if (!money.HasAgreedPrice) throw new DomainException("La orden todavía no tiene un precio acordado.");
        if (money.BalanceDue <= 0) throw new DomainException("La orden no tiene saldo pendiente.");

        // Reuse a still valid link for the same amount (customers often open the portal several times).
        var existing = (await _links.ListByEntityAsync(shopId, EntityType, orderId, ct))
            .FirstOrDefault(l => l.IsUsable(now) && l.Amount == money.BalanceDue && l.Currency == money.Currency && l.ExpiresAtUtc > now.AddHours(1));
        if (existing is not null) return ToResponse(existing);

        foreach (var stale in (await _links.ListByEntityAsync(shopId, EntityType, orderId, ct)).Where(l => l.Status == PaymentLinkStatus.Pending))
            stale.Cancel(now);

        var link = new PaymentLink(shopId, EntityType, orderId, money.BalanceDue, money.Currency, $"Orden {order.Code}", now.AddDays(3), actor.IsSystem ? null : actor.UserId, now);
        var customer = await _customers.GetByIdAsync(shopId, order.CustomerId, ct);
        var preference = await _mp.CreatePreferenceAsync(
            _protector.Unprotect(integration.MercadoPagoAccessTokenProtected!),
            new MpPreferenceRequest(link.Title, link.Amount, link.Currency, ReferencePrefix + link.Id,
                $"{_appLinks.ApiBase.TrimEnd('/')}/api/v1/webhooks/mercadopago/{shopId}",
                _appLinks.Tracking(order.PublicToken), link.ExpiresAtUtc, customer?.Email), ct);

        link.AttachProviderData(preference.Id, preference.InitPoint, now);
        await _links.AddAsync(link, ct);
        await _audit.AddAsync(shopId, EntityType, orderId, "payment_link_created", actor, new { link.Id, link.Amount, link.Currency }, ct);
        await _uow.SaveChangesAsync(ct);
        return ToResponse(link);
    }

    /// <summary>Processes a Mercado Pago notification. Returns false when the signature is invalid.</summary>
    public async Task<bool> HandleWebhookAsync(Guid shopId, MercadoPagoNotification n, CancellationToken ct)
    {
        var integration = await _integrations.GetAsync(shopId, ct);
        if (integration?.MercadoPagoAccessTokenProtected is null) return true; // nothing to do, but don't make MP retry forever

        if (integration.MercadoPagoWebhookSecretProtected is not null)
        {
            var secret = _protector.Unprotect(integration.MercadoPagoWebhookSecretProtected);
            if (!MercadoPagoSignature.IsValid(secret, n.Signature, n.RequestId, n.DataId, _clock.UtcNow, TimeSpan.FromMinutes(30)))
            {
                _logger.LogWarning("Rejected Mercado Pago webhook with invalid signature for shop {ShopId}.", shopId);
                return false;
            }
        }

        if (!string.Equals(n.Type, "payment", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(n.DataId)) return true;

        var payment = await _mp.GetPaymentAsync(_protector.Unprotect(integration.MercadoPagoAccessTokenProtected), n.DataId, ct);
        if (payment is null || !string.Equals(payment.Status, "approved", StringComparison.OrdinalIgnoreCase)) return true;
        if (payment.ExternalReference is null || !payment.ExternalReference.StartsWith(ReferencePrefix, StringComparison.Ordinal)) return true;
        if (!Guid.TryParse(payment.ExternalReference[ReferencePrefix.Length..], out var linkId)) return true;

        var link = await _links.GetByIdAsync(linkId, ct);
        if (link is null || link.ShopId != shopId) return true;
        if (link.Status == PaymentLinkStatus.Paid) return true; // idempotent

        var now = _clock.UtcNow;
        var actor = await ResolveActorAsync(shopId, link, ct);
        var amount = Money.Round(payment.TransactionAmount);
        try
        {
            await _payments.AddAsync(shopId, link.EntityId,
                new CreateRepairOrderPaymentRequest(amount, payment.Currency, PaymentMethod.MercadoPago, $"MP #{payment.Id}"),
                actor, ct, externalPaymentId: $"mp:{payment.Id}");
        }
        catch (ConflictException)
        {
            // Already registered by a previous delivery of the same notification.
        }
        catch (DomainException ex)
        {
            // E.g. the balance changed meanwhile: keep the money traceable for manual review.
            _logger.LogWarning(ex, "Mercado Pago payment {PaymentId} could not be applied to order {OrderId}.", payment.Id, link.EntityId);
            await _audit.AddAsync(shopId, EntityType, link.EntityId, "payment_link_unapplied", actor, new { paymentId = payment.Id, amount, error = ex.Message }, ct);
        }

        link.MarkPaid(payment.Id, now);
        await _uow.SaveChangesAsync(ct);
        return true;
    }

    private async Task<Actor> ResolveActorAsync(Guid shopId, PaymentLink link, CancellationToken ct)
    {
        if (link.CreatedByUserId is not null) return new Actor(link.CreatedByUserId.Value, "mercadopago", "System");
        var admin = (await _users.ListForShopAsync(shopId, ct)).FirstOrDefault(u => u.IsActive && u.Role == UserRole.Admin);
        return new Actor(admin?.Id ?? Guid.Empty, "mercadopago", "System");
    }

    private static PaymentLinkResponse ToResponse(PaymentLink l)
        => new(l.Id, l.Provider, l.Url ?? "", l.Amount, l.Currency, l.Status.ToString(), l.ExpiresAtUtc, l.CreatedAtUtc, l.PaidAtUtc);
}
