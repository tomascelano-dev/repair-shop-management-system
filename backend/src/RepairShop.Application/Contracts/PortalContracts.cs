using System.ComponentModel.DataAnnotations;

namespace RepairShop.Application.Contracts;

public sealed record PublicShopInfo(string Name, string? Phone, string? WhatsAppUrl, string? Address, string? City, string? PickupHours, string? LogoUrl);

public sealed record PublicOrderInfo(
    string Code,
    string Status,
    string StatusLabel,
    string CustomerFirstName,
    string Device,
    string IssueDescription,
    DateTime CreatedAtUtc,
    DateTime? PromisedAtUtc,
    DateTime? ReadyAtUtc,
    DateTime? DeliveredAtUtc,
    bool IsWarrantyClaim);

public sealed record PublicTimelineItem(string Status, string Label, DateTime AtUtc);

public sealed record PublicNote(string Body, DateTime AtUtc);

public sealed record PublicQuoteItem(string Description, decimal Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record PublicQuote(
    Guid Id,
    int Version,
    string Status,
    string StatusLabel,
    string Currency,
    IReadOnlyList<PublicQuoteItem> Items,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal Total,
    DateTime? ValidUntilUtc,
    int? WarrantyDays,
    string? Notes,
    bool CanDecide);

public sealed record PublicMoney(string Currency, decimal Total, decimal Paid, decimal Balance);

public sealed record PublicWarranty(int Days, DateTime? ExpiresAtUtc, bool Active);

public sealed record PublicTrackingResponse(
    PublicShopInfo Shop,
    PublicOrderInfo Order,
    IReadOnlyList<PublicTimelineItem> Timeline,
    IReadOnlyList<PublicNote> Notes,
    PublicQuote? Quote,
    PublicMoney? Money,
    bool CanPayOnline,
    PublicWarranty? Warranty,
    bool CanLeaveFeedback,
    bool FeedbackSubmitted);

public sealed record PublicDecisionRequest(string? Note);

public sealed record PublicFeedbackRequest([Range(1, 5)] int Score, string? Comment);

public sealed record PublicFeedbackResponse(bool Saved, string? GoogleReviewUrl);

public sealed record PublicPaymentLinkResponse(string Url, decimal Amount, string Currency);
