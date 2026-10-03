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
    DateTime UpdatedAtUtc,
    // --- extended
    int OrderNumber,
    string Code,
    string StatusLabel,
    string? IssueCategory,
    string Priority,
    Guid? AssignedTechnicianId,
    string? AssignedTechnicianName,
    DateTime? PromisedAtUtc,
    bool IsOverdue,
    string CustomerName,
    string CustomerPhone,
    string DeviceLabel,
    string? DeviceImei,
    string Currency,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceDue,
    decimal ExtraCharges,
    bool HasApprovedQuote,
    bool QaPassed,
    int? WarrantyDays,
    DateTime? WarrantyExpiresAtUtc,
    bool UnderWarranty,
    bool IsWarrantyClaim,
    Guid? WarrantyOfOrderId,
    DateTime LastStatusChangeAtUtc,
    DateTime? ReadyAtUtc,
    DateTime? DeliveredAtUtc,
    DateTime? CancelledAtUtc,
    string? CancellationReason,
    string UnlockMethod,
    bool HasUnlockSecret,
    bool HasReceptionSignature,
    string? ReceptionSignedByName,
    bool HasDeliverySignature,
    string? DeliverySignedByName,
    string TrackingUrl,
    IReadOnlyList<string> AllowedNextStatuses,
    int PhotosCount
);

public sealed record RepairOrderCreateRequest(
    [Required] Guid CustomerId,
    [Required] Guid DeviceId,
    [Required, MinLength(5)] string IssueDescription,
    string? Notes,
    string? IssueCategory = null,
    RepairOrderPriority Priority = RepairOrderPriority.Normal,
    Guid? AssignedTechnicianId = null,
    DateTime? PromisedAtUtc = null,
    int? WarrantyDays = null,
    bool SendReceivedMessage = false
);

public sealed record RepairOrderUpdateRequest(
    [Required, MinLength(5)] string IssueDescription,
    string? Notes,
    string? IssueCategory = null,
    int? WarrantyDays = null
);

public sealed record PlanOrderRequest(
    Guid? AssignedTechnicianId,
    RepairOrderPriority Priority,
    DateTime? PromisedAtUtc
);

public sealed record SetOrderQuoteRequest(
    decimal? Amount,
    string? Currency
);

public sealed record ChangeOrderStatusRequest(
    [Required] RepairOrderStatus Status,
    bool EnqueueOutbox = true,
    NotificationChannel? Channel = null,
    string? Reason = null,
    bool ForceUnpaidDelivery = false
);

public sealed record ChangeOrderStatusResponse(
    Guid OrderId,
    RepairOrderStatus FromStatus,
    RepairOrderStatus ToStatus,
    string SuggestedMessage,
    Guid? OutboxItemId,
    string? WhatsAppUrl = null
);

public sealed record OrderStatusHistoryResponse(
    Guid Id,
    RepairOrderStatus FromStatus,
    RepairOrderStatus ToStatus,
    Guid ChangedByUserId,
    DateTime ChangedAtUtc,
    string? FromLabel = null,
    string? ToLabel = null,
    string? ChangedByName = null,
    string? Reason = null
);

public sealed record RepairOrderNoteResponse(
    Guid Id,
    string Body,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    bool IsPublic = false,
    string? CreatedByName = null
);

public sealed record CreateRepairOrderNoteRequest(
    [Required, MinLength(2)] string Body,
    bool IsPublic = false
);

public sealed record RepairOrderAttachmentResponse(
    Guid Id,
    string? Url,
    string? Label,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    string Kind = "Link",
    Guid? FileId = null,
    string? FileName = null,
    string? ContentType = null,
    long? SizeBytes = null
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

public sealed record RefundOrderPaymentRequest(
    [Required] decimal Amount,
    [Required] PaymentMethod Method,
    [Required, MinLength(3)] string Reason
);

public sealed record RepairOrderPaymentResponse(
    Guid Id,
    Guid RepairOrderId,
    decimal Amount,
    string Currency,
    PaymentMethod Method,
    string? Reference,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    string Type = "Payment",
    bool IsDeposit = false,
    Guid? RefundOfPaymentId = null,
    decimal RefundedAmount = 0
);

public sealed record OrderFinancialsResponse(
    string Currency,
    decimal AgreedPrice,
    decimal ExtraCharges,
    decimal Total,
    decimal Paid,
    decimal BalanceDue,
    bool HasAgreedPrice);

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

public sealed record UpdateQaChecklistRequest(
    bool? PowersOn,
    bool? ScreenOk,
    bool? TouchOk,
    bool? CamerasOk,
    bool? AudioOk,
    bool? MicrophoneOk,
    bool? ButtonsOk,
    bool? ChargingOk,
    bool? ConnectivityOk,
    bool? BiometricsOk,
    int? BatteryHealthPercent,
    string? Notes,
    bool Approve
);

public sealed record QaChecklistResponse(
    Guid Id,
    Guid RepairOrderId,
    bool? PowersOn,
    bool? ScreenOk,
    bool? TouchOk,
    bool? CamerasOk,
    bool? AudioOk,
    bool? MicrophoneOk,
    bool? ButtonsOk,
    bool? ChargingOk,
    bool? ConnectivityOk,
    bool? BiometricsOk,
    int? BatteryHealthPercent,
    string? Notes,
    bool Passed,
    Guid CheckedByUserId,
    DateTime CheckedAtUtc
);

public sealed record SetUnlockSecretRequest([Required] UnlockMethod Method, string? Value);

public sealed record UnlockSecretResponse(string Method, string? Value);

public enum SignatureKind { Reception = 0, Delivery = 1 }

public sealed record SaveSignatureRequest(
    [Required] SignatureKind Kind,
    [Required, MinLength(2)] string SignerName,
    [Required] string ImageDataUrl
);

public sealed record WarrantyClaimRequest(
    [Required, MinLength(5)] string IssueDescription,
    string? Notes
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
    string Body,
    string? WhatsAppUrl = null,
    string? Recipient = null
);

public sealed record OrderBoardResponse(IReadOnlyList<OrderBoardColumn> Columns);

public sealed record OrderBoardColumn(string Status, string Label, int Count, IReadOnlyList<OrderCardResponse> Items);

public sealed record OrderCardResponse(
    Guid Id,
    string Code,
    string Status,
    string CustomerName,
    string DeviceLabel,
    string IssueDescription,
    string Priority,
    Guid? AssignedTechnicianId,
    string? AssignedTechnicianName,
    DateTime? PromisedAtUtc,
    bool IsOverdue,
    bool IsStale,
    int AgeDays,
    DateTime LastStatusChangeAtUtc,
    bool IsWarrantyClaim,
    decimal BalanceDue,
    string Currency);
