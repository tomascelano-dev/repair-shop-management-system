using System.ComponentModel.DataAnnotations;
using RepairShop.Domain.Notifications;
using RepairShop.Domain.Quotes;

namespace RepairShop.Application.Contracts;

public sealed record QuoteItemRequest(
    QuoteItemKind Kind,
    [Required, MinLength(2)] string Description,
    decimal Quantity,
    decimal UnitPrice,
    Guid? InventoryItemId = null,
    int? WarrantyDays = null);

public sealed record SaveQuoteRequest(
    [Required, MinLength(3)] string Currency,
    [Required] IReadOnlyList<QuoteItemRequest> Items,
    decimal DiscountAmount = 0,
    int? WarrantyDays = null,
    string? Notes = null);

public sealed record SendQuoteRequest(int? ValidDays = null, bool EnqueueOutbox = true, NotificationChannel? Channel = null);

public sealed record DecideQuoteRequest(string? Note = null);

public sealed record QuoteItemResponse(
    Guid Id,
    int Position,
    string Kind,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    Guid? InventoryItemId,
    int? WarrantyDays);

public sealed record QuoteResponse(
    Guid Id,
    Guid RepairOrderId,
    int Version,
    string Status,
    string StatusLabel,
    string Currency,
    IReadOnlyList<QuoteItemResponse> Items,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal Total,
    DateTime? ValidUntilUtc,
    int? WarrantyDays,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? SentAtUtc,
    DateTime? DecidedAtUtc,
    string? DecisionSource,
    string? DecisionNote,
    Guid? DecidedByUserId);

public sealed record QuoteActionResponse(QuoteResponse Quote, string? SuggestedMessage, string? WhatsAppUrl, Guid? OutboxItemId, IReadOnlyList<string> Warnings);
