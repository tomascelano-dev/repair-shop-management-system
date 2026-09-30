using RepairShop.Domain.Shops;

namespace RepairShop.Application.Abstractions;

public interface IShopRepository
{
    Task<Shop?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<List<Shop>> ListAsync(int skip, int take, CancellationToken ct);
    Task AddAsync(Shop shop, CancellationToken ct);
}
