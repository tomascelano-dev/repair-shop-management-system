using RepairShop.Domain.Common;

namespace RepairShop.Domain.Users;

/// <summary>
/// Rotating refresh token (only the SHA-256 hash is persisted).
/// Tokens of the same login share a FamilyId: reusing a rotated token revokes the whole family.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }

    // Shop the session is currently working on (changes with "switch shop").
    public Guid ShopId { get; private set; }

    public string TokenHash { get; private set; } = null!;
    public Guid FamilyId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }
    public string? RevokedReason { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    public string? CreatedByIp { get; private set; }
    public string? UserAgent { get; private set; }

    private RefreshToken() { } // EF

    public RefreshToken(Guid userId, Guid shopId, string tokenHash, Guid familyId, DateTime nowUtc, DateTime expiresAtUtc, string? ip, string? userAgent)
    {
        if (userId == Guid.Empty) throw new DomainException("El token de sesión debe pertenecer a un usuario.");
        if (shopId == Guid.Empty) throw new DomainException("El token de sesión debe referenciar una sucursal.");
        if (string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Length < 32) throw new DomainException("El token de sesión es inválido.");
        if (expiresAtUtc <= nowUtc) throw new DomainException("El vencimiento del token de sesión debe ser futuro.");

        UserId = userId;
        ShopId = shopId;
        TokenHash = tokenHash;
        FamilyId = familyId == Guid.Empty ? Guid.NewGuid() : familyId;
        CreatedAtUtc = nowUtc;
        ExpiresAtUtc = expiresAtUtc;
        CreatedByIp = Truncate(ip, 64);
        UserAgent = Truncate(userAgent, 300);
    }

    public bool IsActive(DateTime nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;

    public void Revoke(string reason, DateTime nowUtc, Guid? replacedBy = null)
    {
        if (RevokedAtUtc is not null) return;
        RevokedAtUtc = nowUtc;
        RevokedReason = Truncate(reason, 80);
        ReplacedByTokenId = replacedBy;
    }

    private static string? Truncate(string? value, int max)
        => string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value : value[..max];
}
