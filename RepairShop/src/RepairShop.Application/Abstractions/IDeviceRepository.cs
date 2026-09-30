using RepairShop.Domain.Devices;

namespace RepairShop.Application.Abstractions;

public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct);
    Task<List<Device>> ListByCustomerAsync(Guid shopId, Guid customerId, int skip, int take, CancellationToken ct);
    Task AddAsync(Device device, CancellationToken ct);
    Task RemoveAsync(Device device, CancellationToken ct);
}
