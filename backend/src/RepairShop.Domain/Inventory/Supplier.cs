using RepairShop.Domain.Common;

namespace RepairShop.Domain.Inventory;

public sealed class Supplier : IShopScoped
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }

    public string Name { get; private set; } = null!;
    public string? ContactName { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? TaxId { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; } = true;

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Supplier() { }

    public Supplier(Guid shopId, string name, DateTime nowUtc)
    {
        ShopId = shopId;
        SetName(name);
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Update(string name, string? contactName, string? phone, string? email, string? taxId, string? notes, bool isActive, DateTime nowUtc)
    {
        SetName(name);
        email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        if (email is not null && !email.Contains('@')) throw new DomainException("El email del proveedor es inválido.");

        ContactName = Clean(contactName, 120);
        Phone = Clean(phone, 40);
        Email = email;
        TaxId = Clean(taxId, 20);
        Notes = Clean(notes, 1000);
        IsActive = isActive;
        UpdatedAtUtc = nowUtc;
    }

    private void SetName(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length < 2) throw new DomainException("El nombre del proveedor es obligatorio.");
        if (name.Length > 160) throw new DomainException("El nombre del proveedor es demasiado largo.");
        Name = name;
    }

    private static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        return value.Length > max ? value[..max] : value;
    }
}
