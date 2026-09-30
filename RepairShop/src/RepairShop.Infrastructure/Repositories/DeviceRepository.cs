using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Devices;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Repositories;

public sealed class DeviceRepository : IDeviceRepository
{
    private readonly RepairShopDbContext _db;
    public DeviceRepository(RepairShopDbContext db) => _db = db;

    public Task<Device?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct)
        => _db.Devices.FirstOrDefaultAsync(x => x.Id == id && x.ShopId == shopId, ct);

    public Task<List<Device>> ListByCustomerAsync(Guid shopId, Guid customerId, int skip, int take, CancellationToken ct)
        => _db.Devices
            .Where(x => x.ShopId == shopId && x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

    public Task AddAsync(Device device, CancellationToken ct)
        => _db.Devices.AddAsync(device, ct).AsTask();

    public Task RemoveAsync(Device device, CancellationToken ct)
    {
        _db.Devices.Remove(device);
        return Task.CompletedTask;
    }
}
