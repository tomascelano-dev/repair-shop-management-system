using RepairShop.Domain.Quotes;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.Abstractions;

public interface IQuoteRepository
{
    Task<Quote?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct);
    Task<List<Quote>> ListByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct);
    Task<Quote?> GetApprovedAsync(Guid shopId, Guid orderId, CancellationToken ct);
    Task<int> GetMaxVersionAsync(Guid shopId, Guid orderId, CancellationToken ct);
    Task<List<Quote>> ListExpiredSentAsync(DateTime nowUtc, int take, CancellationToken ct);
    Task AddAsync(Quote quote, CancellationToken ct);
}

public interface IRepairOrderQaChecklistRepository
{
    Task<RepairOrderQaChecklist?> GetByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct);
    Task AddAsync(RepairOrderQaChecklist checklist, CancellationToken ct);
}
