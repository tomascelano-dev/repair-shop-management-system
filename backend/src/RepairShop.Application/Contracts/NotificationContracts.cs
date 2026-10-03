using System.ComponentModel.DataAnnotations;
using RepairShop.Domain.Notifications;

namespace RepairShop.Application.Contracts;

public sealed record NotificationResponse(
    Guid Id,
    string Channel,
    string Recipient,
    string Title,
    string Body,
    string Status,
    int AttemptCount,
    DateTime? NextAttemptAtUtc,
    string? LastError,
    string? TemplateKey,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    string? Provider,
    DateTime? SentAtUtc,
    DateTime CreatedAtUtc);

public sealed record SendOrderMessageRequest(
    [Required, MinLength(3)] string TemplateKey,
    NotificationChannel? Channel = null,
    string? CustomBody = null);

public sealed record LogManualMessageRequest(
    [Required] NotificationChannel Channel,
    string? TemplateKey,
    [Required, MinLength(2)] string Body);

public sealed record NotificationResult(
    string TemplateKey,
    string Title,
    string Body,
    string? WhatsAppUrl,
    Guid? OutboxItemId,
    string? SkippedReason);
