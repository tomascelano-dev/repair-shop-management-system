namespace RepairShop.Application.Abstractions;

/// <summary>
/// Atomic, per-scope correlative numbers (orders, sales, purchases, cash sessions...).
/// Gaps are possible if a transaction fails after taking a number; numbers are never reused.
/// </summary>
public interface ICounterService
{
    Task<int> NextAsync(Guid scopeId, string key, CancellationToken ct);
}

public static class CounterKeys
{
    public const string RepairOrder = "repair_order";
    public const string Sale = "sale";
    public const string PurchaseOrder = "purchase_order";
    public const string CashSession = "cash_session";
    public const string StockTransfer = "stock_transfer";
}
