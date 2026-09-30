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
    DateTime UpdatedAtUtc
);

public sealed record CreateInventoryItemRequest(
    [Required, MinLength(2)] string Sku,
    [Required, MinLength(2)] string Name,
    int InitialQuantity = 0,
    decimal? UnitCost = null,
    string? UnitCostCurrency = null,
    bool IsActive = true
);

public sealed record UpdateInventoryItemRequest(
    [Required, MinLength(2)] string Name,
    bool IsActive = true
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
    DateTime CreatedAtUtc
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
    DateTime CreatedAtUtc
);
