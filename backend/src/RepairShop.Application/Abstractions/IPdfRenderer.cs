namespace RepairShop.Application.Abstractions;

public sealed record DocShop(string Name, string? LegalName, string? TaxId, string? TaxCondition, string? Address, string? City, string? Phone, string? Email, byte[]? Logo);

public sealed record DocParty(string Name, string? Phone, string? Email, string? Document);

public sealed record DocKeyValue(string Label, string Value);

public sealed record DocLine(string Description, decimal Quantity, decimal UnitPrice, decimal Discount, decimal Total);

public sealed record IntakeReceiptModel(
    DocShop Shop,
    string OrderCode,
    DateTime CreatedAtLocal,
    DocParty Customer,
    string Device,
    string? Serial,
    string? Imei,
    string Issue,
    string? Notes,
    IReadOnlyList<DocKeyValue> Checklist,
    string? CosmeticNotes,
    string? UnlockInfo,
    string? Terms,
    byte[]? Signature,
    string? SignedBy,
    DateTime? PromisedAtLocal,
    string? TechnicianName,
    IReadOnlyList<DocKeyValue> Payments,
    string TrackingUrl);

public sealed record LabelModel(string ShopName, string OrderCode, string CustomerName, string Device, DateTime CreatedAtLocal, string Issue, string TrackingUrl);

public sealed record QuoteDocumentModel(
    DocShop Shop,
    string OrderCode,
    int Version,
    string StatusLabel,
    DateTime CreatedAtLocal,
    DateTime? ValidUntilLocal,
    DocParty Customer,
    string Device,
    string Issue,
    IReadOnlyList<DocLine> Lines,
    decimal Subtotal,
    decimal Discount,
    decimal Total,
    string Currency,
    int? WarrantyDays,
    string? Notes,
    string TrackingUrl);

public sealed record PaymentReceiptModel(
    DocShop Shop,
    string ReceiptCode,
    DateTime DateLocal,
    DocParty Customer,
    string Concept,
    string Method,
    decimal Amount,
    string Currency,
    bool IsRefund,
    decimal OrderTotal,
    decimal Paid,
    decimal Balance,
    string? Reference,
    string? Device);

public sealed record SaleTicketModel(
    DocShop Shop,
    string Code,
    DateTime DateLocal,
    string? CustomerName,
    IReadOnlyList<DocLine> Lines,
    decimal Subtotal,
    decimal Discount,
    decimal Total,
    IReadOnlyList<DocKeyValue> Payments,
    decimal Change,
    string Currency,
    string? CashierName,
    string Status,
    IReadOnlyList<string> Footer);

public sealed record WarrantyCertificateModel(
    DocShop Shop,
    string OrderCode,
    DocParty Customer,
    string Device,
    string? Serial,
    string Issue,
    IReadOnlyList<string> Work,
    DateTime DeliveredLocal,
    int Days,
    DateTime? ExpiresLocal,
    string? Terms,
    byte[]? Signature,
    string? SignedBy,
    string TrackingUrl);

public sealed record InvoiceDocumentModel(
    DocShop Shop,
    string Letter,
    string VoucherName,
    int VoucherCode,
    int PointOfSale,
    long? Number,
    DateTime DateLocal,
    DocParty Receiver,
    string ReceiverTaxCondition,
    IReadOnlyList<DocLine> Lines,
    decimal Net,
    decimal Vat,
    decimal Total,
    string? Cae,
    DateTime? CaeDue,
    string? QrUrl,
    string Status);

public sealed record CashReportModel(
    DocShop Shop,
    int Number,
    string Status,
    DateTime OpenedAtLocal,
    string? OpenedBy,
    DateTime? ClosedAtLocal,
    string? ClosedBy,
    decimal OpeningCash,
    string Currency,
    IReadOnlyList<Contracts.CashSummaryLine> Summary,
    IReadOnlyList<DocKeyValue> Movements,
    decimal? Expected,
    decimal? Counted,
    decimal? Difference,
    string? Notes);

/// <summary>Renders printable documents as PDF (A4, 80mm tickets and labels).</summary>
public interface IPdfRenderer
{
    byte[] IntakeReceipt(IntakeReceiptModel model);
    byte[] Label(LabelModel model);
    byte[] Quote(QuoteDocumentModel model);
    byte[] PaymentReceipt(PaymentReceiptModel model);
    byte[] SaleTicket(SaleTicketModel model);
    byte[] WarrantyCertificate(WarrantyCertificateModel model);
    byte[] Invoice(InvoiceDocumentModel model);
    byte[] CashReport(CashReportModel model);
}
