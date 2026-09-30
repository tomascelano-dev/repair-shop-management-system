using RepairShop.Domain.Common;

namespace RepairShop.Domain.Users;

public sealed class AppUser
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    // Multi-sucursal: el usuario pertenece a una única sucursal.
    public Guid ShopId { get; private set; }

    public string Email { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public UserRole Role { get; private set; }

    // Stored as PBKDF2 string (see Application/Infrastructure).
    public string PasswordHash { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    private AppUser() { } // EF

    /// <summary>
    /// Crea un usuario asociado a una sucursal.
    /// </summary>
    public AppUser(Guid shopId, string email, string displayName, UserRole role, string passwordHash, DateTime nowUtc)
    {
        ShopId = shopId;
        Email = (email ?? "").Trim().ToLowerInvariant();
        DisplayName = (displayName ?? "").Trim();
        Role = role;
        PasswordHash = (passwordHash ?? "").Trim();
        CreatedAtUtc = nowUtc;

        // En el código base puede estar vacío; al completar el wiring (JWT fijo), se valida.
        if (!Email.Contains('@')) throw new DomainException("User email is invalid.");
        if (DisplayName.Length < 2) throw new DomainException("User display name is required.");
        if (PasswordHash.Length < 20) throw new DomainException("User password hash is invalid.");
    }

    // Back-compat: constructor anterior sin ShopId.
    public AppUser(string email, string displayName, UserRole role, string passwordHash, DateTime nowUtc)
        : this(Guid.Empty, email, displayName, role, passwordHash, nowUtc)
    {
    }

    public void ChangePassword(string newPasswordHash, DateTime nowUtc)
    {
        newPasswordHash = (newPasswordHash ?? "").Trim();
        if (newPasswordHash.Length < 20) throw new DomainException("User password hash is invalid.");
        PasswordHash = newPasswordHash;
        // Not tracking UpdatedAt to keep it simple.
    }
}
