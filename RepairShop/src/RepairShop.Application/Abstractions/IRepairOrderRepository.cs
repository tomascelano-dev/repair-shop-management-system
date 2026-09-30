using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.Abstractions;

public interface IRepairOrderRepository
{
    Task<RepairOrder?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct);
    Task<List<RepairOrder>> ListAsync(Guid shopId, int skip, int take, CancellationToken ct);
    Task<(List<RepairOrder> Items, int Total)> SearchAsync(Guid shopId, RepairOrderSearchOptions options, CancellationToken ct);
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
    int Take = 50);
