namespace RepairShop.Application.Contracts;

public sealed record ReportPeriod(DateTime FromUtc, DateTime ToUtc, string ReportingCurrency, IReadOnlyList<string> RateSources, IReadOnlyList<string> MissingRates);

public sealed record RevenueRow(string Period, string Currency, string Method, decimal Orders, decimal Sales, decimal Refunds, decimal Net, decimal? NetConverted);

public sealed record RevenueReport(ReportPeriod Period, IReadOnlyList<RevenueRow> Rows, IReadOnlyList<CurrencyAmount> TotalsByCurrency, decimal? TotalConverted,
    IReadOnlyList<CurrencyAmount> ByMethod);

public sealed record MarginRow(string Kind, Guid Id, string Code, string Description, DateTime DateUtc, string Currency, decimal Revenue, decimal Cost, decimal Margin, double? MarginPercent);

public sealed record MarginReport(ReportPeriod Period, IReadOnlyList<MarginRow> Rows, IReadOnlyList<CurrencyAmount> RevenueByCurrency, IReadOnlyList<CurrencyAmount> MarginByCurrency, decimal? MarginConverted);

public sealed record StatusDurationRow(string Status, string Label, int Orders, double AverageHours, double MedianHours);

public sealed record RepairTimesReport(ReportPeriod Period, int OrdersAnalyzed, double? AverageHoursToReady, double? AverageHoursToDelivery, IReadOnlyList<StatusDurationRow> ByStatus, string? Bottleneck);

public sealed record QuoteStatsReport(ReportPeriod Period, int Total, int Approved, int Rejected, int Expired, int Pending, double? ApprovalRate, IReadOnlyList<CurrencyAmount> AverageApprovedTotal);

public sealed record TopRow(string Key, int Orders, decimal? AverageTicket, string? Currency);

public sealed record TopIssuesReport(ReportPeriod Period, IReadOnlyList<TopRow> ByCategory, IReadOnlyList<TopRow> ByModel, IReadOnlyList<TopRow> ByBrand);

public sealed record TechnicianRow(Guid UserId, string Name, int Assigned, int Delivered, double? AverageHoursToReady, IReadOnlyList<CurrencyAmount> Revenue, int WarrantyClaims, double? ReentryRate);

public sealed record TechniciansReport(ReportPeriod Period, IReadOnlyList<TechnicianRow> Rows);

public sealed record WarrantyPartRow(Guid InventoryItemId, string Sku, string Name, int Uses, int Claims, double? ClaimRate);

public sealed record WarrantyReport(ReportPeriod Period, int DeliveredOrders, int WarrantyClaims, double? ReentryRate, IReadOnlyList<WarrantyPartRow> ByPart);

public sealed record FeedbackComment(string OrderCode, int Score, string? Comment, DateTime CreatedAtUtc);

public sealed record FeedbackReport(ReportPeriod Period, int Responses, double? AverageScore, IReadOnlyDictionary<int, int> Distribution, IReadOnlyList<FeedbackComment> Latest);

public sealed record InventoryValuationRow(Guid Id, string Sku, string Name, string? Category, int OnHand, int Reserved, int MinStock, decimal? UnitCost, string? Currency, decimal? Value, bool LowStock);

public sealed record InventoryReport(IReadOnlyList<InventoryValuationRow> Rows, IReadOnlyList<CurrencyAmount> TotalValue, int LowStockCount);
