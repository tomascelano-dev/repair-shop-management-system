namespace RepairShop.Domain.RepairOrders;

/// <summary>
/// Facts the state machine needs to validate a transition that live outside the order aggregate
/// (quotes, QA checklist, payments, shop settings).
/// </summary>
public sealed record StatusTransitionContext(
    bool HasApprovedQuote = false,
    bool QaPassed = false,
    decimal BalanceDue = 0m,
    bool AllowUnpaidDelivery = false,
    int DefaultWarrantyDays = 0,
    string? Reason = null);
