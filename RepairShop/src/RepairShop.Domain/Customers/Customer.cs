using RepairShop.Domain.Common;

namespace RepairShop.Domain.Customers;

public sealed class Customer
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    // Multi-sucursal
    public Guid ShopId { get; private set; }

    public string FullName { get; private set; } = null!;
    public string Phone { get; private set; } = null!;
    public string? Notes { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private Customer() { } // EF

    public Customer(Guid shopId, string fullName, string phone, string? notes, DateTime nowUtc)
    {
        ShopId = shopId;
        FullName = (fullName ?? "").Trim();
        Phone = (phone ?? "").Trim();
        Notes = notes?.Trim();
        CreatedAtUtc = nowUtc;

        if (FullName.Length < 3) throw new DomainException("Customer full name is required (min 3 chars).");
        if (Phone.Length < 6) throw new DomainException("Customer phone is required (min 6 chars).");
    }

    // Back-compat: constructor anterior sin ShopId.
    public Customer(string fullName, string phone, string? notes, DateTime nowUtc)
        : this(Guid.Empty, fullName, phone, notes, nowUtc)
    {
    }

    public void Update(string fullName, string phone, string? notes)
    {
        FullName = (fullName ?? "").Trim();
        Phone = (phone ?? "").Trim();
        Notes = notes?.Trim();

        if (FullName.Length < 3) throw new DomainException("Customer full name is required (min 3 chars).");
        if (Phone.Length < 6) throw new DomainException("Customer phone is required (min 6 chars).");
    }
}
