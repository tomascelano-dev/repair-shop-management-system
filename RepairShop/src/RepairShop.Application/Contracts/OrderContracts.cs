using System.ComponentModel.DataAnnotations;
using RepairShop.Domain.Notifications;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.Contracts;

public sealed record RepairOrderResponse(
    Guid Id,
    Guid ShopId,
    Guid CustomerId,
    Guid DeviceId,
    string IssueDescription,
    string? Notes,
    string Status,
    decimal? QuoteAmount,
    string? QuoteCurrency,
    Guid? QuoteUpdatedByUserId,
    DateTime? QuoteUpdatedAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public sealed record RepairOrderCreateRequest(
    [Required] Guid CustomerId,
    [Required] Guid DeviceId,
    [Required, MinLength(5)] string IssueDescription,
    string? Notes
);

public sealed record RepairOrderUpdateRequest(
    [Required, MinLength(5)] string IssueDescription,
    string? Notes
);

public sealed record SetOrderQuoteRequest(
    decimal? Amount,
    string? Currency
);

public sealed record ChangeOrderStatusRequest(
    [Required] RepairOrderStatus Status,
    bool EnqueueOutbox = true,
    NotificationChannel Channel = NotificationChannel.WhatsApp
);

public sealed record ChangeOrderStatusResponse(
    Guid OrderId,
    RepairOrderStatus FromStatus,
    RepairOrderStatus ToStatus,
    string SuggestedMessage,
    Guid? OutboxItemId
);

public sealed record OrderStatusHistoryResponse(
    Guid Id,
    RepairOrderStatus FromStatus,
    RepairOrderStatus ToStatus,
    Guid ChangedByUserId,
    DateTime ChangedAtUtc
);

public sealed record RepairOrderNoteResponse(
    Guid Id,
    string Body,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc
);

public sealed record CreateRepairOrderNoteRequest(
    [Required, MinLength(2)] string Body
);

public sealed record RepairOrderAttachmentResponse(
    Guid Id,
    string Url,
    string? Label,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc
);

public sealed record CreateRepairOrderAttachmentRequest(
    [Required, MinLength(8)] string Url,
    string? Label
);

public sealed record CreateRepairOrderPaymentRequest(
    [Required] decimal Amount,
    [Required, MinLength(3)] string Currency,
    [Required] PaymentMethod Method,
    string? Reference
);

public sealed record RepairOrderPaymentResponse(
    Guid Id,
    Guid RepairOrderId,
    decimal Amount,
    string Currency,
    PaymentMethod Method,
    string? Reference,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc
);

public sealed record UpdateRepairOrderChecklistRequest(
    bool ScreenOk,
    bool CamerasOk,
    bool SpeakersOk,
    bool MicrophoneOk,
    bool ButtonsOk,
    bool FaceIdOk,
    bool FingerprintOk,
    CloudLockStatus CloudLock,
    int? BatteryPercent,
    string? CosmeticNotes
);

public sealed record RepairOrderChecklistResponse(
    Guid Id,
    Guid RepairOrderId,
    bool ScreenOk,
    bool CamerasOk,
    bool SpeakersOk,
    bool MicrophoneOk,
    bool ButtonsOk,
    bool FaceIdOk,
    bool FingerprintOk,
    CloudLockStatus CloudLock,
    int? BatteryPercent,
    string? CosmeticNotes,
    Guid UpdatedByUserId,
    DateTime UpdatedAtUtc
);

public sealed record AuditEventResponse(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Action,
    Guid? ActorUserId,
    string? ActorEmail,
    string? DataJson,
    DateTime CreatedAtUtc
);

public sealed record RenderOrderMessageRequest(
    [Required, MinLength(3)] string TemplateKey
);

public sealed record MessagePreviewResponse(
    string TemplateKey,
    string Title,
    string Body
);
