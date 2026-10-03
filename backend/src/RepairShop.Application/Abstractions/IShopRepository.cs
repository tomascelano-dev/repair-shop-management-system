using RepairShop.Domain.Shops;

namespace RepairShop.Application.Abstractions;

public interface IShopRepository
{
    Task<Shop?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<List<Shop>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<List<Shop>> ListAsync(int skip, int take, CancellationToken ct);
    Task<List<Shop>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct);
    Task AddAsync(Shop shop, CancellationToken ct);
}

public interface IShopIntegrationRepository
{
    Task<ShopIntegration?> GetAsync(Guid shopId, CancellationToken ct);
    Task AddAsync(ShopIntegration integration, CancellationToken ct);
}
