using RepairShop.Domain.Common;

namespace RepairShop.Domain.Users;

public sealed class AppUser
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    // Home shop. Extra shops are granted through UserShopAccess (multi-sucursal).
    public Guid ShopId { get; private set; }

    public string Email { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public UserRole Role { get; private set; }

    // Stored as PBKDF2 string (see Application/Infrastructure).
    public string PasswordHash { get; private set; } = null!;

    public bool IsActive { get; private set; } = true;

    // Changes whenever credentials/permissions change: invalidates issued access tokens.
    public string SecurityStamp { get; private set; } = NewStamp();

    // Invitation / password reset (only the SHA-256 hash of the token is stored).
    public string? PendingTokenHash { get; private set; }
    public UserTokenPurpose? PendingTokenPurpose { get; private set; }
    public DateTime? PendingTokenExpiresAtUtc { get; private set; }

    // Self-service signups prove their email with a link; invited users prove it by accepting the invitation.
    public DateTime? EmailVerifiedAtUtc { get; private set; }
    public string? EmailVerificationTokenHash { get; private set; }

    public DateTime? LastLoginAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private AppUser() { } // EF

    /// <summary>
    /// Crea un usuario asociado a una sucursal.
    /// </summary>
    public AppUser(Guid shopId, string email, string displayName, UserRole role, string passwordHash, DateTime nowUtc)
    {
        ShopId = shopId;
        Email = NormalizeEmail(email);
        DisplayName = (displayName ?? "").Trim();
        Role = role;
        PasswordHash = (passwordHash ?? "").Trim();
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;

        if (!Email.Contains('@')) throw new DomainException("El email del usuario es inválido.");
        if (DisplayName.Length < 2) throw new DomainException("El nombre del usuario es obligatorio.");
        if (PasswordHash.Length < 20) throw new DomainException("El hash de contraseña es inválido.");
        if (!Enum.IsDefined(role)) throw new DomainException("El rol del usuario es inválido.");
    }

    // Back-compat: constructor anterior sin ShopId.
    public AppUser(string email, string displayName, UserRole role, string passwordHash, DateTime nowUtc)
        : this(Guid.Empty, email, displayName, role, passwordHash, nowUtc)
    {
    }

    public bool HasPendingInvitation => PendingTokenPurpose == UserTokenPurpose.Invitation && PendingTokenHash is not null;

    public void ChangePassword(string newPasswordHash, DateTime nowUtc)
    {
        newPasswordHash = (newPasswordHash ?? "").Trim();
        if (newPasswordHash.Length < 20) throw new DomainException("El hash de contraseña es inválido.");
        PasswordHash = newPasswordHash;
        ClearPendingToken();
        RotateSecurityStamp(nowUtc);
    }

    public void UpdateProfile(string displayName, UserRole role, DateTime nowUtc)
    {
        displayName = (displayName ?? "").Trim();
        if (displayName.Length < 2) throw new DomainException("El nombre del usuario es obligatorio.");
        if (!Enum.IsDefined(role)) throw new DomainException("El rol del usuario es inválido.");

        var roleChanged = role != Role;
        DisplayName = displayName;
        Role = role;
        UpdatedAtUtc = nowUtc;
        if (roleChanged) RotateSecurityStamp(nowUtc);
    }

    public void SetActive(bool isActive, DateTime nowUtc)
    {
        if (IsActive == isActive) return;
        IsActive = isActive;
        RotateSecurityStamp(nowUtc);
    }

    public void SetPendingToken(UserTokenPurpose purpose, string tokenHash, DateTime expiresAtUtc, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Length < 32) throw new DomainException("El token es inválido.");
        if (expiresAtUtc <= nowUtc) throw new DomainException("El vencimiento del token debe ser futuro.");
        PendingTokenHash = tokenHash;
        PendingTokenPurpose = purpose;
        PendingTokenExpiresAtUtc = expiresAtUtc;
        UpdatedAtUtc = nowUtc;
    }

    public bool IsPendingTokenValid(UserTokenPurpose purpose, string tokenHash, DateTime nowUtc)
        => PendingTokenPurpose == purpose
           && PendingTokenHash is not null
           && string.Equals(PendingTokenHash, tokenHash, StringComparison.Ordinal)
           && PendingTokenExpiresAtUtc > nowUtc;

    public void RegisterLogin(DateTime nowUtc) => LastLoginAtUtc = nowUtc;

    public bool IsEmailVerified => EmailVerifiedAtUtc is not null;

    public void RequestEmailVerification(string tokenHash, DateTime nowUtc)
    {
        if (IsEmailVerified) return;
        EmailVerificationTokenHash = string.IsNullOrWhiteSpace(tokenHash) ? throw new DomainException("El token de verificación es inválido.") : tokenHash;
        UpdatedAtUtc = nowUtc;
    }

    public bool VerifyEmail(string tokenHash, DateTime nowUtc)
    {
        if (IsEmailVerified) return true;
        if (EmailVerificationTokenHash is null || !string.Equals(EmailVerificationTokenHash, tokenHash, StringComparison.Ordinal)) return false;
        MarkEmailVerified(nowUtc);
        return true;
    }

    public void MarkEmailVerified(DateTime nowUtc)
    {
        if (IsEmailVerified) return;
        EmailVerifiedAtUtc = nowUtc;
        EmailVerificationTokenHash = null;
        UpdatedAtUtc = nowUtc;
    }

    public void RotateSecurityStamp(DateTime nowUtc)
    {
        SecurityStamp = NewStamp();
        UpdatedAtUtc = nowUtc;
    }

    private void ClearPendingToken()
    {
        PendingTokenHash = null;
        PendingTokenPurpose = null;
        PendingTokenExpiresAtUtc = null;
    }

    public static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    private static string NewStamp() => Guid.NewGuid().ToString("N");
}

public enum UserTokenPurpose
{
    Invitation = 0,
    PasswordReset = 1
}
