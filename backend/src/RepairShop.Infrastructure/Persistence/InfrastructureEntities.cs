namespace RepairShop.Infrastructure.Persistence;

/// <summary>Per-scope correlative counter (see CounterService).</summary>
public sealed class ShopCounter
{
    public Guid ScopeId { get; set; }
    public string Key { get; set; } = null!;
    public int Value { get; set; }
}

/// <summary>Stored response of an idempotent request (Idempotency-Key header).</summary>
public sealed class IdempotencyRecord
{
    public string Id { get; set; } = null!;         // sha256(user|method|path|key)
    public string RequestHash { get; set; } = null!;
    public bool Completed { get; set; }
    public int StatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
