using RepairShop.Domain.Premium;

namespace RepairShop.Domain.Saas;

// SaaS records reuse the premium tenant contract: every row belongs to one shop and carries an optimistic version.

public sealed class ShopSubscription : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Plan { get; set; } = "Pro";
    public string Status { get; set; } = "Trialing";
    public DateTime TrialEndsAtUtc { get; set; }
    public DateTime? CurrentPeriodEndsAtUtc { get; set; }
    public string Provider { get; set; } = "";
    public string? ProviderSubscriptionId { get; set; }
    public string? PendingPlan { get; set; }
    public string? PayerEmail { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "ARS";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class BillingEvent : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Kind { get; set; } = "";
    public string Detail { get; set; } = "";
    public string? ProviderReference { get; set; }
}

public sealed class ShopProfile : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Slug { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string LegalName { get; set; } = "";
    public string TaxId { get; set; } = "";
    // ResponsableInscripto | Monotributo | Exento
    public string TaxCondition { get; set; } = "Monotributo";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string Website { get; set; } = "";
    public string LogoDataUrl { get; set; } = "";
    public string PrimaryColor { get; set; } = "#2563eb";
    public string ReceiptFooter { get; set; } = "";
    public bool RequireSignature { get; set; }
    public bool OnlineBookingEnabled { get; set; }
    public string OpeningHoursJson { get; set; } = "{\"days\":[1,2,3,4,5],\"from\":\"09:00\",\"to\":\"18:00\",\"slotMinutes\":30}";
    public bool NotifyEmail { get; set; } = true;
    public bool NotifySms { get; set; }
    public bool SurveysEnabled { get; set; } = true;
    public int PickupReminderDays { get; set; } = 7;
    public string WeeklySummaryEmail { get; set; } = "";
    public int OnboardingStep { get; set; }
    public bool OnboardingCompleted { get; set; }
}

public sealed class FiscalSettings : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool Enabled { get; set; }
    // Homologacion | Produccion
    public string Environment { get; set; } = "Homologacion";
    public int PointOfSale { get; set; } = 1;
    public string ProtectedCertificate { get; set; } = "";
    public string ProtectedPrivateKey { get; set; } = "";
    public DateTime? CertificateExpiresAtUtc { get; set; }
    public decimal DefaultVatRate { get; set; } = 21m;
}

public sealed class CustomerContact : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid CustomerId { get; set; }
    public string Email { get; set; } = "";
    // 80 CUIT | 86 CUIL | 96 DNI | 99 consumidor final sin identificar
    public int DocType { get; set; } = 99;
    public string DocNumber { get; set; } = "";
    // ConsumidorFinal | ResponsableInscripto | Monotributo | Exento
    public string TaxCondition { get; set; } = "ConsumidorFinal";
    public string Address { get; set; } = "";
    public bool AcceptsMarketing { get; set; }
}

public sealed class CashSession : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? BranchId { get; set; }
    public Guid OpenedBy { get; set; }
    public Guid? ClosedBy { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string Status { get; set; } = "Open";
    public decimal OpeningCash { get; set; }
    public decimal OpeningCashUsd { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? CountedCashUsd { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? ExpectedCashUsd { get; set; }
    public string SummaryJson { get; set; } = "{}";
    public string Notes { get; set; } = "";
}

public sealed class CashMovement : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid SessionId { get; set; }
    // Sale | OrderPayment | AccountPayment | Income | Expense | Refund | Void
    public string Kind { get; set; } = "";
    // Cash | Card | Transfer | MercadoPago | Account
    public string Method { get; set; } = "Cash";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ARS";
    public string Description { get; set; } = "";
    public Guid? ReferenceId { get; set; }
    public Guid ActorId { get; set; }
}

public sealed class CounterSale : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int Number { get; set; }
    public Guid SessionId { get; set; }
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = "Consumidor final";
    public string LinesJson { get; set; } = "[]";
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "ARS";
    public string Method { get; set; } = "Cash";
    public string Status { get; set; } = "Completed";
    public string VoidReason { get; set; } = "";
    public Guid? InvoiceId { get; set; }
    public Guid ActorId { get; set; }
}

public sealed class AccountEntry : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid CustomerId { get; set; }
    // Charge increases what the customer owes; Payment reduces it.
    public string Kind { get; set; } = "Charge";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ARS";
    public string Description { get; set; } = "";
    public Guid? ReferenceId { get; set; }
    public Guid ActorId { get; set; }
}

