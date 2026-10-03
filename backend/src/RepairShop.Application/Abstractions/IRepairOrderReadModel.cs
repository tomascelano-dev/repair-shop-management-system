namespace RepairShop.Application.Abstractions;

/// <summary>
/// Batch read-model for orders: display names and financial aggregates in a few queries
/// (avoids N+1 when listing orders, building the board or the customer summary).
/// </summary>
public interface IRepairOrderReadModel
{
    Task<IReadOnlyDictionary<Guid, OrderReadInfo>> GetInfoAsync(Guid shopId, IReadOnlyCollection<Guid> orderIds, CancellationToken ct);
}

public sealed record OrderReadInfo(
    Guid OrderId,
    string CustomerName,
    string CustomerPhone,
    string? CustomerEmail,
    bool CustomerNotificationsOptIn,
    string DeviceBrand,
    string DeviceModel,
    string? DeviceLabel,
    string? DeviceSerial,
    string? DeviceImei,
    string? TechnicianName,
    decimal Paid,
    string? PaymentsCurrency,
    decimal ExtraCharges,
    bool HasApprovedQuote,
    bool HasOpenQuote,
    bool QaPassed,
    int PhotosCount)
{
    public string DeviceDisplay => string.IsNullOrWhiteSpace(DeviceLabel) ? $"{DeviceBrand} {DeviceModel}" : $"{DeviceBrand} {DeviceModel} ({DeviceLabel})";
}
