using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.Abstractions;

public interface IRepairOrderRepository
{
    Task<RepairOrder?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct);
    Task<RepairOrder?> GetByNumberAsync(Guid shopId, int orderNumber, CancellationToken ct);

    /// <summary>Cross-shop lookup by the public tracking token (portal / QR).</summary>
    Task<RepairOrder?> GetByPublicTokenAsync(string token, CancellationToken ct);

    Task<List<RepairOrder>> ListAsync(Guid shopId, int skip, int take, CancellationToken ct);
    Task<(List<RepairOrder> Items, int Total)> SearchAsync(Guid shopId, RepairOrderSearchOptions options, CancellationToken ct);
    /// <summary>Cross-shop scan used by background jobs (reminders, surveys).</summary>
    Task<List<RepairOrder>> ListForJobsAsync(RepairOrderStatus status, DateTime? readyBeforeUtc, DateTime? deliveredAfterUtc, DateTime? deliveredBeforeUtc, int take, CancellationToken ct);

    Task<bool> HasWarrantyClaimsAsync(Guid shopId, Guid orderId, CancellationToken ct);
    Task AddAsync(RepairOrder order, CancellationToken ct);
    Task RemoveAsync(RepairOrder order, CancellationToken ct);
}

public sealed record RepairOrderSearchOptions(
    string? Q = null,
    RepairOrderStatus? Status = null,
    DateTime? DateFromUtc = null,
    DateTime? DateToUtc = null,
    string? SortBy = null,
    string? SortDir = null,
    int Skip = 0,
    int Take = 50,
    Guid? AssignedTechnicianId = null,
    Guid? CustomerId = null,
    Guid? DeviceId = null,
    bool? OnlyOpen = null,
    bool? OnlyOverdue = null,
    RepairOrderPriority? Priority = null,
    IReadOnlyCollection<RepairOrderStatus>? Statuses = null,
    DateTime? NowUtc = null);
