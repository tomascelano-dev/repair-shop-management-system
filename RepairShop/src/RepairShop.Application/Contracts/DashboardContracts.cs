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
    DateTime GeneratedAtUtc
);
