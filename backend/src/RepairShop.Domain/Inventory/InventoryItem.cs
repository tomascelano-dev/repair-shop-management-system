using RepairShop.Domain.Common;

namespace RepairShop.Domain.Inventory;

public sealed class InventoryItem : IShopScoped
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }

    public string Sku { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Category { get; private set; }

    // EAN/UPC or any scannable code. Unique per shop when present.
    public string? Barcode { get; private set; }

    public int QuantityOnHand { get; private set; }

    // Reorder point: an alert is raised when available stock drops to this level.
    public int MinStock { get; private set; }

    // Items like services ("colocación de vidrio") don't track stock.
    public bool TrackStock { get; private set; } = true;

    // Accessories sold at the counter (POS).
    public bool IsSellable { get; private set; }

    public decimal? UnitCost { get; private set; }
    public string? UnitCostCurrency { get; private set; }

    public decimal? SalePrice { get; private set; }
    public string? SalePriceCurrency { get; private set; }

    public int? WarrantyDays { get; private set; }
    public string? Location { get; private set; }

    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private InventoryItem() { }

    public InventoryItem(Guid shopId, string sku, string name, int initialQty, decimal? unitCost, string? unitCostCurrency, bool isActive, DateTime nowUtc)
    {
        ShopId = shopId;
        Sku = NormalizeSku(sku);
        Name = NormalizeName(name);
        QuantityOnHand = initialQty;
        SetCost(unitCost, unitCostCurrency);
        IsActive = isActive;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;

        if (QuantityOnHand < 0) throw new DomainException("El stock no puede ser negativo.");
    }

    public void Update(string name, bool isActive, DateTime nowUtc)
    {
        Name = NormalizeName(name);
        IsActive = isActive;
        UpdatedAtUtc = nowUtc;
    }

    public void UpdateCatalog(
        string? category,
        string? barcode,
        int minStock,
        bool trackStock,
        bool isSellable,
        decimal? salePrice,
        string? salePriceCurrency,
        int? warrantyDays,
        string? location,
        DateTime nowUtc)
    {
        if (minStock < 0) throw new DomainException("El stock mínimo no puede ser negativo.");
        if (warrantyDays is < 0 or > 3650) throw new DomainException("La garantía debe estar entre 0 y 3650 días.");
        if (salePrice is < 0) throw new DomainException("El precio de venta no puede ser negativo.");
        if (isSellable && salePrice is null) throw new DomainException("Para vender en mostrador el ítem necesita precio de venta.");

        Category = Clean(category, 60);
        Barcode = NormalizeBarcode(barcode);
        MinStock = minStock;
        TrackStock = trackStock;
        IsSellable = isSellable;
        SalePrice = salePrice is null ? null : Money.Round(salePrice.Value);
        SalePriceCurrency = salePrice is null ? null : Money.NormalizeCurrency(salePriceCurrency);
        WarrantyDays = warrantyDays;
        Location = Clean(location, 60);
        UpdatedAtUtc = nowUtc;
    }

    public void UpdateCost(decimal? unitCost, string? currency, DateTime nowUtc)
    {
        SetCost(unitCost, currency);
        UpdatedAtUtc = nowUtc;
    }

    public void ApplyDelta(int deltaQty, DateTime nowUtc)
    {
        if (!TrackStock)
        {
            UpdatedAtUtc = nowUtc;
            return;
        }

        var next = QuantityOnHand + deltaQty;
        if (next < 0) throw new DomainException($"Stock insuficiente para \"{Name}\" (disponible: {QuantityOnHand}).");
        QuantityOnHand = next;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Receives stock from a purchase updating the weighted average cost.
    /// </summary>
    public void ReceivePurchase(int quantity, decimal unitCost, string currency, DateTime nowUtc)
    {
        if (quantity <= 0) throw new DomainException("La cantidad recibida debe ser mayor a 0.");
        if (unitCost < 0) throw new DomainException("El costo no puede ser negativo.");
        currency = Money.NormalizeCurrency(currency);

        if (UnitCost is null || UnitCostCurrency != currency || QuantityOnHand <= 0)
        {
            UnitCost = Money.Round(unitCost);
        }
        else
        {
            var total = (QuantityOnHand * UnitCost.Value) + (quantity * unitCost);
            UnitCost = Money.Round(total / (QuantityOnHand + quantity));
        }

        UnitCostCurrency = currency;
        if (TrackStock) QuantityOnHand += quantity;
        UpdatedAtUtc = nowUtc;
    }

    public bool IsLowStock(int reserved) => TrackStock && IsActive && QuantityOnHand - reserved <= MinStock;

    private void SetCost(decimal? unitCost, string? currency)
    {
        if (unitCost is < 0) throw new DomainException("El costo no puede ser negativo.");
        UnitCost = unitCost is null ? null : Money.Round(unitCost.Value);
        UnitCostCurrency = unitCost is null ? null : string.IsNullOrWhiteSpace(currency) ? null : Money.NormalizeCurrency(currency);
    }

    private static string NormalizeName(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length < 2) throw new DomainException("El nombre del ítem es obligatorio.");
        if (name.Length > 160) throw new DomainException("El nombre del ítem es demasiado largo (máx. 160).");
        return name;
    }

    private static string NormalizeSku(string sku)
    {
        sku = (sku ?? "").Trim().ToUpperInvariant();
        if (sku.Length < 2) throw new DomainException("El SKU es obligatorio.");
        if (sku.Length > 60) throw new DomainException("El SKU es demasiado largo (máx. 60).");
        return sku;
    }

    private static string? NormalizeBarcode(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return null;
        barcode = barcode.Trim();
        if (barcode.Length > 64) throw new DomainException("El código de barras es demasiado largo (máx. 64).");
        return barcode;
    }

    private static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        return value.Length > max ? value[..max] : value;
    }
}
