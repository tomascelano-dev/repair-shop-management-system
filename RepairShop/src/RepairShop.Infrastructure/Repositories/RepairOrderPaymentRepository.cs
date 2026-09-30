using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.RepairOrders;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Repositories;

public sealed class RepairOrderPaymentRepository : IRepairOrderPaymentRepository
{
    private readonly RepairShopDbContext _db;
    public RepairOrderPaymentRepository(RepairShopDbContext db) => _db = db;

    public Task<List<RepairOrderPayment>> ListByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => _db.RepairOrderPayments
            .Where(x => x.ShopId == shopId && x.RepairOrderId == orderId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<decimal> SumByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
    {
        return await _db.RepairOrderPayments
            .Where(x => x.ShopId == shopId && x.RepairOrderId == orderId)
            .Select(x => x.Amount)
            .DefaultIfEmpty(0m)
            .SumAsync(ct);
    }

    public Task AddAsync(RepairOrderPayment payment, CancellationToken ct)
        => _db.RepairOrderPayments.AddAsync(payment, ct).AsTask();
}
