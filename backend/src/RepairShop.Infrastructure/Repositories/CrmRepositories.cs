using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Common;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Devices;
using RepairShop.Domain.Feedback;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly RepairShopDbContext _db;
    public CustomerRepository(RepairShopDbContext db) => _db = db;

    public Task<Customer?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct)
        => _db.Customers.FirstOrDefaultAsync(x => x.Id == id && x.ShopId == shopId, ct);

    public Task<List<Customer>> GetByIdsAsync(Guid shopId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => _db.Customers.Where(x => x.ShopId == shopId && ids.Contains(x.Id)).ToListAsync(ct);

    public Task<List<Customer>> ListAsync(Guid shopId, int skip, int take, CancellationToken ct)
        => _db.Customers
            .Where(x => x.ShopId == shopId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

    public async Task<(List<Customer> Items, int Total)> SearchAsync(Guid shopId, CustomerSearchOptions options, CancellationToken ct)
    {
        var q = _db.Customers.AsQueryable().Where(x => x.ShopId == shopId);

        if (!string.IsNullOrWhiteSpace(options.Q))
        {
            var pattern = Like.Contains(options.Q);
            var digits = PhoneNumber.Digits(options.Q);
            var phonePattern = digits.Length >= 4 ? Like.Contains(digits.Length > 8 ? digits[^8..] : digits) : null;

            q = q.Where(x =>
                EF.Functions.ILike(x.FullName, pattern)
                || EF.Functions.ILike(x.Phone, pattern)
                || (phonePattern != null && EF.Functions.ILike(x.PhoneKey, phonePattern))
                || (x.Email != null && EF.Functions.ILike(x.Email, pattern))
                || (x.DocumentNumber != null && EF.Functions.ILike(x.DocumentNumber, pattern))
                || (x.Notes != null && EF.Functions.ILike(x.Notes, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(options.Tag))
        {
            var tag = Like.Contains(options.Tag.Trim().ToLowerInvariant());
            q = q.Where(x => x.Tags != null && EF.Functions.ILike(x.Tags, tag));
        }

        if (options.DateFromUtc is not null) q = q.Where(x => x.CreatedAtUtc >= options.DateFromUtc);
        if (options.DateToUtc is not null) q = q.Where(x => x.CreatedAtUtc <= options.DateToUtc);

        q = ApplySort(q, options.SortBy, options.SortDir);

        var total = await q.CountAsync(ct);
        var take = Math.Clamp(options.Take, 1, 200);
        var skip = Math.Max(0, options.Skip);
        var items = await q.Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    public Task<List<Customer>> FindByPhoneKeyAsync(Guid shopId, string phoneKey, Guid? excludeId, CancellationToken ct)
        => _db.Customers
            .Where(x => x.ShopId == shopId && x.PhoneKey == phoneKey && (excludeId == null || x.Id != excludeId))
            .OrderBy(x => x.CreatedAtUtc)
            .Take(20)
            .ToListAsync(ct);

    public async Task<CustomerUsage> GetUsageAsync(Guid shopId, Guid customerId, CancellationToken ct)
    {
        var devices = await _db.Devices.CountAsync(x => x.ShopId == shopId && x.CustomerId == customerId, ct);
        var orders = await _db.RepairOrders.CountAsync(x => x.ShopId == shopId && x.CustomerId == customerId, ct);
        var sales = await _db.Sales.CountAsync(x => x.ShopId == shopId && x.CustomerId == customerId, ct);
        return new CustomerUsage(devices, orders, sales);
    }

    private static IQueryable<Customer> ApplySort(IQueryable<Customer> q, string? sortBy, string? sortDir)
    {
        sortBy = (sortBy ?? "createdAt").Trim();
        sortDir = (sortDir ?? "desc").Trim();
        var desc = sortDir.Equals("desc", StringComparison.OrdinalIgnoreCase);

        return (sortBy.ToLowerInvariant(), desc) switch
        {
            ("createdat", true) => q.OrderByDescending(x => x.CreatedAtUtc),
            ("createdat", false) => q.OrderBy(x => x.CreatedAtUtc),
            ("name", true) => q.OrderByDescending(x => x.FullName),
            ("name", false) => q.OrderBy(x => x.FullName),
            ("updatedat", true) => q.OrderByDescending(x => x.UpdatedAtUtc),
            ("updatedat", false) => q.OrderBy(x => x.UpdatedAtUtc),
            _ => q.OrderByDescending(x => x.CreatedAtUtc)
        };
    }

    public Task AddAsync(Customer customer, CancellationToken ct)
        => _db.Customers.AddAsync(customer, ct).AsTask();

    public Task RemoveAsync(Customer customer, CancellationToken ct)
    {
        _db.Customers.Remove(customer);
        return Task.CompletedTask;
    }
}

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

    public async Task<(List<Device> Items, int Total)> SearchAsync(Guid shopId, string? q, int skip, int take, CancellationToken ct)
    {
        var query = _db.Devices.Where(x => x.ShopId == shopId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var p = Like.Contains(q);
            query = query.Where(x =>
                EF.Functions.ILike(x.Brand, p)
                || EF.Functions.ILike(x.Model, p)
                || (x.Label != null && EF.Functions.ILike(x.Label, p))
                || (x.SerialNumber != null && EF.Functions.ILike(x.SerialNumber, p))
                || (x.Imei != null && EF.Functions.ILike(x.Imei, p))
                || _db.Customers.Any(c => c.Id == x.CustomerId && (EF.Functions.ILike(c.FullName, p) || EF.Functions.ILike(c.Phone, p))));
        }

        var total = await query.CountAsync(ct);
        (skip, take) = (Math.Max(0, skip), Math.Clamp(take, 1, 200));
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    public Task<int> CountOrdersAsync(Guid shopId, Guid deviceId, CancellationToken ct)
        => _db.RepairOrders.CountAsync(x => x.ShopId == shopId && x.DeviceId == deviceId, ct);

    public Task AddAsync(Device device, CancellationToken ct)
        => _db.Devices.AddAsync(device, ct).AsTask();

    public Task RemoveAsync(Device device, CancellationToken ct)
    {
        _db.Devices.Remove(device);
        return Task.CompletedTask;
    }
}

public sealed class CustomerFeedbackRepository : ICustomerFeedbackRepository
{
    private readonly RepairShopDbContext _db;
    public CustomerFeedbackRepository(RepairShopDbContext db) => _db = db;

    public Task<CustomerFeedback?> GetByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => _db.CustomerFeedback.FirstOrDefaultAsync(x => x.ShopId == shopId && x.RepairOrderId == orderId, ct);

    public Task AddAsync(CustomerFeedback feedback, CancellationToken ct)
        => _db.CustomerFeedback.AddAsync(feedback, ct).AsTask();
}
