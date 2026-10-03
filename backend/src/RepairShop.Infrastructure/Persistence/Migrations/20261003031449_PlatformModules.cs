using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RepairShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PlatformModulesUpgradeSql.BeforeSchemaChanges);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginAtUtc",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PendingTokenExpiresAtUtc",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingTokenHash",
                table: "users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingTokenPurpose",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DefaultCurrency",
                table: "shops",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "ARS");

            migrationBuilder.AddColumn<int>(
                name: "DefaultNotificationChannel",
                table: "shops",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefaultWarrantyDays",
                table: "shops",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "shops",
                type: "character varying(180)",
                maxLength: 180,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoogleReviewUrl",
                table: "shops",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalName",
                table: "shops",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LogoFileId",
                table: "shops",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotificationsEnabled",
                table: "shops",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "shops",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "PhoneCountryCode",
                table: "shops",
                type: "character varying(4)",
                maxLength: 4,
                nullable: false,
                defaultValue: "54");

            migrationBuilder.AddColumn<string>(
                name: "PickupHours",
                table: "shops",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuoteValidityDays",
                table: "shops",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReadyReminderDays",
                table: "shops",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "7,15,30");

            migrationBuilder.AddColumn<string>(
                name: "ReceptionTerms",
                table: "shops",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportingCurrency",
                table: "shops",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "ARS");

            migrationBuilder.AddColumn<bool>(
                name: "RequireOpenCashSession",
                table: "shops",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SendFeedbackSurvey",
                table: "shops",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "StaleOrderDays",
                table: "shops",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TaxCondition",
                table: "shops",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TaxId",
                table: "shops",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "shops",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "America/Argentina/Buenos_Aires");

            migrationBuilder.AddColumn<string>(
                name: "WarrantyTerms",
                table: "shops",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "QuoteUpdatedByUserId",
                table: "repair_orders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "QuoteUpdatedAtUtc",
                table: "repair_orders",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<decimal>(
                name: "QuoteAmount",
                table: "repair_orders",
                type: "numeric(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "repair_orders",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(800)",
                oldMaxLength: 800,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedTechnicianId",
                table: "repair_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "repair_orders",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAtUtc",
                table: "repair_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAtUtc",
                table: "repair_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeliverySignatureFileId",
                table: "repair_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliverySignedAtUtc",
                table: "repair_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliverySignedByName",
                table: "repair_orders",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsWarrantyClaim",
                table: "repair_orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IssueCategory",
                table: "repair_orders",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastStatusChangeAtUtc",
                table: "repair_orders",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "OrderNumber",
                table: "repair_orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "repair_orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PromisedAtUtc",
                table: "repair_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicToken",
                table: "repair_orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadyAtUtc",
                table: "repair_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReceptionSignatureFileId",
                table: "repair_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceptionSignedAtUtc",
                table: "repair_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceptionSignedByName",
                table: "repair_orders",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnlockMethod",
                table: "repair_orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UnlockSecretProtected",
                table: "repair_orders",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UnlockSecretPurgedAtUtc",
                table: "repair_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarrantyDays",
                table: "repair_orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WarrantyExpiresAtUtc",
                table: "repair_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WarrantyOfOrderId",
                table: "repair_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "repair_orders",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "order_status_history",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BatteryPercent",
                table: "order_reception_checklists",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<Guid>(
                name: "CashSessionId",
                table: "order_payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalPaymentId",
                table: "order_payments",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeposit",
                table: "order_payments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "RefundOfPaymentId",
                table: "order_payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "order_payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "order_part_usage",
                type: "numeric(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AddColumn<bool>(
                name: "ChargedToCustomer",
                table: "order_part_usage",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ReservationId",
                table: "order_part_usage",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "order_part_usage",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitCostCurrency",
                table: "order_part_usage",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "order_notes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "order_attachments",
                type: "character varying(800)",
                maxLength: 800,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(800)",
                oldMaxLength: 800);

            migrationBuilder.AddColumn<Guid>(
                name: "FileId",
                table: "order_attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "order_attachments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "notification_outbox",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "RelatedEntityId",
                table: "notification_outbox",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "Recipient",
                table: "notification_outbox",
                type: "character varying(180)",
                maxLength: 180,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<DateTime>(
                name: "NextAttemptAtUtc",
                table: "notification_outbox",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "notification_outbox",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "notification_outbox",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderMessageId",
                table: "notification_outbox",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentAtUtc",
                table: "notification_outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateKey",
                table: "notification_outbox",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "inventory_items",
                type: "numeric(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "inventory_items",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "inventory_items",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSellable",
                table: "inventory_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "inventory_items",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinStock",
                table: "inventory_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "SalePrice",
                table: "inventory_items",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalePriceCurrency",
                table: "inventory_items",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TrackStock",
                table: "inventory_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "WarrantyDays",
                table: "inventory_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "inventory_items",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AlterColumn<Guid>(
                name: "RepairOrderId",
                table: "inventory_adjustments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ReferenceId",
                table: "inventory_adjustments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceType",
                table: "inventory_adjustments",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Imei",
                table: "devices",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "devices",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "customers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentNumber",
                table: "customers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DocumentType",
                table: "customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "customers",
                type: "character varying(180)",
                maxLength: 180,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MarketingOptIn",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotificationsOptIn",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PhoneKey",
                table: "customers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "customers",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaxCondition",
                table: "customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "customers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorUserId",
                table: "audit_events",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.Sql(PlatformModulesUpgradeSql.BackfillNewColumns);

            migrationBuilder.CreateTable(
                name: "cash_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OpeningCash = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OpeningNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CountedCash = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ExpectedCash = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Difference = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ClosingNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClosingSummaryJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cash_sessions_shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_feedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepairOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_feedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_feedback_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customer_feedback_repair_orders_RepairOrderId",
                        column: x => x.RepairOrderId,
                        principalTable: "repair_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_customer_feedback_shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "data_protection_keys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_protection_keys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "exchange_rates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    BaseCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    QuoteCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exchange_rates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "fiscal_invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherType = table.Column<int>(type: "integer", nullable: false),
                    PointOfSale = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: true),
                    IssueDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Concept = table.Column<int>(type: "integer", nullable: false),
                    ReceiverDocumentType = table.Column<int>(type: "integer", nullable: false),
                    ReceiverDocumentNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ReceiverName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ReceiverTaxCondition = table.Column<int>(type: "integer", nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Cae = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CaeDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResultMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AssociatedInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiscal_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fiscal_invoices_fiscal_invoices_AssociatedInvoiceId",
                        column: x => x.AssociatedInvoiceId,
                        principalTable: "fiscal_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fiscal_invoices_shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Completed = table.Column<bool>(type: "boolean", nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    ResponseBody = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_item_compatibilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Brand = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Model = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    BrandKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ModelKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_item_compatibilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_item_compatibilities_inventory_items_InventoryIte~",
                        column: x => x.InventoryItemId,
                        principalTable: "inventory_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_item_compatibilities_shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_qa_checklists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepairOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PowersOn = table.Column<bool>(type: "boolean", nullable: true),
                    ScreenOk = table.Column<bool>(type: "boolean", nullable: true),
                    TouchOk = table.Column<bool>(type: "boolean", nullable: true),
                    CamerasOk = table.Column<bool>(type: "boolean", nullable: true),
                    AudioOk = table.Column<bool>(type: "boolean", nullable: true),
                    MicrophoneOk = table.Column<bool>(type: "boolean", nullable: true),
                    ButtonsOk = table.Column<bool>(type: "boolean", nullable: true),
                    ChargingOk = table.Column<bool>(type: "boolean", nullable: true),
                    ConnectivityOk = table.Column<bool>(type: "boolean", nullable: true),
                    BiometricsOk = table.Column<bool>(type: "boolean", nullable: true),
                    BatteryHealthPercent = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    CheckedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_qa_checklists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_qa_checklists_repair_orders_RepairOrderId",
                        column: x => x.RepairOrderId,
                        principalTable: "repair_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_order_qa_checklists_shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Url = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    ExternalPaymentId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_links_shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepairOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ValidUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WarrantyDays = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionSource = table.Column<int>(type: "integer", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DecisionIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quotes_repair_orders_RepairOrderId",
                        column: x => x.RepairOrderId,
                        principalTable: "repair_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_quotes_shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedReason = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ReplacedByTokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shop_counters",
                columns: table => new
                {
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Value = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_counters", x => new { x.ScopeId, x.Key });
                });

            migrationBuilder.CreateTable(
                name: "shop_integrations",
                columns: table => new
                {
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    MercadoPagoEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MercadoPagoAccessTokenProtected = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    MercadoPagoWebhookSecretProtected = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    FiscalEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    FiscalEnvironment = table.Column<int>(type: "integer", nullable: false),
                    FiscalPointOfSale = table.Column<int>(type: "integer", nullable: false),
                    FiscalCertificateProtected = table.Column<string>(type: "character varying(40000)", maxLength: 40000, nullable: true),
                    FiscalCertificatePasswordProtected = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_integrations", x => x.ShopId);
                    table.ForeignKey(
                        name: "FK_shop_integrations_shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_transfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    FromShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_transfers_shops_FromShopId",
                        column: x => x.FromShopId,
                        principalTable: "shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfers_shops_ToShopId",
                        column: x => x.ToShopId,
                        principalTable: "shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stored_files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    FileName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stored_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stored_files_shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ContactName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    TaxId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_suppliers_shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_shop_access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_shop_access", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_shop_access_shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_shop_access_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cash_movements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    RelatedEntityType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cash_movements_cash_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "cash_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_movements_shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ChangeAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CashSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VoidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_cash_sessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalTable: "cash_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepairOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ConsumedQuantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_reservations_inventory_items_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "inventory_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_reservations_quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_inventory_reservations_repair_orders_RepairOrderId",
                        column: x => x.RepairOrderId,
                        principalTable: "repair_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_reservations_shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quote_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    WarrantyDays = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quote_items_quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_transfer_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StockTransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    UnitCostCurrency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfer_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_transfer_lines_stock_transfers_StockTransferId",
                        column: x => x.StockTransferId,
                        principalTable: "stock_transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExpectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_orders_shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sku = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    TrackStock = table.Column<bool>(type: "boolean", nullable: false),
                    WarrantyDays = table.Column<int>(type: "integer", nullable: true),
                    RefundedQuantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sale_lines_sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sale_payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sale_payments_sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sale_refunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Restocked = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CashSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_refunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sale_refunds_sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_inventory_items_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "inventory_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_purchase_orders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "purchase_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sale_refund_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleRefundId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_refund_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sale_refund_lines_sale_refunds_SaleRefundId",
                        column: x => x.SaleRefundId,
                        principalTable: "sale_refunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(PlatformModulesUpgradeSql.SeedCounters);

            migrationBuilder.CreateIndex(
                name: "IX_users_PendingTokenHash",
                table: "users",
                column: "PendingTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_shops_OrganizationId",
                table: "shops",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_repair_orders_AssignedTechnicianId",
                table: "repair_orders",
                column: "AssignedTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_repair_orders_CustomerId",
                table: "repair_orders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_repair_orders_DeliverySignatureFileId",
                table: "repair_orders",
                column: "DeliverySignatureFileId");

            migrationBuilder.CreateIndex(
                name: "IX_repair_orders_DeviceId",
                table: "repair_orders",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_repair_orders_PublicToken",
                table: "repair_orders",
                column: "PublicToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_repair_orders_ReceptionSignatureFileId",
                table: "repair_orders",
                column: "ReceptionSignatureFileId");

            migrationBuilder.CreateIndex(
                name: "IX_repair_orders_ShopId_AssignedTechnicianId",
                table: "repair_orders",
                columns: new[] { "ShopId", "AssignedTechnicianId" });

            migrationBuilder.CreateIndex(
                name: "IX_repair_orders_ShopId_CreatedAtUtc",
                table: "repair_orders",
                columns: new[] { "ShopId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_repair_orders_ShopId_OrderNumber",
                table: "repair_orders",
                columns: new[] { "ShopId", "OrderNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_repair_orders_WarrantyOfOrderId",
                table: "repair_orders",
                column: "WarrantyOfOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_order_status_history_RepairOrderId",
                table: "order_status_history",
                column: "RepairOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_order_status_history_ShopId_ChangedAtUtc",
                table: "order_status_history",
                columns: new[] { "ShopId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_order_reception_checklists_RepairOrderId",
                table: "order_reception_checklists",
                column: "RepairOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_order_reception_checklists_ShopId",
                table: "order_reception_checklists",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_order_payments_CashSessionId",
                table: "order_payments",
                column: "CashSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_order_payments_RefundOfPaymentId",
                table: "order_payments",
                column: "RefundOfPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_order_payments_RepairOrderId",
                table: "order_payments",
                column: "RepairOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_order_payments_ShopId_CreatedAtUtc",
                table: "order_payments",
                columns: new[] { "ShopId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_order_payments_ShopId_ExternalPaymentId",
                table: "order_payments",
                columns: new[] { "ShopId", "ExternalPaymentId" },
                unique: true,
                filter: "\"ExternalPaymentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_order_part_usage_InventoryItemId",
                table: "order_part_usage",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_order_part_usage_RepairOrderId",
                table: "order_part_usage",
                column: "RepairOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_order_notes_RepairOrderId",
                table: "order_notes",
                column: "RepairOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_order_attachments_FileId",
                table: "order_attachments",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_order_attachments_RepairOrderId",
                table: "order_attachments",
                column: "RepairOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_ShopId_RelatedEntityType_RelatedEntityId",
                table: "notification_outbox",
                columns: new[] { "ShopId", "RelatedEntityType", "RelatedEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_Status_NextAttemptAtUtc",
                table: "notification_outbox",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_ShopId_Barcode",
                table: "inventory_items",
                columns: new[] { "ShopId", "Barcode" },
                unique: true,
                filter: "\"Barcode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustments_InventoryItemId",
                table: "inventory_adjustments",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustments_ShopId_CreatedAtUtc",
                table: "inventory_adjustments",
                columns: new[] { "ShopId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_devices_CustomerId",
                table: "devices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_devices_ShopId_Imei",
                table: "devices",
                columns: new[] { "ShopId", "Imei" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_ShopId_Email",
                table: "customers",
                columns: new[] { "ShopId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_ShopId_PhoneKey",
                table: "customers",
                columns: new[] { "ShopId", "PhoneKey" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_ShopId_CreatedAtUtc",
                table: "audit_events",
                columns: new[] { "ShopId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_cash_movements_SessionId",
                table: "cash_movements",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_movements_ShopId",
                table: "cash_movements",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_movements_ShopId_CreatedAtUtc",
                table: "cash_movements",
                columns: new[] { "ShopId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_cash_sessions_ShopId_Number",
                table: "cash_sessions",
                columns: new[] { "ShopId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_sessions_ShopId_open",
                table: "cash_sessions",
                column: "ShopId",
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_customer_feedback_CustomerId",
                table: "customer_feedback",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_feedback_RepairOrderId",
                table: "customer_feedback",
                column: "RepairOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_feedback_ShopId",
                table: "customer_feedback",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_feedback_ShopId_CreatedAtUtc",
                table: "customer_feedback",
                columns: new[] { "ShopId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_exchange_rates_Date_BaseCurrency_QuoteCurrency_Source",
                table: "exchange_rates",
                columns: new[] { "Date", "BaseCurrency", "QuoteCurrency", "Source" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_invoices_AssociatedInvoiceId",
                table: "fiscal_invoices",
                column: "AssociatedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_invoices_ShopId",
                table: "fiscal_invoices",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_invoices_ShopId_SourceType_SourceId",
                table: "fiscal_invoices",
                columns: new[] { "ShopId", "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_invoices_ShopId_VoucherType_PointOfSale_Number",
                table: "fiscal_invoices",
                columns: new[] { "ShopId", "VoucherType", "PointOfSale", "Number" },
                unique: true,
                filter: "\"Number\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_ExpiresAtUtc",
                table: "idempotency_records",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_item_compatibilities_InventoryItemId_BrandKey_Mod~",
                table: "inventory_item_compatibilities",
                columns: new[] { "InventoryItemId", "BrandKey", "ModelKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_item_compatibilities_ShopId",
                table: "inventory_item_compatibilities",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_item_compatibilities_ShopId_BrandKey_ModelKey",
                table: "inventory_item_compatibilities",
                columns: new[] { "ShopId", "BrandKey", "ModelKey" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_InventoryItemId",
                table: "inventory_reservations",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_QuoteId",
                table: "inventory_reservations",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_RepairOrderId",
                table: "inventory_reservations",
                column: "RepairOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_ShopId",
                table: "inventory_reservations",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_ShopId_InventoryItemId_Status",
                table: "inventory_reservations",
                columns: new[] { "ShopId", "InventoryItemId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_ShopId_RepairOrderId",
                table: "inventory_reservations",
                columns: new[] { "ShopId", "RepairOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_order_qa_checklists_RepairOrderId",
                table: "order_qa_checklists",
                column: "RepairOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_order_qa_checklists_ShopId",
                table: "order_qa_checklists",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_order_qa_checklists_ShopId_RepairOrderId",
                table: "order_qa_checklists",
                columns: new[] { "ShopId", "RepairOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_links_ShopId",
                table: "payment_links",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_links_ShopId_EntityType_EntityId",
                table: "payment_links",
                columns: new[] { "ShopId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_InventoryItemId",
                table: "purchase_order_lines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_PurchaseOrderId",
                table: "purchase_order_lines",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_ShopId",
                table: "purchase_orders",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_ShopId_Number",
                table: "purchase_orders",
                columns: new[] { "ShopId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_ShopId_Status",
                table: "purchase_orders",
                columns: new[] { "ShopId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_SupplierId",
                table: "purchase_orders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_quote_items_InventoryItemId",
                table: "quote_items",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_quote_items_QuoteId",
                table: "quote_items",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_quotes_RepairOrderId_Version",
                table: "quotes",
                columns: new[] { "RepairOrderId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quotes_ShopId",
                table: "quotes",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_quotes_ShopId_Status",
                table: "quotes",
                columns: new[] { "ShopId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_ExpiresAtUtc",
                table: "refresh_tokens",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_ShopId",
                table: "refresh_tokens",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_TokenHash",
                table: "refresh_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId_FamilyId",
                table: "refresh_tokens",
                columns: new[] { "UserId", "FamilyId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_lines_InventoryItemId",
                table: "sale_lines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_sale_lines_SaleId",
                table: "sale_lines",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_sale_payments_SaleId",
                table: "sale_payments",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_sale_refund_lines_SaleRefundId",
                table: "sale_refund_lines",
                column: "SaleRefundId");

            migrationBuilder.CreateIndex(
                name: "IX_sale_refunds_CashSessionId",
                table: "sale_refunds",
                column: "CashSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_sale_refunds_SaleId",
                table: "sale_refunds",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_CashSessionId",
                table: "sales",
                column: "CashSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_CustomerId",
                table: "sales",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_ShopId",
                table: "sales",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_ShopId_CreatedAtUtc",
                table: "sales",
                columns: new[] { "ShopId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_ShopId_Number",
                table: "sales",
                columns: new[] { "ShopId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_lines_StockTransferId",
                table: "stock_transfer_lines",
                column: "StockTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_FromShopId",
                table: "stock_transfers",
                column: "FromShopId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_OrganizationId_Number",
                table: "stock_transfers",
                columns: new[] { "OrganizationId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_ToShopId",
                table: "stock_transfers",
                column: "ToShopId");

            migrationBuilder.CreateIndex(
                name: "IX_stored_files_ShopId",
                table: "stored_files",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_ShopId",
                table: "suppliers",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_ShopId_Name",
                table: "suppliers",
                columns: new[] { "ShopId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_user_shop_access_ShopId",
                table: "user_shop_access",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_user_shop_access_UserId_ShopId",
                table: "user_shop_access",
                columns: new[] { "UserId", "ShopId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_audit_events_shops_ShopId",
                table: "audit_events",
                column: "ShopId",
                principalTable: "shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_customers_shops_ShopId",
                table: "customers",
                column: "ShopId",
                principalTable: "shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_devices_customers_CustomerId",
                table: "devices",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_devices_shops_ShopId",
                table: "devices",
                column: "ShopId",
                principalTable: "shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_adjustments_inventory_items_InventoryItemId",
                table: "inventory_adjustments",
                column: "InventoryItemId",
                principalTable: "inventory_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_adjustments_shops_ShopId",
                table: "inventory_adjustments",
                column: "ShopId",
                principalTable: "shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_items_shops_ShopId",
                table: "inventory_items",
                column: "ShopId",
                principalTable: "shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_message_templates_shops_ShopId",
                table: "message_templates",
                column: "ShopId",
                principalTable: "shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_notification_outbox_shops_ShopId",
                table: "notification_outbox",
                column: "ShopId",
                principalTable: "shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_attachments_repair_orders_RepairOrderId",
                table: "order_attachments",
                column: "RepairOrderId",
                principalTable: "repair_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_order_attachments_shops_ShopId",
                table: "order_attachments",
                column: "ShopId",
                principalTable: "shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_attachments_stored_files_FileId",
                table: "order_attachments",
                column: "FileId",
                principalTable: "stored_files",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_notes_repair_orders_RepairOrderId",
                table: "order_notes",
                column: "RepairOrderId",
                principalTable: "repair_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_order_notes_shops_ShopId",
                table: "order_notes",
                column: "ShopId",
                principalTable: "shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_part_usage_inventory_items_InventoryItemId",
                table: "order_part_usage",
                column: "InventoryItemId",
                principalTable: "inventory_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_part_usage_repair_orders_RepairOrderId",
                table: "order_part_usage",
                column: "RepairOrderId",
                principalTable: "repair_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_part_usage_shops_ShopId",
                table: "order_part_usage",
                column: "ShopId",
                principalTable: "shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_payments_order_payments_RefundOfPaymentId",
                table: "order_payments",
                column: "RefundOfPaymentId",
                principalTable: "order_payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_payments_repair_orders_RepairOrderId",
                table: "order_payments",
                column: "RepairOrderId",
                principalTable: "repair_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_payments_shops_ShopId",
                table: "order_payments",
                column: "ShopId",
                principalTable: "shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_reception_checklists_repair_orders_RepairOrderId",
                table: "order_reception_checklists",
                column: "RepairOrderId",
                principalTable: "repair_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_order_reception_checklists_shops_ShopId",
                table: "order_reception_checklists",
                column: "ShopId",
                principalTable: "shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_status_history_repair_orders_RepairOrderId",
                table: "order_status_history",
                column: "RepairOrderId",
                principalTable: "repair_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_order_status_history_shops_ShopId",
                table: "order_status_history",
                column: "ShopId",
                principalTable: "shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_repair_orders_customers_CustomerId",
                table: "repair_orders",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_repair_orders_devices_DeviceId",
                table: "repair_orders",
                column: "DeviceId",
                principalTable: "devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_repair_orders_repair_orders_WarrantyOfOrderId",
                table: "repair_orders",
                column: "WarrantyOfOrderId",
                principalTable: "repair_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_repair_orders_shops_ShopId",
                table: "repair_orders",
                column: "ShopId",
                principalTable: "shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_repair_orders_stored_files_DeliverySignatureFileId",
                table: "repair_orders",
                column: "DeliverySignatureFileId",
                principalTable: "stored_files",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_repair_orders_stored_files_ReceptionSignatureFileId",
                table: "repair_orders",
                column: "ReceptionSignatureFileId",
                principalTable: "stored_files",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_repair_orders_users_AssignedTechnicianId",
                table: "repair_orders",
                column: "AssignedTechnicianId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_users_shops_ShopId",
                table: "users",
                column: "ShopId",
                principalTable: "shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_events_shops_ShopId",
                table: "audit_events");

            migrationBuilder.DropForeignKey(
                name: "FK_customers_shops_ShopId",
                table: "customers");

            migrationBuilder.DropForeignKey(
                name: "FK_devices_customers_CustomerId",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "FK_devices_shops_ShopId",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "FK_inventory_adjustments_inventory_items_InventoryItemId",
                table: "inventory_adjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_inventory_adjustments_shops_ShopId",
                table: "inventory_adjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_inventory_items_shops_ShopId",
                table: "inventory_items");

            migrationBuilder.DropForeignKey(
                name: "FK_message_templates_shops_ShopId",
                table: "message_templates");

            migrationBuilder.DropForeignKey(
                name: "FK_notification_outbox_shops_ShopId",
                table: "notification_outbox");

            migrationBuilder.DropForeignKey(
                name: "FK_order_attachments_repair_orders_RepairOrderId",
                table: "order_attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_order_attachments_shops_ShopId",
                table: "order_attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_order_attachments_stored_files_FileId",
                table: "order_attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_order_notes_repair_orders_RepairOrderId",
                table: "order_notes");

            migrationBuilder.DropForeignKey(
                name: "FK_order_notes_shops_ShopId",
                table: "order_notes");

            migrationBuilder.DropForeignKey(
                name: "FK_order_part_usage_inventory_items_InventoryItemId",
                table: "order_part_usage");

            migrationBuilder.DropForeignKey(
                name: "FK_order_part_usage_repair_orders_RepairOrderId",
                table: "order_part_usage");

            migrationBuilder.DropForeignKey(
                name: "FK_order_part_usage_shops_ShopId",
                table: "order_part_usage");

            migrationBuilder.DropForeignKey(
                name: "FK_order_payments_order_payments_RefundOfPaymentId",
                table: "order_payments");

            migrationBuilder.DropForeignKey(
                name: "FK_order_payments_repair_orders_RepairOrderId",
                table: "order_payments");

            migrationBuilder.DropForeignKey(
                name: "FK_order_payments_shops_ShopId",
                table: "order_payments");

            migrationBuilder.DropForeignKey(
                name: "FK_order_reception_checklists_repair_orders_RepairOrderId",
                table: "order_reception_checklists");

            migrationBuilder.DropForeignKey(
                name: "FK_order_reception_checklists_shops_ShopId",
                table: "order_reception_checklists");

            migrationBuilder.DropForeignKey(
                name: "FK_order_status_history_repair_orders_RepairOrderId",
                table: "order_status_history");

            migrationBuilder.DropForeignKey(
                name: "FK_order_status_history_shops_ShopId",
                table: "order_status_history");

            migrationBuilder.DropForeignKey(
                name: "FK_repair_orders_customers_CustomerId",
                table: "repair_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_repair_orders_devices_DeviceId",
                table: "repair_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_repair_orders_repair_orders_WarrantyOfOrderId",
                table: "repair_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_repair_orders_shops_ShopId",
                table: "repair_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_repair_orders_stored_files_DeliverySignatureFileId",
                table: "repair_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_repair_orders_stored_files_ReceptionSignatureFileId",
                table: "repair_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_repair_orders_users_AssignedTechnicianId",
                table: "repair_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_users_shops_ShopId",
                table: "users");

            migrationBuilder.DropTable(
                name: "cash_movements");

            migrationBuilder.DropTable(
                name: "customer_feedback");

            migrationBuilder.DropTable(
                name: "data_protection_keys");

            migrationBuilder.DropTable(
                name: "exchange_rates");

            migrationBuilder.DropTable(
                name: "fiscal_invoices");

            migrationBuilder.DropTable(
                name: "idempotency_records");

            migrationBuilder.DropTable(
                name: "inventory_item_compatibilities");

            migrationBuilder.DropTable(
                name: "inventory_reservations");

            migrationBuilder.DropTable(
                name: "order_qa_checklists");

            migrationBuilder.DropTable(
                name: "payment_links");

            migrationBuilder.DropTable(
                name: "purchase_order_lines");

            migrationBuilder.DropTable(
                name: "quote_items");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "sale_lines");

            migrationBuilder.DropTable(
                name: "sale_payments");

            migrationBuilder.DropTable(
                name: "sale_refund_lines");

            migrationBuilder.DropTable(
                name: "shop_counters");

            migrationBuilder.DropTable(
                name: "shop_integrations");

            migrationBuilder.DropTable(
                name: "stock_transfer_lines");

            migrationBuilder.DropTable(
                name: "stored_files");

            migrationBuilder.DropTable(
                name: "user_shop_access");

            migrationBuilder.DropTable(
                name: "purchase_orders");

            migrationBuilder.DropTable(
                name: "quotes");

            migrationBuilder.DropTable(
                name: "sale_refunds");

            migrationBuilder.DropTable(
                name: "stock_transfers");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "sales");

            migrationBuilder.DropTable(
                name: "cash_sessions");

            migrationBuilder.DropIndex(
                name: "IX_users_PendingTokenHash",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_shops_OrganizationId",
                table: "shops");

            migrationBuilder.DropIndex(
                name: "IX_repair_orders_AssignedTechnicianId",
                table: "repair_orders");

            migrationBuilder.DropIndex(
                name: "IX_repair_orders_CustomerId",
                table: "repair_orders");

            migrationBuilder.DropIndex(
                name: "IX_repair_orders_DeliverySignatureFileId",
                table: "repair_orders");

            migrationBuilder.DropIndex(
                name: "IX_repair_orders_DeviceId",
                table: "repair_orders");

            migrationBuilder.DropIndex(
                name: "IX_repair_orders_PublicToken",
                table: "repair_orders");

            migrationBuilder.DropIndex(
                name: "IX_repair_orders_ReceptionSignatureFileId",
                table: "repair_orders");

            migrationBuilder.DropIndex(
                name: "IX_repair_orders_ShopId_AssignedTechnicianId",
                table: "repair_orders");

            migrationBuilder.DropIndex(
                name: "IX_repair_orders_ShopId_CreatedAtUtc",
                table: "repair_orders");

            migrationBuilder.DropIndex(
                name: "IX_repair_orders_ShopId_OrderNumber",
                table: "repair_orders");

            migrationBuilder.DropIndex(
                name: "IX_repair_orders_WarrantyOfOrderId",
                table: "repair_orders");

            migrationBuilder.DropIndex(
                name: "IX_order_status_history_RepairOrderId",
                table: "order_status_history");

            migrationBuilder.DropIndex(
                name: "IX_order_status_history_ShopId_ChangedAtUtc",
                table: "order_status_history");

            migrationBuilder.DropIndex(
                name: "IX_order_reception_checklists_RepairOrderId",
                table: "order_reception_checklists");

            migrationBuilder.DropIndex(
                name: "IX_order_reception_checklists_ShopId",
                table: "order_reception_checklists");

            migrationBuilder.DropIndex(
                name: "IX_order_payments_CashSessionId",
                table: "order_payments");

            migrationBuilder.DropIndex(
                name: "IX_order_payments_RefundOfPaymentId",
                table: "order_payments");

            migrationBuilder.DropIndex(
                name: "IX_order_payments_RepairOrderId",
                table: "order_payments");

            migrationBuilder.DropIndex(
                name: "IX_order_payments_ShopId_CreatedAtUtc",
                table: "order_payments");

            migrationBuilder.DropIndex(
                name: "IX_order_payments_ShopId_ExternalPaymentId",
                table: "order_payments");

            migrationBuilder.DropIndex(
                name: "IX_order_part_usage_InventoryItemId",
                table: "order_part_usage");

            migrationBuilder.DropIndex(
                name: "IX_order_part_usage_RepairOrderId",
                table: "order_part_usage");

            migrationBuilder.DropIndex(
                name: "IX_order_notes_RepairOrderId",
                table: "order_notes");

            migrationBuilder.DropIndex(
                name: "IX_order_attachments_FileId",
                table: "order_attachments");

            migrationBuilder.DropIndex(
                name: "IX_order_attachments_RepairOrderId",
                table: "order_attachments");

            migrationBuilder.DropIndex(
                name: "IX_notification_outbox_ShopId_RelatedEntityType_RelatedEntityId",
                table: "notification_outbox");

            migrationBuilder.DropIndex(
                name: "IX_notification_outbox_Status_NextAttemptAtUtc",
                table: "notification_outbox");

            migrationBuilder.DropIndex(
                name: "IX_inventory_items_ShopId_Barcode",
                table: "inventory_items");

            migrationBuilder.DropIndex(
                name: "IX_inventory_adjustments_InventoryItemId",
                table: "inventory_adjustments");

            migrationBuilder.DropIndex(
                name: "IX_inventory_adjustments_ShopId_CreatedAtUtc",
                table: "inventory_adjustments");

            migrationBuilder.DropIndex(
                name: "IX_devices_CustomerId",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "IX_devices_ShopId_Imei",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "IX_customers_ShopId_Email",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "IX_customers_ShopId_PhoneKey",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "IX_audit_events_ShopId_CreatedAtUtc",
                table: "audit_events");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "users");

            migrationBuilder.DropColumn(
                name: "LastLoginAtUtc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PendingTokenExpiresAtUtc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PendingTokenHash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PendingTokenPurpose",
                table: "users");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "users");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DefaultCurrency",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "DefaultNotificationChannel",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "DefaultWarrantyDays",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "GoogleReviewUrl",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "LegalName",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "LogoFileId",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "NotificationsEnabled",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "PhoneCountryCode",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "PickupHours",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "QuoteValidityDays",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "ReadyReminderDays",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "ReceptionTerms",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "ReportingCurrency",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "RequireOpenCashSession",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "SendFeedbackSurvey",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "StaleOrderDays",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "TaxCondition",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "TaxId",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "WarrantyTerms",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "AssignedTechnicianId",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "DeliveredAtUtc",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "DeliverySignatureFileId",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "DeliverySignedAtUtc",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "DeliverySignedByName",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "IsWarrantyClaim",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "IssueCategory",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "LastStatusChangeAtUtc",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "OrderNumber",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "PromisedAtUtc",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "PublicToken",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "ReadyAtUtc",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "ReceptionSignatureFileId",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "ReceptionSignedAtUtc",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "ReceptionSignedByName",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "UnlockMethod",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "UnlockSecretProtected",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "UnlockSecretPurgedAtUtc",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "WarrantyDays",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "WarrantyExpiresAtUtc",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "WarrantyOfOrderId",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "repair_orders");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "order_status_history");

            migrationBuilder.DropColumn(
                name: "CashSessionId",
                table: "order_payments");

            migrationBuilder.DropColumn(
                name: "ExternalPaymentId",
                table: "order_payments");

            migrationBuilder.DropColumn(
                name: "IsDeposit",
                table: "order_payments");

            migrationBuilder.DropColumn(
                name: "RefundOfPaymentId",
                table: "order_payments");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "order_payments");

            migrationBuilder.DropColumn(
                name: "ChargedToCustomer",
                table: "order_part_usage");

            migrationBuilder.DropColumn(
                name: "ReservationId",
                table: "order_part_usage");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "order_part_usage");

            migrationBuilder.DropColumn(
                name: "UnitCostCurrency",
                table: "order_part_usage");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "order_notes");

            migrationBuilder.DropColumn(
                name: "FileId",
                table: "order_attachments");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "order_attachments");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "notification_outbox");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "notification_outbox");

            migrationBuilder.DropColumn(
                name: "ProviderMessageId",
                table: "notification_outbox");

            migrationBuilder.DropColumn(
                name: "SentAtUtc",
                table: "notification_outbox");

            migrationBuilder.DropColumn(
                name: "TemplateKey",
                table: "notification_outbox");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "IsSellable",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "MinStock",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "SalePrice",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "SalePriceCurrency",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "TrackStock",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "WarrantyDays",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "inventory_adjustments");

            migrationBuilder.DropColumn(
                name: "ReferenceType",
                table: "inventory_adjustments");

            migrationBuilder.DropColumn(
                name: "Imei",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "DocumentNumber",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "MarketingOptIn",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "NotificationsOptIn",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "PhoneKey",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "TaxCondition",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "customers");

            migrationBuilder.AlterColumn<Guid>(
                name: "QuoteUpdatedByUserId",
                table: "repair_orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "QuoteUpdatedAtUtc",
                table: "repair_orders",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "QuoteAmount",
                table: "repair_orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "repair_orders",
                type: "character varying(800)",
                maxLength: 800,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BatteryPercent",
                table: "order_reception_checklists",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "order_part_usage",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "order_attachments",
                type: "character varying(800)",
                maxLength: 800,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(800)",
                oldMaxLength: 800,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "notification_outbox",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<Guid>(
                name: "RelatedEntityId",
                table: "notification_outbox",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Recipient",
                table: "notification_outbox",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(180)",
                oldMaxLength: 180);

            migrationBuilder.AlterColumn<DateTime>(
                name: "NextAttemptAtUtc",
                table: "notification_outbox",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "inventory_items",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "RepairOrderId",
                table: "inventory_adjustments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorUserId",
                table: "audit_events",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
