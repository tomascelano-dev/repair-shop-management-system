using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SaasPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saas_account_entry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_account_entry", x => x.Id);
                    table.UniqueConstraint("AK_saas_account_entry_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "saas_alert",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Link = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_alert", x => x.Id);
                    table.UniqueConstraint("AK_saas_alert_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "saas_api_key",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Prefix = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Hash = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LastUsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_api_key", x => x.Id);
                    table.UniqueConstraint("AK_saas_api_key_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "saas_appointment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Phone = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Email = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DeviceLabel = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Address = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Source = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RecurrenceMonths = table.Column<int>(type: "integer", nullable: false),
                    PreviousId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReminderSentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_appointment", x => x.Id);
                    table.UniqueConstraint("AK_saas_appointment_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "saas_billing_event",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Kind = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_billing_event", x => x.Id);
                    table.UniqueConstraint("AK_saas_billing_event_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "saas_cash_session",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    OpenedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ClosedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OpeningCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OpeningCashUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CountedCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CountedCashUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExpectedCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExpectedCashUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SummaryJson = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_cash_session", x => x.Id);
                    table.UniqueConstraint("AK_saas_cash_session_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "saas_customer_contact",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DocType = table.Column<int>(type: "integer", nullable: false),
                    DocNumber = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    TaxCondition = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Address = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AcceptsMarketing = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_customer_contact", x => x.Id);
                    table.UniqueConstraint("AK_saas_customer_contact_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "saas_fiscal_invoice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherType = table.Column<int>(type: "integer", nullable: false),
                    PointOfSale = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: false),
                    Cae = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CaeDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DocType = table.Column<int>(type: "integer", nullable: false),
                    DocNumber = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CustomerTaxCondition = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Net = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Vat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LinesJson = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    Status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Environment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProviderMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CancelsInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_fiscal_invoice", x => x.Id);
                    table.UniqueConstraint("AK_saas_fiscal_invoice_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "saas_fiscal_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Environment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PointOfSale = table.Column<int>(type: "integer", nullable: false),
                    ProtectedCertificate = table.Column<string>(type: "character varying(40000)", maxLength: 40000, nullable: false),
                    ProtectedPrivateKey = table.Column<string>(type: "character varying(40000)", maxLength: 40000, nullable: false),
                    CertificateExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DefaultVatRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_fiscal_settings", x => x.Id);
                    table.UniqueConstraint("AK_saas_fiscal_settings_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "saas_integration_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WooUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProtectedWooKey = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProtectedWooSecret = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LastSyncAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncResult = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_integration_settings", x => x.Id);
                    table.UniqueConstraint("AK_saas_integration_settings_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "saas_order_assignment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FieldAddress = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    State = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RunningSinceUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WorkedMinutes = table.Column<int>(type: "integer", nullable: false),
                    ClosingReport = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_order_assignment", x => x.Id);
                    table.UniqueConstraint("AK_saas_order_assignment_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "saas_order_diagram",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Template = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    MarksJson = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_order_diagram", x => x.Id);
                    table.UniqueConstraint("AK_saas_order_diagram_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "saas_order_tracking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CustomerEmail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LastPickupReminderAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_order_tracking", x => x.Id);
                    table.UniqueConstraint("AK_saas_order_tracking_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "saas_portal_signature",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    SignerName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Accepted = table.Column<bool>(type: "boolean", nullable: false),
                    SignatureDataUrl = table.Column<string>(type: "character varying(400000)", maxLength: 400000, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_portal_signature", x => x.Id);
                    table.UniqueConstraint("AK_saas_portal_signature_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "saas_service_catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Code = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Category = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EstimatedMinutes = table.Column<int>(type: "integer", nullable: false),
                    WarrantyDays = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_service_catalog", x => x.Id);
                    table.UniqueConstraint("AK_saas_service_catalog_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "saas_shop_profile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Slug = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LegalName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    TaxId = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    TaxCondition = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Email = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Phone = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Address = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    City = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Website = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LogoDataUrl = table.Column<string>(type: "character varying(400000)", maxLength: 400000, nullable: false),
                    PrimaryColor = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReceiptFooter = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RequireSignature = table.Column<bool>(type: "boolean", nullable: false),
                    OnlineBookingEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    OpeningHoursJson = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    NotifyEmail = table.Column<bool>(type: "boolean", nullable: false),
                    NotifySms = table.Column<bool>(type: "boolean", nullable: false),
                    SurveysEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PickupReminderDays = table.Column<int>(type: "integer", nullable: false),
                    WeeklySummaryEmail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OnboardingStep = table.Column<int>(type: "integer", nullable: false),
                    OnboardingCompleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_shop_profile", x => x.Id);
                    table.UniqueConstraint("AK_saas_shop_profile_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "saas_subscription",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Plan = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    TrialEndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrentPeriodEndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Provider = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProviderSubscriptionId = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PendingPlan = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PayerEmail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_subscription", x => x.Id);
                    table.UniqueConstraint("AK_saas_subscription_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "saas_survey",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AnsweredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_survey", x => x.Id);
                    table.UniqueConstraint("AK_saas_survey_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "saas_webhook_endpoint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Events = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProtectedSecret = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_webhook_endpoint", x => x.Id);
                    table.UniqueConstraint("AK_saas_webhook_endpoint_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "saas_cash_movement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Method = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_cash_movement", x => x.Id);
                    table.UniqueConstraint("AK_saas_cash_movement_ShopId_Id", x => new { x.ShopId, x.Id });
                    table.ForeignKey(
                        name: "FK_saas_cash_movement_saas_cash_session_ShopId_SessionId",
                        columns: x => new { x.ShopId, x.SessionId },
                        principalTable: "saas_cash_session",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "saas_counter_sale",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LinesJson = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Discount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Method = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    VoidReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_counter_sale", x => x.Id);
                    table.UniqueConstraint("AK_saas_counter_sale_ShopId_Id", x => new { x.ShopId, x.Id });
                    table.ForeignKey(
                        name: "FK_saas_counter_sale_saas_cash_session_ShopId_SessionId",
                        columns: x => new { x.ShopId, x.SessionId },
                        principalTable: "saas_cash_session",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "saas_webhook_delivery",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndpointId = table.Column<Guid>(type: "uuid", nullable: false),
                    Event = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PayloadJson = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    Status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResponseCode = table.Column<int>(type: "integer", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_webhook_delivery", x => x.Id);
                    table.UniqueConstraint("AK_saas_webhook_delivery_ShopId_Id", x => new { x.ShopId, x.Id });
                    table.ForeignKey(
                        name: "FK_saas_webhook_delivery_saas_webhook_endpoint_ShopId_Endpoint~",
                        columns: x => new { x.ShopId, x.EndpointId },
                        principalTable: "saas_webhook_endpoint",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_saas_account_entry_ShopId",
                table: "saas_account_entry",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_saas_account_entry_ShopId_CustomerId",
                table: "saas_account_entry",
                columns: new[] { "ShopId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_saas_alert_ShopId",
                table: "saas_alert",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_saas_alert_ShopId_CreatedAtUtc",
                table: "saas_alert",
                columns: new[] { "ShopId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_saas_api_key_Hash",
                table: "saas_api_key",
                column: "Hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saas_api_key_ShopId",
                table: "saas_api_key",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_saas_appointment_ShopId",
                table: "saas_appointment",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_saas_appointment_ShopId_StartsAtUtc",
                table: "saas_appointment",
                columns: new[] { "ShopId", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_saas_billing_event_ShopId",
                table: "saas_billing_event",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_saas_cash_movement_ShopId",
                table: "saas_cash_movement",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_saas_cash_movement_ShopId_SessionId",
                table: "saas_cash_movement",
                columns: new[] { "ShopId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_saas_cash_session_ShopId",
                table: "saas_cash_session",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_saas_cash_session_ShopId_BranchId",
                table: "saas_cash_session",
                columns: new[] { "ShopId", "BranchId" },
                unique: true,
                filter: "\"Status\" = 'Open'");

            migrationBuilder.CreateIndex(
                name: "IX_saas_counter_sale_ShopId",
                table: "saas_counter_sale",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_saas_counter_sale_ShopId_Number",
                table: "saas_counter_sale",
                columns: new[] { "ShopId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saas_counter_sale_ShopId_SessionId",
                table: "saas_counter_sale",
                columns: new[] { "ShopId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_saas_customer_contact_ShopId",
                table: "saas_customer_contact",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_saas_customer_contact_ShopId_CustomerId",
                table: "saas_customer_contact",
                columns: new[] { "ShopId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saas_fiscal_invoice_ShopId",
                table: "saas_fiscal_invoice",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_saas_fiscal_invoice_ShopId_PointOfSale_VoucherType_Number",
                table: "saas_fiscal_invoice",
                columns: new[] { "ShopId", "PointOfSale", "VoucherType", "Number" },
                unique: true,
                filter: "\"Status\" = 'Authorized'");

            migrationBuilder.CreateIndex(
                name: "IX_saas_fiscal_invoice_ShopId_SourceType_SourceId",
                table: "saas_fiscal_invoice",
                columns: new[] { "ShopId", "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_saas_fiscal_settings_ShopId_unique",
                table: "saas_fiscal_settings",
                column: "ShopId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saas_integration_settings_ShopId_unique",
                table: "saas_integration_settings",
                column: "ShopId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saas_order_assignment_ShopId",
                table: "saas_order_assignment",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_saas_order_assignment_ShopId_OrderId",
                table: "saas_order_assignment",
                columns: new[] { "ShopId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saas_order_assignment_ShopId_TechnicianId",
                table: "saas_order_assignment",
                columns: new[] { "ShopId", "TechnicianId" });

            migrationBuilder.CreateIndex(
                name: "IX_saas_order_diagram_ShopId",
                table: "saas_order_diagram",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_saas_order_diagram_ShopId_OrderId",
                table: "saas_order_diagram",
                columns: new[] { "ShopId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saas_order_tracking_Code",
                table: "saas_order_tracking",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saas_order_tracking_ShopId",
                table: "saas_order_tracking",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_saas_order_tracking_ShopId_OrderId",
                table: "saas_order_tracking",
                columns: new[] { "ShopId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saas_portal_signature_ShopId",
                table: "saas_portal_signature",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_saas_service_catalog_ShopId",
                table: "saas_service_catalog",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_saas_service_catalog_ShopId_Code",
                table: "saas_service_catalog",
                columns: new[] { "ShopId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saas_shop_profile_ShopId_unique",
                table: "saas_shop_profile",
                column: "ShopId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saas_shop_profile_Slug",
                table: "saas_shop_profile",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saas_subscription_ProviderSubscriptionId",
                table: "saas_subscription",
                column: "ProviderSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_saas_subscription_ShopId_unique",
                table: "saas_subscription",
                column: "ShopId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saas_survey_ShopId",
                table: "saas_survey",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_saas_survey_ShopId_OrderId",
                table: "saas_survey",
                columns: new[] { "ShopId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saas_survey_TokenHash",
                table: "saas_survey",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saas_webhook_delivery_ShopId",
                table: "saas_webhook_delivery",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_saas_webhook_delivery_ShopId_EndpointId",
                table: "saas_webhook_delivery",
                columns: new[] { "ShopId", "EndpointId" });

            migrationBuilder.CreateIndex(
                name: "IX_saas_webhook_delivery_Status_NextAttemptAtUtc",
                table: "saas_webhook_delivery",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_saas_webhook_endpoint_ShopId",
                table: "saas_webhook_endpoint",
                column: "ShopId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saas_account_entry");

            migrationBuilder.DropTable(
                name: "saas_alert");

            migrationBuilder.DropTable(
                name: "saas_api_key");

            migrationBuilder.DropTable(
                name: "saas_appointment");

            migrationBuilder.DropTable(
                name: "saas_billing_event");

            migrationBuilder.DropTable(
                name: "saas_cash_movement");

            migrationBuilder.DropTable(
                name: "saas_counter_sale");

            migrationBuilder.DropTable(
                name: "saas_customer_contact");

            migrationBuilder.DropTable(
                name: "saas_fiscal_invoice");

            migrationBuilder.DropTable(
                name: "saas_fiscal_settings");

            migrationBuilder.DropTable(
                name: "saas_integration_settings");

            migrationBuilder.DropTable(
                name: "saas_order_assignment");

            migrationBuilder.DropTable(
                name: "saas_order_diagram");

            migrationBuilder.DropTable(
                name: "saas_order_tracking");

            migrationBuilder.DropTable(
                name: "saas_portal_signature");

            migrationBuilder.DropTable(
                name: "saas_service_catalog");

            migrationBuilder.DropTable(
                name: "saas_shop_profile");

            migrationBuilder.DropTable(
                name: "saas_subscription");

            migrationBuilder.DropTable(
                name: "saas_survey");

            migrationBuilder.DropTable(
                name: "saas_webhook_delivery");

            migrationBuilder.DropTable(
                name: "saas_cash_session");

            migrationBuilder.DropTable(
                name: "saas_webhook_endpoint");
        }
    }
}
