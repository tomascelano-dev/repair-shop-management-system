namespace RepairShop.Application.Abstractions;

public interface IUnitOfWork
{
    /// <summary>
    /// Persists tracked changes. Concurrency conflicts and unique/foreign key violations
    /// are translated to <see cref="Common.ConflictException"/> (HTTP 409).
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct);

    /// <summary>Runs the work inside a database transaction (commit on success, rollback on error).</summary>
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct);

    /// <summary>
    /// Re-runs the work when an optimistic concurrency conflict happens (e.g. two sales of the same item).
    /// The change tracker is cleared between attempts, so the work must load everything it needs.
    /// </summary>
    Task<T> RetryOnConflictAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct, int maxAttempts = 3);

    /// <summary>Stops tracking an entity whose insert failed (so later saves don't retry it).</summary>
    void Detach(object entity);
}
