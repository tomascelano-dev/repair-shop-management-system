using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;

namespace RepairShop.Infrastructure.Persistence;

/// <summary>
/// Atomic counters using INSERT ... ON CONFLICT DO UPDATE ... RETURNING (single round trip, row-level lock).
/// When called inside a transaction, the increment is rolled back with it (no gaps).
/// </summary>
public sealed class CounterService : ICounterService
{
    private readonly RepairShopDbContext _db;
    public CounterService(RepairShopDbContext db) => _db = db;

    public async Task<int> NextAsync(Guid scopeId, string key, CancellationToken ct)
    {
        var values = await _db.Database.SqlQuery<int>($"""
            INSERT INTO shop_counters ("ScopeId", "Key", "Value") VALUES ({scopeId}, {key}, 1)
            ON CONFLICT ("ScopeId", "Key") DO UPDATE SET "Value" = shop_counters."Value" + 1
            RETURNING "Value"
            """).ToListAsync(ct);
        return values.Single();
    }
}
