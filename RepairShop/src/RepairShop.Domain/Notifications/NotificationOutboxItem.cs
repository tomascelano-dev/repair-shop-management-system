using RepairShop.Domain.Common;

namespace RepairShop.Domain.Notifications;

public sealed class NotificationOutboxItem
{
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

    public string? RelatedEntityType { get; private set; }
    public Guid? RelatedEntityId { get; private set; }

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
        CorrelationKey = correlationKey?.Trim();
        RelatedEntityType = relatedEntityType?.Trim();
        RelatedEntityId = relatedEntityId;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;

        if (Recipient.Length < 2) throw new DomainException("Outbox recipient is required.");
        if (Title.Length < 2) throw new DomainException("Outbox title is required.");
        if (Body.Length < 2) throw new DomainException("Outbox body is required.");
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

    public void MarkProcessing(DateTime nowUtc)
    {
        Status = OutboxStatus.Processing;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkSent(DateTime nowUtc)
    {
        Status = OutboxStatus.Sent;
        UpdatedAtUtc = nowUtc;
        LastError = null;
        NextAttemptAtUtc = null;
    }

    public void MarkFailed(string error, DateTime? nextAttemptAtUtc, DateTime nowUtc)
    {
        Status = OutboxStatus.Failed;
        AttemptCount += 1;
        LastError = (error ?? "").Trim();
        NextAttemptAtUtc = nextAttemptAtUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkCancelled(DateTime nowUtc)
    {
        Status = OutboxStatus.Cancelled;
        UpdatedAtUtc = nowUtc;
    }
}
