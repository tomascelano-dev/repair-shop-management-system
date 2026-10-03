using RepairShop.Domain.Common;

namespace RepairShop.Domain.Users;

/// <summary>
/// Grants a user access to an additional shop (branch) of the same organization, with a role for that shop.
/// The user's home shop (AppUser.ShopId) does not need an entry.
/// </summary>
public sealed class UserShopAccess
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid ShopId { get; private set; }
    public UserRole Role { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private UserShopAccess() { } // EF

    public UserShopAccess(Guid userId, Guid shopId, UserRole role, DateTime nowUtc)
    {
        if (userId == Guid.Empty) throw new DomainException("El acceso debe referenciar un usuario.");
        if (shopId == Guid.Empty) throw new DomainException("El acceso debe referenciar una sucursal.");
        if (!Enum.IsDefined(role)) throw new DomainException("Rol inválido.");

        UserId = userId;
        ShopId = shopId;
        Role = role;
        CreatedAtUtc = nowUtc;
    }

    public void ChangeRole(UserRole role)
    {
        if (!Enum.IsDefined(role)) throw new DomainException("Rol inválido.");
        Role = role;
    }
}
