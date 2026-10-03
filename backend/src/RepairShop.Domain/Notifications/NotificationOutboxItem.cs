using RepairShop.Domain.Common;

namespace RepairShop.Domain.Notifications;

public sealed class NotificationOutboxItem : IShopScoped
{
    public const int MaxAttempts = 6;

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }

    public NotificationChannel Channel { get; private set; }
    public string Recipient { get; private set; } = null!;

    public string Title { get; private set; } = null!;
    public string Body { get; private set; } = null!;

    public OutboxStatus Status { get; private set; } = OutboxStatus.Pending;

    public int AttemptCount { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public string? LastError { get; private set; }

    public string? CorrelationKey { get; private set; }
    public string? TemplateKey { get; private set; }

    public string? RelatedEntityType { get; private set; }
    public Guid? RelatedEntityId { get; private set; }

    // Provider that delivered the message ("twilio", "meta", "smtp", "simulated", "manual").
    public string? Provider { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public DateTime? SentAtUtc { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private NotificationOutboxItem() { }

    public NotificationOutboxItem(
        Guid shopId,
        NotificationChannel channel,
        string recipient,
        string title,
        string body,
        OutboxStatus status,
        string? correlationKey,
        string? relatedEntityType,
        Guid? relatedEntityId,
        DateTime nowUtc)
    {
        ShopId = shopId;
        Channel = channel;
        Recipient = (recipient ?? "").Trim();
        Title = (title ?? "").Trim();
        Body = (body ?? "").Trim();
        Status = status;
        CorrelationKey = string.IsNullOrWhiteSpace(correlationKey) ? null : correlationKey.Trim();
        RelatedEntityType = relatedEntityType?.Trim();
        RelatedEntityId = relatedEntityId;
        NextAttemptAtUtc = status == OutboxStatus.Pending ? nowUtc : null;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;

        if (!Enum.IsDefined(channel)) throw new DomainException("Canal de notificación inválido.");
        if (Recipient.Length < 2) throw new DomainException("Falta el destinatario del mensaje.");
        if (Title.Length < 2) throw new DomainException("Falta el título del mensaje.");
        if (Body.Length < 2) throw new DomainException("Falta el texto del mensaje.");
        if (Body.Length > 4000) Body = Body[..4000];
        if (Title.Length > 120) Title = Title[..120];
    }

    // Backward-compatible convenience constructor
    public NotificationOutboxItem(
        Guid shopId,
        NotificationChannel channel,
        string recipient,
        string title,
        string body,
        string? correlationKey,
        DateTime nowUtc)
        : this(
            shopId: shopId,
            channel: channel,
            recipient: recipient,
            title: title,
            body: body,
            status: OutboxStatus.Pending,
            correlationKey: correlationKey,
            relatedEntityType: null,
            relatedEntityId: null,
            nowUtc: nowUtc)
    {
    }

    /// <summary>
    /// Message sent by a person outside the system (e.g. wa.me link). Logged for history only.
    /// </summary>
    public static NotificationOutboxItem ManualLog(
        Guid shopId, NotificationChannel channel, string recipient, string title, string body,
        string? templateKey, string? relatedEntityType, Guid? relatedEntityId, Guid userId, DateTime nowUtc)
    {
        var item = new NotificationOutboxItem(shopId, channel, recipient, title, body, OutboxStatus.Sent, null, relatedEntityType, relatedEntityId, nowUtc)
        {
            TemplateKey = templateKey,
            Provider = "manual",
            SentAtUtc = nowUtc,
            CreatedByUserId = userId == Guid.Empty ? null : userId
        };
        return item;
    }

    public void SetOrigin(string? templateKey, Guid? createdByUserId)
    {
        TemplateKey = string.IsNullOrWhiteSpace(templateKey) ? null : templateKey.Trim();
        CreatedByUserId = createdByUserId == Guid.Empty ? null : createdByUserId;
    }

    public bool IsDue(DateTime nowUtc)
        => Status is OutboxStatus.Pending or OutboxStatus.Failed && (NextAttemptAtUtc is null || NextAttemptAtUtc <= nowUtc);

    public void MarkProcessing(DateTime nowUtc)
    {
        Status = OutboxStatus.Processing;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkSent(DateTime nowUtc)
        => MarkSent(Provider ?? "unknown", null, nowUtc);

    public void MarkSent(string provider, string? providerMessageId, DateTime nowUtc)
    {
        Status = OutboxStatus.Sent;
        Provider = provider;
        ProviderMessageId = providerMessageId is { Length: > 200 } ? providerMessageId[..200] : providerMessageId;
        SentAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        LastError = null;
        NextAttemptAtUtc = null;
        AttemptCount += 1;
    }

    public void MarkFailed(string error, DateTime? nextAttemptAtUtc, DateTime nowUtc)
    {
        Status = OutboxStatus.Failed;
        AttemptCount += 1;
        LastError = Truncate(error, 4000);
        NextAttemptAtUtc = nextAttemptAtUtc;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Registers a failed attempt and schedules the next one with exponential backoff
    /// (1m, 5m, 15m, 1h, 6h). After MaxAttempts the message is given up (Cancelled).
    /// </summary>
    public void RegisterFailure(string error, bool permanent, DateTime nowUtc)
    {
        if (permanent || AttemptCount + 1 >= MaxAttempts)
        {
            AttemptCount += 1;
            Status = OutboxStatus.Cancelled;
            LastError = Truncate(error, 4000);
            NextAttemptAtUtc = null;
            UpdatedAtUtc = nowUtc;
            return;
        }

        var delays = new[] { TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1), TimeSpan.FromHours(6) };
        var delay = delays[Math.Min(AttemptCount, delays.Length - 1)];
        MarkFailed(error, nowUtc.Add(delay), nowUtc);
    }

    public void Retry(DateTime nowUtc)
    {
        if (Status is OutboxStatus.Sent) throw new DomainException("El mensaje ya fue enviado.");
        Status = OutboxStatus.Pending;
        NextAttemptAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkCancelled(DateTime nowUtc)
    {
        if (Status is OutboxStatus.Sent) throw new DomainException("El mensaje ya fue enviado.");
        Status = OutboxStatus.Cancelled;
        NextAttemptAtUtc = null;
        UpdatedAtUtc = nowUtc;
    }

    private static string Truncate(string? value, int max)
    {
        value = (value ?? "").Trim();
        return value.Length > max ? value[..max] : value;
    }
}
