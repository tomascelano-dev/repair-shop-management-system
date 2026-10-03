using RepairShop.Domain.Quotes;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.Abstractions;

/// <summary>Raw, already filtered rows for dashboards and reports (aggregation happens in the application layer).</summary>
public interface IReportQueries
{
    Task<IReadOnlyDictionary<RepairOrderStatus, int>> CountOrdersByStatusAsync(Guid shopId, CancellationToken ct);
    Task<int> CountOverdueAsync(Guid shopId, DateTime nowUtc, CancellationToken ct);
    Task<int> CountStaleAsync(Guid shopId, DateTime olderThanUtc, CancellationToken ct);
    Task<int> CountReadyBeforeAsync(Guid shopId, DateTime readyBeforeUtc, CancellationToken ct);
    Task<int> CountQuotesAsync(Guid shopId, QuoteStatus status, CancellationToken ct);
    Task<int> CountLowStockAsync(Guid shopId, CancellationToken ct);
    Task<int> CountOpenAssignedAsync(Guid shopId, Guid userId, CancellationToken ct);
    Task<bool> HasOpenCashSessionAsync(Guid shopId, CancellationToken ct);
    Task<IReadOnlyList<WorkloadRow>> TechnicianWorkloadAsync(Guid shopId, DateTime nowUtc, CancellationToken ct);

    Task<IReadOnlyList<Contracts.CurrencyAmount>> OrderPaymentsByCurrencyAsync(Guid shopId, CancellationToken ct);
    Task<IReadOnlyList<MoneyMovementRow>> MoneyMovementsAsync(Guid shopId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
    Task<IReadOnlyList<OrderFactRow>> OrderFactsAsync(Guid shopId, DateTime fromUtc, DateTime toUtc, bool byDelivery, CancellationToken ct);
    Task<IReadOnlyList<StatusChangeRow>> StatusChangesAsync(Guid shopId, IReadOnlyCollection<Guid> orderIds, CancellationToken ct);
    Task<IReadOnlyList<SaleFactRow>> SaleFactsAsync(Guid shopId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
    Task<IReadOnlyList<QuoteFactRow>> QuoteFactsAsync(Guid shopId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
    Task<IReadOnlyList<PartUseRow>> PartUsesAsync(Guid shopId, IReadOnlyCollection<Guid> orderIds, CancellationToken ct);
    Task<IReadOnlyList<FeedbackRow>> FeedbackAsync(Guid shopId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
    Task<IReadOnlyList<StockRow>> StockAsync(Guid shopId, CancellationToken ct);
}

public sealed record WorkloadRow(Guid UserId, string Name, int OpenOrders, int OverdueOrders);

/// <summary>Money that entered (+) or left (-) through order payments, sales and refunds.</summary>
public sealed record MoneyMovementRow(DateTime AtUtc, string Kind, string Currency, string Method, decimal SignedAmount);

public sealed record OrderFactRow(
    Guid Id,
    int OrderNumber,
    RepairOrderStatus Status,
    string? Category,
    string Brand,
    string Model,
    Guid? TechnicianId,
    string? TechnicianName,
    decimal? AgreedPrice,
    string? Currency,
    decimal ExtraCharges,
    bool IsWarrantyClaim,
    Guid? WarrantyOfOrderId,
    DateTime CreatedAtUtc,
    DateTime? ReadyAtUtc,
    DateTime? DeliveredAtUtc,
    string CustomerName);

public sealed record StatusChangeRow(Guid OrderId, RepairOrderStatus From, RepairOrderStatus To, DateTime AtUtc);

public sealed record SaleFactRow(Guid Id, int Number, DateTime CreatedAtUtc, string Currency, decimal Total, decimal Refunded, decimal Cost, bool Voided);

public sealed record QuoteFactRow(Guid Id, QuoteStatus Status, string Currency, decimal Total, DateTime CreatedAtUtc);

public sealed record PartUseRow(Guid OrderId, Guid ItemId, string Sku, string Name, int Quantity, decimal? UnitCost, string? CostCurrency);

public sealed record FeedbackRow(int OrderNumber, int Score, string? Comment, DateTime CreatedAtUtc);

public sealed record StockRow(Guid Id, string Sku, string Name, string? Category, int OnHand, int Reserved, int MinStock, bool TrackStock, decimal? UnitCost, string? Currency);
