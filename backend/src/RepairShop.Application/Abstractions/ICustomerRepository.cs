using RepairShop.Domain.Customers;

namespace RepairShop.Application.Abstractions;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct);
    Task<List<Customer>> GetByIdsAsync(Guid shopId, IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<List<Customer>> ListAsync(Guid shopId, int skip, int take, CancellationToken ct);
    Task<(List<Customer> Items, int Total)> SearchAsync(Guid shopId, CustomerSearchOptions options, CancellationToken ct);
    Task<List<Customer>> FindByPhoneKeyAsync(Guid shopId, string phoneKey, Guid? excludeId, CancellationToken ct);
    Task<CustomerUsage> GetUsageAsync(Guid shopId, Guid customerId, CancellationToken ct);
    Task AddAsync(Customer customer, CancellationToken ct);
    Task RemoveAsync(Customer customer, CancellationToken ct);
}

public sealed record CustomerUsage(int Devices, int Orders, int Sales);

public sealed record CustomerSearchOptions(
    string? Q = null,
    DateTime? DateFromUtc = null,
    DateTime? DateToUtc = null,
    string? SortBy = null,
    string? SortDir = null,
    int Skip = 0,
    int Take = 50,
    string? Tag = null);
