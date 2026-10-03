using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Idempotency;

public enum IdempotencyOutcome
{
    /// <summary>First time this key is seen: execute the request.</summary>
    Started,
    /// <summary>Same key and same request already completed: replay the stored response.</summary>
    Replay,
    /// <summary>Same key is still being processed by another request.</summary>
    InProgress,
    /// <summary>Same key was used with a different request body.</summary>
    Mismatch
}

public sealed record IdempotencyBeginResult(IdempotencyOutcome Outcome, int StatusCode = 0, string? ResponseBody = null);

/// <summary>
/// Stores responses of POST requests sent with an Idempotency-Key header so a retried request
/// (double click, flaky mobile connection) does not create a second sale, payment or order.
/// Uses its own DbContext instance so it never interferes with the request's unit of work.
/// </summary>
public sealed class IdempotencyStore
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(2);

    private readonly DbContextOptions<RepairShopDbContext> _options;
    private readonly IDateTimeProvider _clock;

    public IdempotencyStore(DbContextOptions<RepairShopDbContext> options, IDateTimeProvider clock)
    {
        _options = options;
        _clock = clock;
    }

    private RepairShopDbContext CreateContext() => new(_options);

    public async Task<IdempotencyBeginResult> BeginAsync(string id, string requestHash, TimeSpan ttl, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        await using var db = CreateContext();

        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO idempotency_records ("Id", "RequestHash", "Completed", "StatusCode", "ResponseBody", "CreatedAtUtc", "ExpiresAtUtc")
            VALUES ({id}, {requestHash}, false, 0, NULL, {now}, {now.Add(ttl)})
            ON CONFLICT ("Id") DO NOTHING
            """, ct);
        if (inserted == 1) return new IdempotencyBeginResult(IdempotencyOutcome.Started);

        var existing = await db.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (existing is null)
        {
            // Deleted between the insert and the read (abandoned): treat as new.
            return await BeginAsync(id, requestHash, ttl, ct);
        }

        if (existing.ExpiresAtUtc < now)
        {
            await db.IdempotencyRecords.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
            return await BeginAsync(id, requestHash, ttl, ct);
        }

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            return new IdempotencyBeginResult(IdempotencyOutcome.Mismatch);

        if (existing.Completed)
            return new IdempotencyBeginResult(IdempotencyOutcome.Replay, existing.StatusCode, existing.ResponseBody);

        if (now - existing.CreatedAtUtc > StaleAfter)
        {
            // The original request died without completing: let this one take over.
            var taken = await db.IdempotencyRecords
                .Where(x => x.Id == id && !x.Completed && x.CreatedAtUtc == existing.CreatedAtUtc)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAtUtc, now), ct);
            if (taken == 1) return new IdempotencyBeginResult(IdempotencyOutcome.Started);
        }

        return new IdempotencyBeginResult(IdempotencyOutcome.InProgress);
    }

    public async Task CompleteAsync(string id, int statusCode, string? responseBody, CancellationToken ct)
    {
        await using var db = CreateContext();
        await db.IdempotencyRecords.Where(x => x.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Completed, true)
                .SetProperty(x => x.StatusCode, statusCode)
                .SetProperty(x => x.ResponseBody, responseBody), ct);
    }

    /// <summary>Forget the key (the request failed with a server error), so the client can retry.</summary>
    public async Task AbandonAsync(string id, CancellationToken ct)
    {
        await using var db = CreateContext();
        await db.IdempotencyRecords.Where(x => x.Id == id && !x.Completed).ExecuteDeleteAsync(ct);
    }
}
