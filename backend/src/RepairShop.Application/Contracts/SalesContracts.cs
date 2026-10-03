using System.ComponentModel.DataAnnotations;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.Contracts;

public sealed record SaleLineRequest(
    Guid? InventoryItemId,
    string? Description,
    int Quantity,
    decimal? UnitPrice = null,
    decimal DiscountAmount = 0,
    decimal? DiscountPercent = null);

public sealed record SalePaymentRequest([Required] PaymentMethod Method, decimal Amount, string? Reference = null);

public sealed record CreateSaleRequest(
    [Required] IReadOnlyList<SaleLineRequest> Lines,
    [Required] IReadOnlyList<SalePaymentRequest> Payments,
    Guid? CustomerId = null,
    string? Currency = null,
    decimal DiscountAmount = 0,
    decimal? DiscountPercent = null,
    string? Notes = null);

public sealed record RefundSaleRequest(
    [Required] IReadOnlyList<RefundSaleLineRequest> Lines,
    [Required] PaymentMethod Method,
    bool Restock = true,
    string? Reason = null);

public sealed record RefundSaleLineRequest([Required] Guid SaleLineId, int Quantity);

public sealed record VoidSaleRequest([Required, MinLength(3)] string Reason, PaymentMethod Method = PaymentMethod.Cash);

public sealed record SaleLineResponse(
    Guid Id,
    int Position,
    Guid? InventoryItemId,
    string Sku,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal LineTotal,
    int RefundedQuantity,
    int? WarrantyDays);

public sealed record SalePaymentResponse(Guid Id, string Method, decimal Amount, string? Reference);

public sealed record SaleRefundResponse(Guid Id, decimal Amount, string Method, bool Restocked, string? Reason, DateTime CreatedAtUtc);

public sealed record SaleResponse(
    Guid Id,
    int Number,
    string Code,
    string Status,
    Guid? CustomerId,
    string? CustomerName,
    string Currency,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal Total,
    decimal PaidAmount,
    decimal ChangeAmount,
    decimal RefundedAmount,
    Guid? CashSessionId,
    string? Notes,
    Guid CreatedByUserId,
    string? CreatedByName,
    DateTime CreatedAtUtc,
    string? VoidReason,
    IReadOnlyList<SaleLineResponse> Lines,
    IReadOnlyList<SalePaymentResponse> Payments,
    IReadOnlyList<SaleRefundResponse> Refunds);

public sealed record PosCatalogItem(
    Guid Id,
    string Sku,
    string? Barcode,
    string Name,
    string? Category,
    decimal? SalePrice,
    string? Currency,
    bool TrackStock,
    int Available,
    int? WarrantyDays);
