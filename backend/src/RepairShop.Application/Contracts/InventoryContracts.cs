using System.ComponentModel.DataAnnotations;
using RepairShop.Domain.Inventory;

namespace RepairShop.Application.Contracts;

public sealed record InventoryItemResponse(
    Guid Id,
    Guid ShopId,
    string Sku,
    string Name,
    int QuantityOnHand,
    decimal? UnitCost,
    string? UnitCostCurrency,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? Category = null,
    string? Barcode = null,
    int MinStock = 0,
    bool TrackStock = true,
    bool IsSellable = false,
    decimal? SalePrice = null,
    string? SalePriceCurrency = null,
    int? WarrantyDays = null,
    string? Location = null,
    int ReservedQuantity = 0,
    int AvailableQuantity = 0,
    bool IsLowStock = false
);

public sealed record CreateInventoryItemRequest(
    [Required, MinLength(2)] string Sku,
    [Required, MinLength(2)] string Name,
    int InitialQuantity = 0,
    decimal? UnitCost = null,
    string? UnitCostCurrency = null,
    bool IsActive = true,
    string? Category = null,
    string? Barcode = null,
    int MinStock = 0,
    bool TrackStock = true,
    bool IsSellable = false,
    decimal? SalePrice = null,
    string? SalePriceCurrency = null,
    int? WarrantyDays = null,
    string? Location = null
);

public sealed record UpdateInventoryItemRequest(
    [Required, MinLength(2)] string Name,
    bool IsActive = true,
    string? Category = null,
    string? Barcode = null,
    int MinStock = 0,
    bool TrackStock = true,
    bool IsSellable = false,
    decimal? SalePrice = null,
    string? SalePriceCurrency = null,
    int? WarrantyDays = null,
    string? Location = null,
    decimal? UnitCost = null,
    string? UnitCostCurrency = null
);

public sealed record CreateInventoryAdjustmentRequest(
    [Required] InventoryAdjustmentType Type,
    [Required] int DeltaQuantity,
    string? Reason
);

public sealed record InventoryAdjustmentResponse(
    Guid Id,
    Guid InventoryItemId,
    InventoryAdjustmentType Type,
    int DeltaQuantity,
    string? Reason,
    Guid? RepairOrderId,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    string? ReferenceType = null,
    Guid? ReferenceId = null
);

public sealed record UsePartOnOrderRequest(
    [Required] Guid InventoryItemId,
    [Required] int QuantityUsed,
    decimal? UnitPrice,
    string? UnitPriceCurrency
);

public sealed record RepairOrderPartUsageResponse(
    Guid Id,
    Guid RepairOrderId,
    Guid InventoryItemId,
    int QuantityUsed,
    decimal? UnitPrice,
    string? UnitPriceCurrency,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    string? ItemName = null,
    string? ItemSku = null,
    bool ChargedToCustomer = false,
    decimal? UnitCost = null,
    bool CoveredByQuote = false
);

public sealed record CompatibilityRequest([Required, MinLength(2)] string Brand, [Required, MinLength(1)] string Model);

public sealed record CompatibilityResponse(Guid Id, string Brand, string Model);

public sealed record ReservationResponse(Guid Id, Guid InventoryItemId, string ItemName, Guid RepairOrderId, int Quantity, int ConsumedQuantity, string Status, DateTime CreatedAtUtc);

// Purchasing
public sealed record SupplierRequest(
    [Required, MinLength(2)] string Name,
    string? ContactName,
    string? Phone,
    string? Email,
    string? TaxId,
    string? Notes,
    bool IsActive = true);

public sealed record SupplierResponse(Guid Id, string Name, string? ContactName, string? Phone, string? Email, string? TaxId, string? Notes, bool IsActive, DateTime CreatedAtUtc);

public sealed record PurchaseOrderLineRequest([Required] Guid InventoryItemId, int Quantity, decimal UnitCost, string? Description = null);

public sealed record SavePurchaseOrderRequest(
    [Required] Guid SupplierId,
    [Required, MinLength(3)] string Currency,
    [Required] IReadOnlyList<PurchaseOrderLineRequest> Lines,
    string? Notes = null,
    DateTime? ExpectedAtUtc = null);

public sealed record ReceivePurchaseOrderRequest([Required] IReadOnlyList<ReceiveLineRequest> Lines);

public sealed record ReceiveLineRequest([Required] Guid LineId, int Quantity);

public sealed record PurchaseOrderLineResponse(Guid Id, Guid InventoryItemId, string Description, int Quantity, decimal UnitCost, int ReceivedQuantity, decimal LineTotal);

public sealed record PurchaseOrderResponse(
    Guid Id,
    int Number,
    string Code,
    Guid SupplierId,
    string? SupplierName,
    string Status,
    string Currency,
    decimal Total,
    string? Notes,
    DateTime? ExpectedAtUtc,
    DateTime CreatedAtUtc,
    DateTime? OrderedAtUtc,
    DateTime? ReceivedAtUtc,
    IReadOnlyList<PurchaseOrderLineResponse> Lines);

// Transfers between branches
public sealed record TransferLineRequest([Required] Guid InventoryItemId, int Quantity);

public sealed record CreateTransferRequest([Required] Guid ToShopId, [Required] IReadOnlyList<TransferLineRequest> Lines, string? Notes = null);

public sealed record TransferLineResponse(Guid Id, Guid SourceItemId, string Sku, string Name, int Quantity);

public sealed record TransferResponse(
    Guid Id,
    string Code,
    Guid FromShopId,
    string? FromShopName,
    Guid ToShopId,
    string? ToShopName,
    string Status,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? ReceivedAtUtc,
    bool IsIncoming,
    IReadOnlyList<TransferLineResponse> Lines);
