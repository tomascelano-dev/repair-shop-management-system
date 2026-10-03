namespace RepairShop.Application.Contracts;

public sealed record PlanResponse(
    string Id,
    string Name,
    int MaxBranches,
    IReadOnlyList<string> Modules,
    decimal? Price,
    string Currency);

public sealed record PlansResponse(string Country, string Currency, int TrialDays, IReadOnlyList<PlanResponse> Plans);

public sealed record SubscriptionResponse(
    string Plan,
    string PlanName,
    string Status,
    string Provider,
    string BillingCountry,
    bool HasFullAccess,
    DateTime TrialEndsAtUtc,
    int TrialDaysLeft,
    DateTime? CurrentPeriodEndsAtUtc,
    DateTime? CanceledAtUtc,
    decimal? Amount,
    string? Currency,
    string? PendingPlan,
    IReadOnlyList<string> Modules,
    int MaxBranches,
    int BranchesUsed,
    bool CanManageAtProvider,
    bool CheckoutAvailable,
    bool EmailVerified,
    IReadOnlyList<PlanResponse> Plans);

public sealed record CheckoutRequest(string Plan);

/// <summary>Url to send the customer to (null when the plan changed right away on a running subscription).</summary>
public sealed record CheckoutResponse(string? Url, bool Changed);

public sealed record SignupRequest(
    string ShopName,
    string OwnerName,
    string Email,
    string Password,
    string Country,
    string? TimeZone,
    bool AcceptTerms);

public sealed record VerifyEmailRequest(string Token);
