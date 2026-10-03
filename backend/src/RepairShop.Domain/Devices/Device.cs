using RepairShop.Domain.Common;

namespace RepairShop.Domain.Devices;

public sealed class Device : IShopScoped
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

    // 15-digit IMEI (validated with Luhn). Optional.
    public string? Imei { get; private set; }

    public string? Notes { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Device() { } // EF

    public Device(Guid shopId, Guid customerId, string brand, string model, string? label, string? serialNumber, string? notes, DateTime nowUtc)
    {
        ShopId = shopId;
        CustomerId = customerId;
        SetValues(brand, model, label, serialNumber, notes);
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;

        if (CustomerId == Guid.Empty) throw new DomainException("El equipo debe pertenecer a un cliente.");
    }

    // Back-compat: constructor anterior sin ShopId/Label.
    public Device(Guid customerId, string brand, string model, string? serialNumber, string? notes, DateTime nowUtc)
        : this(Guid.Empty, customerId, brand, model, null, serialNumber, notes, nowUtc)
    {
    }

    public void Update(string brand, string model, string? label, string? serialNumber, string? notes)
        => SetValues(brand, model, label, serialNumber, notes);

    public void Update(string brand, string model, string? label, string? serialNumber, string? notes, DateTime nowUtc)
    {
        SetValues(brand, model, label, serialNumber, notes);
        UpdatedAtUtc = nowUtc;
    }

    // Back-compat
    public void Update(string brand, string model, string? serialNumber, string? notes)
        => Update(brand, model, null, serialNumber, notes);

    public void SetImei(string? imei, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(imei))
        {
            Imei = null;
        }
        else
        {
            var digits = PhoneNumber.Digits(imei);
            if (!ImeiValidator.IsValid(digits)) throw new DomainException("El IMEI es inválido (15 dígitos con dígito verificador).");
            Imei = digits;
        }

        UpdatedAtUtc = nowUtc;
    }

    public void ReassignCustomer(Guid customerId, DateTime nowUtc)
    {
        if (customerId == Guid.Empty) throw new DomainException("El equipo debe pertenecer a un cliente.");
        CustomerId = customerId;
        UpdatedAtUtc = nowUtc;
    }

    public string DisplayName => string.IsNullOrWhiteSpace(Label) ? $"{Brand} {Model}" : $"{Brand} {Model} ({Label})";

    private void SetValues(string brand, string model, string? label, string? serialNumber, string? notes)
    {
        Brand = (brand ?? "").Trim();
        Model = (model ?? "").Trim();
        Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        SerialNumber = string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        if (Brand.Length < 2) throw new DomainException("La marca del equipo es obligatoria.");
        if (Model.Length < 2) throw new DomainException("El modelo del equipo es obligatorio.");
    }
}

public static class ImeiValidator
{
    public static bool IsValid(string? imei)
    {
        if (imei is null || imei.Length != 15 || !imei.All(char.IsDigit)) return false;

        var sum = 0;
        for (var i = 0; i < 15; i++)
        {
            var d = imei[i] - '0';
            // Luhn: double every second digit (from the left, positions 2,4,...,14).
            if (i % 2 == 1)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
        }

        return sum % 10 == 0;
    }
}