public sealed class FiscalInvoice : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    // Order | Sale
    public string SourceType { get; set; } = "";
    public Guid SourceId { get; set; }
    // 1 Factura A | 6 Factura B | 11 Factura C | 3/8/13 notas de crédito
    public int VoucherType { get; set; }
    public int PointOfSale { get; set; }
    public long Number { get; set; }
    public string Cae { get; set; } = "";
    public DateTime? CaeDueDate { get; set; }
    public string CustomerName { get; set; } = "";
    public int DocType { get; set; } = 99;
    public string DocNumber { get; set; } = "0";
    public string CustomerTaxCondition { get; set; } = "ConsumidorFinal";
    public decimal Net { get; set; }
    public decimal Vat { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "ARS";
    public decimal ExchangeRate { get; set; } = 1m;
    public string LinesJson { get; set; } = "[]";
    // Authorized | Rejected | Simulated
    public string Status { get; set; } = "";
    public string Environment { get; set; } = "";
    public string ProviderMessage { get; set; } = "";
    public Guid? CancelsInvoiceId { get; set; }
    public Guid ActorId { get; set; }
}

public sealed class Appointment : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string DeviceLabel { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime StartsAtUtc { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public Guid? TechnicianId { get; set; }
    // Workshop | Field
    public string Kind { get; set; } = "Workshop";
    public string Address { get; set; } = "";
    // Booked | Confirmed | Done | NoShow | Cancelled | Converted
    public string Status { get; set; } = "Booked";
    // Staff | Online
    public string Source { get; set; } = "Staff";
    public int RecurrenceMonths { get; set; }
    public Guid? PreviousId { get; set; }
    public Guid? OrderId { get; set; }
    public DateTime? ReminderSentAtUtc { get; set; }
}

public sealed class OrderTracking : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid OrderId { get; set; }
    public string Code { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public DateTime? LastPickupReminderAtUtc { get; set; }
}

public sealed class PortalSignature : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid OrderId { get; set; }
    public Guid QuoteId { get; set; }
    public string SignerName { get; set; } = "";
    public bool Accepted { get; set; }
    public string SignatureDataUrl { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public string UserAgent { get; set; } = "";
}

public sealed class SatisfactionSurvey : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid OrderId { get; set; }
    public string TokenHash { get; set; } = "";
    public int? Score { get; set; }
    public string Comment { get; set; } = "";
    public DateTime? AnsweredAtUtc { get; set; }
}

public sealed class OrderAssignment : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid OrderId { get; set; }
    public Guid TechnicianId { get; set; }
    // Workshop | Field
    public string Mode { get; set; } = "Workshop";
    public string FieldAddress { get; set; } = "";
    public DateTime? ScheduledAtUtc { get; set; }
    // Assigned | Working | Paused | Finished
    public string State { get; set; } = "Assigned";
    public DateTime? RunningSinceUtc { get; set; }
    public int WorkedMinutes { get; set; }
    public string ClosingReport { get; set; } = "";
    public DateTime? FinishedAtUtc { get; set; }
}

public sealed class OrderDiagram : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid OrderId { get; set; }
    // phone | tablet | laptop | console | other
    public string Template { get; set; } = "phone";
    public string MarksJson { get; set; } = "[]";
}

public sealed class ServiceCatalogItem : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    public decimal EstimatedCost { get; set; }
    public string Currency { get; set; } = "ARS";
    public int EstimatedMinutes { get; set; }
    public int WarrantyDays { get; set; } = 90;
    public bool Active { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ShopAlert : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; }
    public string Kind { get; set; } = "";
    public string Message { get; set; } = "";
    public Guid? OrderId { get; set; }
    public string Link { get; set; } = "";
    public DateTime? ReadAtUtc { get; set; }
}

public sealed class ApiKey : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Name { get; set; } = "";
    public string Prefix { get; set; } = "";
    public string Hash { get; set; } = "";
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}

public sealed class WebhookEndpoint : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Url { get; set; } = "";
    public string Events { get; set; } = "*";
    public string ProtectedSecret { get; set; } = "";
    public bool Active { get; set; } = true;
}

public sealed class WebhookDelivery : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid EndpointId { get; set; }
    public string Event { get; set; } = "";
    public string PayloadJson { get; set; } = "{}";
    // Pending | Sent | Failed | Dead
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTime NextAttemptAtUtc { get; set; } = DateTime.UtcNow;
    public int? ResponseCode { get; set; }
    public string LastError { get; set; } = "";
}

public sealed class IntegrationSettings : IPremiumRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string WooUrl { get; set; } = "";
    public string ProtectedWooKey { get; set; } = "";
    public string ProtectedWooSecret { get; set; } = "";
    public DateTime? LastSyncAtUtc { get; set; }
    public string LastSyncResult { get; set; } = "";
}
