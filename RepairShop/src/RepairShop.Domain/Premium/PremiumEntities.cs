namespace RepairShop.Domain.Premium;

public interface IPremiumRecord { Guid Id { get; set; } Guid ShopId { get; set; } int Version { get; set; } }

public sealed class RefurbEvent : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid DeviceId { get; set; }
    public string Message { get; set; } = "";
}

public sealed class PremiumBranch : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
}

public sealed class SupplierPrice : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Supplier { get; set; } = "";
    public string Sku { get; set; } = "";
    public string Description { get; set; } = "";
    public string Compatibility { get; set; } = "";
    public string Quality { get; set; } = "";
    public decimal UnitCost { get; set; }
    public string Currency { get; set; } = "ARS";
    public string Source { get; set; } = "";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class StockLot : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid ItemId { get; set; }
    public Guid BranchId { get; set; }
    public string Supplier { get; set; } = "";
    public string LotCode { get; set; } = "";
    public string? Serial { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string Currency { get; set; } = "ARS";
}

public sealed class StockReservation : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid LotId { get; set; }
    public Guid OrderId { get; set; }
    public int Quantity { get; set; }
    public string Status { get; set; } = "Reserved";
    public decimal UnitCost { get; set; }
    public string Currency { get; set; } = "ARS";
    public DateTime? ConsumedAtUtc { get; set; }
}

public sealed class StockMovement : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid LotId { get; set; }
    public Guid? OrderId { get; set; }
    public string Kind { get; set; } = "";
    public int Quantity { get; set; }
    public string Reason { get; set; } = "";
    public Guid ActorId { get; set; }
}

public sealed class StockMinimum : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid ItemId { get; set; }
    public Guid BranchId { get; set; }
    public int Minimum { get; set; }
}

public sealed class OrderExpense : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid OrderId { get; set; }
    public string Kind { get; set; } = "Other";
    public string Description { get; set; } = "";
    public int Minutes { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ARS";
    public Guid ActorId { get; set; }
}

public sealed class WarrantyCase : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid OrderId { get; set; }
    public Guid? ReservationId { get; set; }
    public string Problem { get; set; } = "";
    public string FailureCode { get; set; } = "";
    public string Supplier { get; set; } = "";
    public string SupplierClaim { get; set; } = "";
    public string Status { get; set; } = "Open";
    public decimal Cost { get; set; }
    public decimal Recovered { get; set; }
    public string Currency { get; set; } = "ARS";
    public string Resolution { get; set; } = "";
    public bool CoveredAtIntake { get; set; }
    public DateTime? WarrantyEndsAtUtc { get; set; }
}

public sealed class RefurbDevice : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Model { get; set; } = "";
    public string Identifier { get; set; } = "";
    public string Seller { get; set; } = "";
    public string Acquisition { get; set; } = "Buy";
    public string Grade { get; set; } = "B";
    public string Diagnosis { get; set; } = "";
    public string QualityChecksJson { get; set; } = "{}";
    public decimal PurchasePrice { get; set; }
    public decimal TargetPrice { get; set; }
    public string Currency { get; set; } = "ARS";
    public string Status { get; set; } = "Received";
    public decimal? SalePrice { get; set; }
    public string? Buyer { get; set; }
    public DateTime? SoldAtUtc { get; set; }
    public Guid BranchId { get; set; }
}

public sealed class RefurbExpense : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid DeviceId { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
}

public sealed class CompanyContract : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid CustomerId { get; set; }
    public string CompanyName { get; set; } = "";
    public string Contact { get; set; } = "";
    public string Phone { get; set; } = "";
    public decimal MonthlyFee { get; set; }
    public decimal ExtraOrderRate { get; set; }
    public int IncludedOrders { get; set; }
    public int SlaHours { get; set; } = 72;
    public string Currency { get; set; } = "ARS";
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public string Status { get; set; } = "Active";
    public string Terms { get; set; } = "";
}

public sealed class CompanyEquipment : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid ContractId { get; set; }
    public Guid DeviceId { get; set; }
    public string Label { get; set; } = "";
    public string Identifier { get; set; } = "";
}

public sealed class OrderBusiness : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid OrderId { get; set; }
    public Guid BranchId { get; set; }
    public Guid? ContractId { get; set; }
    public Guid? EquipmentId { get; set; }
    public string BatchReference { get; set; } = "";
    public DateTime? DueAtUtc { get; set; }
}

public sealed class ContractSettlement : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid ContractId { get; set; }
    public string Period { get; set; } = "";
    public int CompletedOrders { get; set; }
    public int ExtraOrders { get; set; }
    public decimal MonthlyFee { get; set; }
    public decimal ExtraRate { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "ARS";
    public string OrderIdsJson { get; set; } = "[]";
    public string Status { get; set; } = "Pending";
    public DateTime? PaidAtUtc { get; set; }
}

