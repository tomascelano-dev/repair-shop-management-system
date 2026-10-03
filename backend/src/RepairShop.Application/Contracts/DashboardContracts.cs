namespace RepairShop.Application.Contracts;

public sealed record DashboardSummaryResponse(
    Guid ShopId,
    int TotalOrders,
    int OpenOrders,
    int ReadyOrders,
    int DeliveredOrders,
    int CancelledOrders,
    decimal TotalPaymentsAmount,
    string? PaymentsCurrency,
    DateTime GeneratedAtUtc,
    IReadOnlyDictionary<string, int>? StatusCounts = null,
    int OverdueOrders = 0,
    int StaleOrders = 0,
    int ReadyNotPickedUp = 0,
    int QuotesPendingDecision = 0,
    int LowStockItems = 0,
    int MyOpenOrders = 0,
    RevenueSummary? Today = null,
    RevenueSummary? Month = null,
    IReadOnlyList<TechnicianWorkload>? Technicians = null,
    bool CashSessionOpen = false,
    string? ReportingCurrency = null,
    IReadOnlyList<string>? MissingRates = null,
    IReadOnlyList<CurrencyAmount>? TotalPaymentsByCurrency = null);

public sealed record RevenueSummary(IReadOnlyList<CurrencyAmount> ByCurrency, decimal? Converted, string Currency);

public sealed record TechnicianWorkload(Guid UserId, string Name, int OpenOrders, int OverdueOrders);

public sealed record RevenuePoint(DateOnly Date, decimal? Converted, IReadOnlyList<CurrencyAmount> ByCurrency);

public sealed record ConsolidatedBranchSummary(Guid ShopId, string ShopName, int OpenOrders, int ReadyOrders, int OverdueOrders, int LowStockItems, RevenueSummary Month);

public sealed record ConsolidatedDashboardResponse(string ReportingCurrency, IReadOnlyList<ConsolidatedBranchSummary> Branches, decimal? MonthTotalConverted, IReadOnlyList<string> MissingRates);
