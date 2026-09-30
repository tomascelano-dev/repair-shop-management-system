using RepairShop.Domain.Common;

namespace RepairShop.Domain.Devices;

public sealed class Device
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    // Multi-sucursal
    public Guid ShopId { get; private set; }
    public Guid CustomerId { get; private set; }

    public string Brand { get; private set; } = null!;
    public string Model { get; private set; } = null!;

    // UX: etiqueta opcional (ej: "iPhone 12 - negro")
    public string? Label { get; private set; }
    public string? SerialNumber { get; private set; }
    public string? Notes { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private Device() { } // EF

    public Device(Guid shopId, Guid customerId, string brand, string model, string? label, string? serialNumber, string? notes, DateTime nowUtc)
    {
        ShopId = shopId;
        CustomerId = customerId;
        Brand = (brand ?? "").Trim();
        Model = (model ?? "").Trim();
        Label = label?.Trim();
        SerialNumber = serialNumber?.Trim();
        Notes = notes?.Trim();
        CreatedAtUtc = nowUtc;

        if (CustomerId == Guid.Empty) throw new DomainException("Device must belong to a customer.");
        if (Brand.Length < 2) throw new DomainException("Device brand is required.");
        if (Model.Length < 2) throw new DomainException("Device model is required.");
    }

    // Back-compat: constructor anterior sin ShopId/Label.
    public Device(Guid customerId, string brand, string model, string? serialNumber, string? notes, DateTime nowUtc)
        : this(Guid.Empty, customerId, brand, model, null, serialNumber, notes, nowUtc)
    {
    }

    public void Update(string brand, string model, string? label, string? serialNumber, string? notes)
    {
        Brand = (brand ?? "").Trim();
        Model = (model ?? "").Trim();
        Label = label?.Trim();
        SerialNumber = serialNumber?.Trim();
        Notes = notes?.Trim();

        if (Brand.Length < 2) throw new DomainException("Device brand is required.");
        if (Model.Length < 2) throw new DomainException("Device model is required.");
    }

    // Back-compat
    public void Update(string brand, string model, string? serialNumber, string? notes)
        => Update(brand, model, null, serialNumber, notes);
}
