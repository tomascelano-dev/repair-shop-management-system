using RepairShop.Domain.Common;

namespace RepairShop.Domain.Shops;

public sealed class Shop
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Name { get; private set; } = null!;

    public string? Phone { get; private set; }
    public string? AddressLine { get; private set; }
    public string? City { get; private set; }
    public string? Country { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Shop() { } // EF

    public Shop(
        string name,
        string? phone,
        string? addressLine,
        string? city,
        string? country,
        DateTime nowUtc)
    {
        SetValues(name, phone, addressLine, city, country);
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Update(
        string name,
        string? phone,
        string? addressLine,
        string? city,
        string? country,
        DateTime nowUtc)
    {
        SetValues(name, phone, addressLine, city, country);
        UpdatedAtUtc = nowUtc;
    }

    // Backward-compatible convenience overload (older code may pass name/address/phone)
    public void Update(string name, string? addressLine, string? phone)
        => SetValues(name, phone, addressLine, City, Country);

    public void SetActive(bool isActive, DateTime nowUtc)
    {
        IsActive = isActive;
        UpdatedAtUtc = nowUtc;
    }

    // Backward-compatible convenience overload
    public void SetActive(bool isActive) => IsActive = isActive;

    private void SetValues(string name, string? phone, string? addressLine, string? city, string? country)
    {
        Name = (name ?? "").Trim();
        Phone = phone?.Trim();
        AddressLine = addressLine?.Trim();
        City = city?.Trim();
        Country = country?.Trim();

        if (Name.Length < 2)
            throw new DomainException("Shop name is required (min 2 chars).");
    }
}
