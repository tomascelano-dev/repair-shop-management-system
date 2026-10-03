using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Domain.Devices;

namespace RepairShop.Application.Customers;

public sealed class DeviceService
{
    private const string EntityType = "device";

    private readonly IDeviceRepository _devices;
    private readonly ICustomerRepository _customers;
    private readonly IAuditLog _audit;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public DeviceService(IDeviceRepository devices, ICustomerRepository customers, IAuditLog audit, IUnitOfWork uow, IDateTimeProvider clock)
    {
        _devices = devices;
        _customers = customers;
        _audit = audit;
        _uow = uow;
        _clock = clock;
    }

    public async Task<PagedResult<DeviceResponse>> SearchAsync(Guid shopId, string? q, int skip, int take, CancellationToken ct)
    {
        var (items, total) = await _devices.SearchAsync(shopId, q, skip, take, ct);
        var names = (await _customers.GetByIdsAsync(shopId, items.Select(d => d.CustomerId).Distinct().ToList(), ct)).ToDictionary(c => c.Id, c => c.FullName);
        return new PagedResult<DeviceResponse>(items.Select(d => d.ToResponse(names.GetValueOrDefault(d.CustomerId))).ToList(), total);
    }

    public async Task<List<DeviceResponse>> ListByCustomerAsync(Guid shopId, Guid customerId, int skip, int take, CancellationToken ct)
    {
        (skip, take) = Paging.Normalize(skip, take);
        var customer = await _customers.GetByIdAsync(shopId, customerId, ct);
        return (await _devices.ListByCustomerAsync(shopId, customerId, skip, take, ct)).Select(d => d.ToResponse(customer?.FullName)).ToList();
    }

    public async Task<DeviceResponse> GetAsync(Guid shopId, Guid id, CancellationToken ct)
    {
        var d = await RequireAsync(shopId, id, ct);
        var customer = await _customers.GetByIdAsync(shopId, d.CustomerId, ct);
        return d.ToResponse(customer?.FullName);
    }

    public async Task<DeviceResponse> CreateAsync(Guid shopId, DeviceCreateRequest req, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var customer = await _customers.GetByIdAsync(shopId, req.CustomerId, ct) ?? throw new NotFoundException("El cliente no existe en esta sucursal.");
        var d = new Device(shopId, customer.Id, req.Brand, req.Model, req.Label, req.SerialNumber, req.Notes, now);
        d.SetImei(req.Imei, now);
        await _devices.AddAsync(d, ct);
        await _audit.AddAsync(shopId, EntityType, d.Id, "device_created", actor, new { customerId = customer.Id, d.Brand, d.Model }, ct);
        await _uow.SaveChangesAsync(ct);
        return d.ToResponse(customer.FullName);
    }

    public async Task<DeviceResponse> UpdateAsync(Guid shopId, Guid id, DeviceUpdateRequest req, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var d = await RequireAsync(shopId, id, ct);
        d.Update(req.Brand, req.Model, req.Label, req.SerialNumber, req.Notes, now);
        d.SetImei(req.Imei, now);
        await _audit.AddAsync(shopId, EntityType, d.Id, "device_updated", actor, null, ct);
        await _uow.SaveChangesAsync(ct);
        return await GetAsync(shopId, id, ct);
    }

    public async Task DeleteAsync(Guid shopId, Guid id, Actor actor, CancellationToken ct)
    {
        var d = await RequireAsync(shopId, id, ct);
        var orders = await _devices.CountOrdersAsync(shopId, id, ct);
        if (orders > 0) throw new ConflictException($"No se puede eliminar: el equipo tiene {orders} orden(es) asociadas.");
        await _devices.RemoveAsync(d, ct);
        await _audit.AddAsync(shopId, EntityType, d.Id, "device_deleted", actor, new { d.Brand, d.Model }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<Device> RequireAsync(Guid shopId, Guid id, CancellationToken ct)
        => await _devices.GetByIdAsync(shopId, id, ct) ?? throw new NotFoundException("Equipo no encontrado.");
}
