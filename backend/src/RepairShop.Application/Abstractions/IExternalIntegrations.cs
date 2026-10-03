using RepairShop.Domain.Fiscal;
using RepairShop.Domain.Shops;

namespace RepairShop.Application.Abstractions;

// ===== Mercado Pago =====

public interface IMercadoPagoClient
{
    Task<MpPreference> CreatePreferenceAsync(string accessToken, MpPreferenceRequest request, CancellationToken ct);
    Task<MpPayment?> GetPaymentAsync(string accessToken, string paymentId, CancellationToken ct);
}

public sealed record MpPreferenceRequest(
    string Title,
    decimal Amount,
    string Currency,
    string ExternalReference,
    string NotificationUrl,
    string BackUrl,
    DateTime ExpiresAtUtc,
    string? PayerEmail);

public sealed record MpPreference(string Id, string InitPoint);

public sealed record MpPayment(string Id, string Status, string? ExternalReference, decimal TransactionAmount, string Currency, DateTime? ApprovedAtUtc);

// ===== ARCA electronic invoicing =====

public interface IFiscalAuthority
{
    Task<FiscalAuthorizationResult> AuthorizeAsync(FiscalCredentials credentials, FiscalVoucherRequest voucher, CancellationToken ct);
}

public sealed record FiscalCredentials(string Cuit, int PointOfSale, FiscalEnvironment Environment, byte[] CertificatePfx, string? CertificatePassword);

public sealed record FiscalVatLine(int VatId, decimal BaseAmount, decimal Amount);

public sealed record FiscalVoucherRequest(
    FiscalVoucherType Type,
    int Concept,
    int DocumentType,
    long DocumentNumber,
    int ReceiverIvaConditionId,
    DateOnly Date,
    decimal NetAmount,
    decimal VatAmount,
    decimal Total,
    IReadOnlyList<FiscalVatLine> VatLines,
    DateOnly? ServiceFrom,
    DateOnly? ServiceTo,
    DateOnly? PaymentDue,
    FiscalVoucherType? AssociatedType,
    int? AssociatedPointOfSale,
    long? AssociatedNumber);

public sealed record FiscalAuthorizationResult(bool Approved, long Number, string? Cae, DateOnly? CaeDueDate, string Message);

// ===== Exchange rates =====

public interface IExchangeRateProvider
{
    Task<IReadOnlyList<FetchedRate>> FetchAsync(CancellationToken ct);
}

public sealed record FetchedRate(string Source, string BaseCurrency, string QuoteCurrency, decimal Rate, DateOnly Date);
