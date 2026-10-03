using RepairShop.Domain.Customers;
using RepairShop.Domain.Fiscal;

namespace RepairShop.Application.Contracts;

public sealed record PaymentLinkResponse(Guid Id, string Provider, string Url, decimal Amount, string Currency, string Status, DateTime ExpiresAtUtc, DateTime CreatedAtUtc, DateTime? PaidAtUtc);

public sealed record MercadoPagoNotification(string? Type, string? DataId, string? RequestId, string? Signature);

public sealed record IssueInvoiceRequest(
    CustomerDocumentType? DocumentType = null,
    string? DocumentNumber = null,
    string? ReceiverName = null,
    CustomerTaxCondition? ReceiverTaxCondition = null);

public sealed record FiscalInvoiceResponse(
    Guid Id,
    string SourceType,
    Guid SourceId,
    string VoucherType,
    string Letter,
    int PointOfSale,
    long? Number,
    string Code,
    DateTime IssueDateUtc,
    string ReceiverName,
    string? ReceiverDocumentNumber,
    decimal NetAmount,
    decimal VatAmount,
    decimal Total,
    string Status,
    string? Cae,
    DateTime? CaeDueDate,
    string? ResultMessage,
    Guid? AssociatedInvoiceId);

public sealed record ExchangeRateResponse(Guid Id, DateOnly Date, string BaseCurrency, string QuoteCurrency, string Source, decimal Rate, DateTime UpdatedAtUtc);

public sealed record SetExchangeRateRequest(DateOnly Date, decimal Rate, string BaseCurrency = "USD", string QuoteCurrency = "ARS", string Source = "manual");
