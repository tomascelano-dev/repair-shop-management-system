using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairShop.Infrastructure.Persistence.Migrations;

[DbContext(typeof(RepairShopDbContext))]
[Migration("20260129000000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "shops",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                AddressLine = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                City = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                Country = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_shops", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Role = table.Column<int>(type: "integer", nullable: false),
                PasswordHash = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_users", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "customers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                FullName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_customers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "devices",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                Brand = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                Model = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                SerialNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_devices", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "repair_orders",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                IssueDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Notes = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false),
                QuoteAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                QuoteCurrency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                QuoteUpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                QuoteUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_repair_orders", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "order_status_history",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                RepairOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                FromStatus = table.Column<int>(type: "integer", nullable: false),
                ToStatus = table.Column<int>(type: "integer", nullable: false),
                ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                ChangedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_status_history", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "order_notes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                RepairOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                Body = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_notes", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "order_attachments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                RepairOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                Url = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_attachments", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "order_payments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                RepairOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                Method = table.Column<int>(type: "integer", nullable: false),
                Reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_payments", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "order_reception_checklists",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                RepairOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                ScreenOk = table.Column<bool>(type: "boolean", nullable: false),
                CamerasOk = table.Column<bool>(type: "boolean", nullable: false),
                SpeakersOk = table.Column<bool>(type: "boolean", nullable: false),
                MicrophoneOk = table.Column<bool>(type: "boolean", nullable: false),
                ButtonsOk = table.Column<bool>(type: "boolean", nullable: false),
                FaceIdOk = table.Column<bool>(type: "boolean", nullable: false),
                FingerprintOk = table.Column<bool>(type: "boolean", nullable: false),
                CloudLock = table.Column<int>(type: "integer", nullable: false),
                BatteryPercent = table.Column<int>(type: "integer", nullable: true),
                CosmeticNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_reception_checklists", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "message_templates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_message_templates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "audit_events",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                EntityType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                Action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                ActorEmail = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                DataJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audit_events", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "notification_outbox",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                Channel = table.Column<int>(type: "integer", nullable: false),
                Recipient = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                AttemptCount = table.Column<int>(type: "integer", nullable: false),
                NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                CorrelationKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                RelatedEntityType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_notification_outbox", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "inventory_items",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                Sku = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                QuantityOnHand = table.Column<int>(type: "integer", nullable: false),
                UnitCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                UnitCostCurrency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_inventory_items", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "inventory_adjustments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<int>(type: "integer", nullable: false),
                DeltaQuantity = table.Column<int>(type: "integer", nullable: false),
                Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                RepairOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_inventory_adjustments", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "order_part_usage",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                RepairOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                QuantityUsed = table.Column<int>(type: "integer", nullable: false),
                UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                UnitPriceCurrency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_part_usage", x => x.Id);
            });

        // Indexes
        migrationBuilder.CreateIndex(name: "IX_shops_Name", table: "shops", column: "Name");

        migrationBuilder.CreateIndex(name: "IX_users_ShopId", table: "users", column: "ShopId");
        migrationBuilder.CreateIndex(name: "IX_users_Email", table: "users", column: "Email", unique: true);

        migrationBuilder.CreateIndex(name: "IX_customers_ShopId", table: "customers", column: "ShopId");
        migrationBuilder.CreateIndex(name: "IX_customers_ShopId_Phone", table: "customers", columns: new[] { "ShopId", "Phone" });

        migrationBuilder.CreateIndex(name: "IX_devices_ShopId", table: "devices", column: "ShopId");
        migrationBuilder.CreateIndex(name: "IX_devices_ShopId_CustomerId", table: "devices", columns: new[] { "ShopId", "CustomerId" });

        migrationBuilder.CreateIndex(name: "IX_repair_orders_ShopId", table: "repair_orders", column: "ShopId");
        migrationBuilder.CreateIndex(name: "IX_repair_orders_ShopId_CustomerId", table: "repair_orders", columns: new[] { "ShopId", "CustomerId" });
        migrationBuilder.CreateIndex(name: "IX_repair_orders_ShopId_DeviceId", table: "repair_orders", columns: new[] { "ShopId", "DeviceId" });
        migrationBuilder.CreateIndex(name: "IX_repair_orders_ShopId_Status", table: "repair_orders", columns: new[] { "ShopId", "Status" });

        migrationBuilder.CreateIndex(name: "IX_order_status_history_ShopId", table: "order_status_history", column: "ShopId");
        migrationBuilder.CreateIndex(name: "IX_order_status_history_ShopId_RepairOrderId", table: "order_status_history", columns: new[] { "ShopId", "RepairOrderId" });

        migrationBuilder.CreateIndex(name: "IX_order_notes_ShopId", table: "order_notes", column: "ShopId");
        migrationBuilder.CreateIndex(name: "IX_order_notes_ShopId_RepairOrderId", table: "order_notes", columns: new[] { "ShopId", "RepairOrderId" });

        migrationBuilder.CreateIndex(name: "IX_order_attachments_ShopId", table: "order_attachments", column: "ShopId");
        migrationBuilder.CreateIndex(name: "IX_order_attachments_ShopId_RepairOrderId", table: "order_attachments", columns: new[] { "ShopId", "RepairOrderId" });

        migrationBuilder.CreateIndex(name: "IX_order_payments_ShopId", table: "order_payments", column: "ShopId");
        migrationBuilder.CreateIndex(name: "IX_order_payments_ShopId_RepairOrderId", table: "order_payments", columns: new[] { "ShopId", "RepairOrderId" });

        migrationBuilder.CreateIndex(name: "IX_order_reception_checklists_ShopId_RepairOrderId", table: "order_reception_checklists", columns: new[] { "ShopId", "RepairOrderId" }, unique: true);

        migrationBuilder.CreateIndex(name: "IX_message_templates_ShopId", table: "message_templates", column: "ShopId");
        migrationBuilder.CreateIndex(name: "IX_message_templates_ShopId_Key", table: "message_templates", columns: new[] { "ShopId", "Key" }, unique: true);

        migrationBuilder.CreateIndex(name: "IX_audit_events_ShopId", table: "audit_events", column: "ShopId");
        migrationBuilder.CreateIndex(name: "IX_audit_events_ShopId_EntityType_EntityId_CreatedAtUtc", table: "audit_events", columns: new[] { "ShopId", "EntityType", "EntityId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(name: "IX_notification_outbox_ShopId", table: "notification_outbox", column: "ShopId");
        migrationBuilder.CreateIndex(name: "IX_notification_outbox_ShopId_Status_CreatedAtUtc", table: "notification_outbox", columns: new[] { "ShopId", "Status", "CreatedAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_notification_outbox_ShopId_CorrelationKey", table: "notification_outbox", columns: new[] { "ShopId", "CorrelationKey" }, unique: true);

        migrationBuilder.CreateIndex(name: "IX_inventory_items_ShopId", table: "inventory_items", column: "ShopId");
        migrationBuilder.CreateIndex(name: "IX_inventory_items_ShopId_Sku", table: "inventory_items", columns: new[] { "ShopId", "Sku" }, unique: true);

        migrationBuilder.CreateIndex(name: "IX_inventory_adjustments_ShopId", table: "inventory_adjustments", column: "ShopId");
        migrationBuilder.CreateIndex(name: "IX_inventory_adjustments_ShopId_InventoryItemId", table: "inventory_adjustments", columns: new[] { "ShopId", "InventoryItemId" });

        migrationBuilder.CreateIndex(name: "IX_order_part_usage_ShopId", table: "order_part_usage", column: "ShopId");
        migrationBuilder.CreateIndex(name: "IX_order_part_usage_ShopId_RepairOrderId", table: "order_part_usage", columns: new[] { "ShopId", "RepairOrderId" });
        migrationBuilder.CreateIndex(name: "IX_order_part_usage_ShopId_InventoryItemId", table: "order_part_usage", columns: new[] { "ShopId", "InventoryItemId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "order_part_usage");
        migrationBuilder.DropTable(name: "inventory_adjustments");
        migrationBuilder.DropTable(name: "inventory_items");
        migrationBuilder.DropTable(name: "notification_outbox");
        migrationBuilder.DropTable(name: "audit_events");
        migrationBuilder.DropTable(name: "message_templates");
        migrationBuilder.DropTable(name: "order_reception_checklists");
        migrationBuilder.DropTable(name: "order_payments");
        migrationBuilder.DropTable(name: "order_attachments");
        migrationBuilder.DropTable(name: "order_notes");
        migrationBuilder.DropTable(name: "order_status_history");
        migrationBuilder.DropTable(name: "repair_orders");
        migrationBuilder.DropTable(name: "devices");
        migrationBuilder.DropTable(name: "customers");
        migrationBuilder.DropTable(name: "users");
        migrationBuilder.DropTable(name: "shops");
    }
}
