using Microsoft.Extensions.Logging;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Application.RepairOrders;
using RepairShop.Domain.Common;
using RepairShop.Domain.Notifications;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.Notifications;

/// <summary>
/// Renders order messages and queues them in the outbox (sent by the background dispatcher).
/// Respects shop settings and the customer's consent. Always returns a wa.me link so staff can
/// send the message manually when no provider is configured.
/// </summary>
public sealed class NotificationService
{
    public const string EntityTypeRepairOrder = "repair_order";

    private readonly RenderOrderMessageService _renderer;
    private readonly IShopRepository _shops;
    private readonly ICustomerRepository _customers;
    private readonly INotificationOutboxRepository _outbox;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        RenderOrderMessageService renderer,
        IShopRepository shops,
        ICustomerRepository customers,
        INotificationOutboxRepository outbox,
        IUnitOfWork uow,
        IDateTimeProvider clock,
        ILogger<NotificationService> logger)
    {
        _renderer = renderer;
        _shops = shops;
        _customers = customers;
        _outbox = outbox;
        _uow = uow;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Renders <paramref name="templateKey"/> for the order and (optionally) queues it.
    /// Saves on its own; failures to queue are logged and never thrown (best-effort side effect).
    /// </summary>
    public async Task<NotificationResult> NotifyOrderAsync(
        Guid shopId,
        RepairOrder order,
        string templateKey,
        string correlationKey,
        bool enqueue,
        NotificationChannel? channel,
        Actor actor,
        CancellationToken ct,
        string? customBody = null)
    {
        var preview = await _renderer.RenderAsync(shopId, order.Id, templateKey, allowFallback: true, ct);
        var body = string.IsNullOrWhiteSpace(customBody) ? preview.Body : customBody.Trim();
        var waUrl = preview.WhatsAppUrl is null ? null : RebuildWhatsAppUrl(preview.WhatsAppUrl, body);

        if (!enqueue) return new NotificationResult(preview.TemplateKey, preview.Title, body, waUrl, null, "not_requested");

        var shop = await _shops.GetByIdAsync(shopId, ct);
        if (shop is null || !shop.NotificationsEnabled)
            return new NotificationResult(preview.TemplateKey, preview.Title, body, waUrl, null, "notifications_disabled");

        var customer = await _customers.GetByIdAsync(shopId, order.CustomerId, ct);
        if (customer is null) return new NotificationResult(preview.TemplateKey, preview.Title, body, waUrl, null, "no_customer");
        if (!customer.NotificationsOptIn) return new NotificationResult(preview.TemplateKey, preview.Title, body, waUrl, null, "customer_opted_out");

        var ch = channel ?? shop.DefaultNotificationChannel;
        var recipient = ch switch
        {
            NotificationChannel.Email => customer.Email,
            _ => PhoneNumber.ToWhatsAppDigits(customer.Phone, shop.PhoneCountryCode)
        };
        if (string.IsNullOrWhiteSpace(recipient))
            return new NotificationResult(preview.TemplateKey, preview.Title, body, waUrl, null, ch == NotificationChannel.Email ? "no_email" : "no_phone");

        if (await _outbox.ExistsAsync(shopId, correlationKey, ct))
            return new NotificationResult(preview.TemplateKey, preview.Title, body, waUrl, null, "duplicate");

        var item = new NotificationOutboxItem(shopId, ch, recipient, preview.Title, body, OutboxStatus.Pending,
            correlationKey, EntityTypeRepairOrder, order.Id, _clock.UtcNow);
        item.SetOrigin(templateKey, actor.IsSystem ? null : actor.UserId);

        try
        {
            await _outbox.AddAsync(item, ct);
            await _uow.SaveChangesAsync(ct);
            return new NotificationResult(preview.TemplateKey, preview.Title, body, waUrl, item.Id, null);
        }
        catch (ConflictException ex)
        {
            _uow.Detach(item);
            _logger.LogWarning(ex, "Could not queue message {TemplateKey} for order {OrderId}.", templateKey, order.Id);
            return new NotificationResult(preview.TemplateKey, preview.Title, body, waUrl, null, "duplicate");
        }
    }

    /// <summary>Records a message sent manually by staff (e.g. through the wa.me link).</summary>
    public async Task<NotificationResponse> LogManualAsync(Guid shopId, RepairOrder order, LogManualMessageRequest req, Actor actor, CancellationToken ct)
    {
        var shop = await _shops.GetByIdAsync(shopId, ct) ?? throw new NotFoundException("Sucursal no encontrada.");
        var customer = await _customers.GetByIdAsync(shopId, order.CustomerId, ct) ?? throw new NotFoundException("Cliente no encontrado.");
        var recipient = req.Channel == NotificationChannel.Email
            ? customer.Email ?? throw new DomainException("El cliente no tiene email.")
            : PhoneNumber.ToWhatsAppDigits(customer.Phone, shop.PhoneCountryCode);

        var item = NotificationOutboxItem.ManualLog(shopId, req.Channel, recipient, "Mensaje manual", req.Body, req.TemplateKey,
            EntityTypeRepairOrder, order.Id, actor.UserId, _clock.UtcNow);
        await _outbox.AddAsync(item, ct);
        await _uow.SaveChangesAsync(ct);
        return ToResponse(item);
    }

    public async Task<NotificationResponse> RetryAsync(Guid shopId, Guid id, CancellationToken ct)
    {
        var item = await _outbox.GetByIdAsync(shopId, id, ct) ?? throw new NotFoundException("Mensaje no encontrado.");
        item.Retry(_clock.UtcNow);
        await _uow.SaveChangesAsync(ct);
        return ToResponse(item);
    }

    public async Task<NotificationResponse> CancelAsync(Guid shopId, Guid id, CancellationToken ct)
    {
        var item = await _outbox.GetByIdAsync(shopId, id, ct) ?? throw new NotFoundException("Mensaje no encontrado.");
        item.MarkCancelled(_clock.UtcNow);
        await _uow.SaveChangesAsync(ct);
        return ToResponse(item);
    }

    public static NotificationResponse ToResponse(NotificationOutboxItem x)
        => new(x.Id, x.Channel.ToString(), x.Recipient, x.Title, x.Body, x.Status.ToString(), x.AttemptCount, x.NextAttemptAtUtc,
            x.LastError, x.TemplateKey, x.RelatedEntityType, x.RelatedEntityId, x.Provider, x.SentAtUtc, x.CreatedAtUtc);

    private static string RebuildWhatsAppUrl(string url, string body)
    {
        var q = url.IndexOf("?text=", StringComparison.Ordinal);
        var basePart = q >= 0 ? url[..q] : url;
        return $"{basePart}?text={Uri.EscapeDataString(body)}";
    }
}
