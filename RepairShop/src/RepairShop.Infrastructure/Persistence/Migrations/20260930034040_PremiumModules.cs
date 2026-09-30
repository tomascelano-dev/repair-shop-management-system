using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PremiumModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_workflows_ShopId_Id",
                table: "workflows",
                columns: new[] { "ShopId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_inventory_items_ShopId_Id",
                table: "inventory_items",
                columns: new[] { "ShopId", "Id" });

            migrationBuilder.CreateTable(
                name: "premium_company_contract",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Contact = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Phone = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    MonthlyFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExtraOrderRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IncludedOrders = table.Column<int>(type: "integer", nullable: false),
                    SlaHours = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Terms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_premium_company_contract", x => x.Id);
                    table.UniqueConstraint("AK_premium_company_contract_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "premium_order_expense",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Minutes = table.Column<int>(type: "integer", nullable: false),
                    HourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_premium_order_expense", x => x.Id);
                    table.UniqueConstraint("AK_premium_order_expense_ShopId_Id", x => new { x.ShopId, x.Id });
                    table.ForeignKey(
                        name: "FK_premium_order_expense_workflows_ShopId_OrderId",
                        columns: x => new { x.ShopId, x.OrderId },
                        principalTable: "workflows",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "premium_premium_branch",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Address = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_premium_premium_branch", x => x.Id);
                    table.UniqueConstraint("AK_premium_premium_branch_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "premium_supplier_price",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Supplier = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Sku = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Compatibility = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Quality = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Source = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_premium_supplier_price", x => x.Id);
                    table.UniqueConstraint("AK_premium_supplier_price_ShopId_Id", x => new { x.ShopId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "premium_company_equipment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Identifier = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_premium_company_equipment", x => x.Id);
                    table.UniqueConstraint("AK_premium_company_equipment_ShopId_Id", x => new { x.ShopId, x.Id });
                    table.ForeignKey(
                        name: "FK_premium_company_equipment_premium_company_contract_ShopId_C~",
                        columns: x => new { x.ShopId, x.ContractId },
                        principalTable: "premium_company_contract",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "premium_contract_settlement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    Period = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CompletedOrders = table.Column<int>(type: "integer", nullable: false),
                    ExtraOrders = table.Column<int>(type: "integer", nullable: false),
                    MonthlyFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExtraRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OrderIdsJson = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    Status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_premium_contract_settlement", x => x.Id);
                    table.UniqueConstraint("AK_premium_contract_settlement_ShopId_Id", x => new { x.ShopId, x.Id });
                    table.ForeignKey(
                        name: "FK_premium_contract_settlement_premium_company_contract_ShopId~",
                        columns: x => new { x.ShopId, x.ContractId },
                        principalTable: "premium_company_contract",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "premium_refurb_device",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Model = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Identifier = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Seller = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Acquisition = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Grade = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Diagnosis = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    QualityChecksJson = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TargetPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SalePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Buyer = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SoldAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_premium_refurb_device", x => x.Id);
                    table.UniqueConstraint("AK_premium_refurb_device_ShopId_Id", x => new { x.ShopId, x.Id });
                    table.ForeignKey(
                        name: "FK_premium_refurb_device_premium_premium_branch_ShopId_BranchId",
                        columns: x => new { x.ShopId, x.BranchId },
                        principalTable: "premium_premium_branch",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "premium_stock_lot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Supplier = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LotCode = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Serial = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_premium_stock_lot", x => x.Id);
                    table.UniqueConstraint("AK_premium_stock_lot_ShopId_Id", x => new { x.ShopId, x.Id });
                    table.ForeignKey(
                        name: "FK_premium_stock_lot_inventory_items_ShopId_ItemId",
                        columns: x => new { x.ShopId, x.ItemId },
                        principalTable: "inventory_items",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_premium_stock_lot_premium_premium_branch_ShopId_BranchId",
                        columns: x => new { x.ShopId, x.BranchId },
                        principalTable: "premium_premium_branch",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "premium_stock_minimum",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Minimum = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_premium_stock_minimum", x => x.Id);
                    table.UniqueConstraint("AK_premium_stock_minimum_ShopId_Id", x => new { x.ShopId, x.Id });
                    table.ForeignKey(
                        name: "FK_premium_stock_minimum_inventory_items_ShopId_ItemId",
                        columns: x => new { x.ShopId, x.ItemId },
                        principalTable: "inventory_items",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_premium_stock_minimum_premium_premium_branch_ShopId_BranchId",
                        columns: x => new { x.ShopId, x.BranchId },
                        principalTable: "premium_premium_branch",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "premium_order_business",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    EquipmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchReference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_premium_order_business", x => x.Id);
                    table.UniqueConstraint("AK_premium_order_business_ShopId_Id", x => new { x.ShopId, x.Id });
                    table.ForeignKey(
                        name: "FK_premium_order_business_premium_company_contract_ShopId_Cont~",
                        columns: x => new { x.ShopId, x.ContractId },
                        principalTable: "premium_company_contract",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_premium_order_business_premium_company_equipment_ShopId_Equ~",
                        columns: x => new { x.ShopId, x.EquipmentId },
                        principalTable: "premium_company_equipment",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_premium_order_business_premium_premium_branch_ShopId_Branch~",
                        columns: x => new { x.ShopId, x.BranchId },
                        principalTable: "premium_premium_branch",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_premium_order_business_workflows_ShopId_OrderId",
                        columns: x => new { x.ShopId, x.OrderId },
                        principalTable: "workflows",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "premium_refurb_event",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_premium_refurb_event", x => x.Id);
                    table.UniqueConstraint("AK_premium_refurb_event_ShopId_Id", x => new { x.ShopId, x.Id });
                    table.ForeignKey(
                        name: "FK_premium_refurb_event_premium_refurb_device_ShopId_DeviceId",
                        columns: x => new { x.ShopId, x.DeviceId },
                        principalTable: "premium_refurb_device",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "premium_refurb_expense",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_premium_refurb_expense", x => x.Id);
                    table.UniqueConstraint("AK_premium_refurb_expense_ShopId_Id", x => new { x.ShopId, x.Id });
                    table.ForeignKey(
                        name: "FK_premium_refurb_expense_premium_refurb_device_ShopId_DeviceId",
                        columns: x => new { x.ShopId, x.DeviceId },
                        principalTable: "premium_refurb_device",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "premium_stock_movement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_premium_stock_movement", x => x.Id);
                    table.UniqueConstraint("AK_premium_stock_movement_ShopId_Id", x => new { x.ShopId, x.Id });
                    table.ForeignKey(
                        name: "FK_premium_stock_movement_premium_stock_lot_ShopId_LotId",
                        columns: x => new { x.ShopId, x.LotId },
                        principalTable: "premium_stock_lot",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "premium_stock_reservation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_premium_stock_reservation", x => x.Id);
                    table.UniqueConstraint("AK_premium_stock_reservation_ShopId_Id", x => new { x.ShopId, x.Id });
                    table.ForeignKey(
                        name: "FK_premium_stock_reservation_premium_stock_lot_ShopId_LotId",
                        columns: x => new { x.ShopId, x.LotId },
                        principalTable: "premium_stock_lot",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_premium_stock_reservation_workflows_ShopId_OrderId",
                        columns: x => new { x.ShopId, x.OrderId },
                        principalTable: "workflows",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "premium_warranty_case",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Problem = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Supplier = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SupplierClaim = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Recovered = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Resolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CoveredAtIntake = table.Column<bool>(type: "boolean", nullable: false),
                    WarrantyEndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_premium_warranty_case", x => x.Id);
                    table.UniqueConstraint("AK_premium_warranty_case_ShopId_Id", x => new { x.ShopId, x.Id });
                    table.ForeignKey(
                        name: "FK_premium_warranty_case_premium_stock_reservation_ShopId_Rese~",
                        columns: x => new { x.ShopId, x.ReservationId },
                        principalTable: "premium_stock_reservation",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_premium_warranty_case_workflows_ShopId_OrderId",
                        columns: x => new { x.ShopId, x.OrderId },
                        principalTable: "workflows",
                        principalColumns: new[] { "ShopId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_premium_company_contract_ShopId",
                table: "premium_company_contract",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_premium_company_equipment_ShopId",
                table: "premium_company_equipment",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_premium_company_equipment_ShopId_ContractId_Identifier",
                table: "premium_company_equipment",
                columns: new[] { "ShopId", "ContractId", "Identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_premium_contract_settlement_ShopId",
                table: "premium_contract_settlement",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_premium_contract_settlement_ShopId_ContractId_Period",
                table: "premium_contract_settlement",
                columns: new[] { "ShopId", "ContractId", "Period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_premium_order_business_ShopId",
                table: "premium_order_business",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_premium_order_business_ShopId_BranchId",
                table: "premium_order_business",
                columns: new[] { "ShopId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_premium_order_business_ShopId_ContractId",
                table: "premium_order_business",
                columns: new[] { "ShopId", "ContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_premium_order_business_ShopId_EquipmentId",
                table: "premium_order_business",
                columns: new[] { "ShopId", "EquipmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_premium_order_business_ShopId_OrderId",
                table: "premium_order_business",
                columns: new[] { "ShopId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_premium_order_expense_ShopId",
                table: "premium_order_expense",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_premium_order_expense_ShopId_OrderId",
                table: "premium_order_expense",
                columns: new[] { "ShopId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_premium_premium_branch_ShopId",
                table: "premium_premium_branch",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_premium_premium_branch_ShopId_Name",
                table: "premium_premium_branch",
                columns: new[] { "ShopId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_premium_refurb_device_ShopId",
                table: "premium_refurb_device",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_premium_refurb_device_ShopId_BranchId",
                table: "premium_refurb_device",
                columns: new[] { "ShopId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_premium_refurb_device_ShopId_Identifier",
                table: "premium_refurb_device",
                columns: new[] { "ShopId", "Identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_premium_refurb_event_ShopId",
                table: "premium_refurb_event",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_premium_refurb_event_ShopId_DeviceId",
                table: "premium_refurb_event",
                columns: new[] { "ShopId", "DeviceId" });

            migrationBuilder.CreateIndex(
                name: "IX_premium_refurb_expense_ShopId",
                table: "premium_refurb_expense",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_premium_refurb_expense_ShopId_DeviceId",
                table: "premium_refurb_expense",
                columns: new[] { "ShopId", "DeviceId" });

            migrationBuilder.CreateIndex(
                name: "IX_premium_stock_lot_ShopId",
                table: "premium_stock_lot",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_premium_stock_lot_ShopId_BranchId",
                table: "premium_stock_lot",
                columns: new[] { "ShopId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_premium_stock_lot_ShopId_ItemId",
                table: "premium_stock_lot",
                columns: new[] { "ShopId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_premium_stock_lot_ShopId_Serial",
                table: "premium_stock_lot",
                columns: new[] { "ShopId", "Serial" },
                unique: true,
                filter: "\"Serial\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_premium_stock_minimum_ShopId",
                table: "premium_stock_minimum",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_premium_stock_minimum_ShopId_BranchId",
                table: "premium_stock_minimum",
                columns: new[] { "ShopId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_premium_stock_minimum_ShopId_ItemId_BranchId",
                table: "premium_stock_minimum",
                columns: new[] { "ShopId", "ItemId", "BranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_premium_stock_movement_ShopId",
                table: "premium_stock_movement",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_premium_stock_movement_ShopId_LotId",
                table: "premium_stock_movement",
                columns: new[] { "ShopId", "LotId" });

            migrationBuilder.CreateIndex(
                name: "IX_premium_stock_reservation_ShopId",
                table: "premium_stock_reservation",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_premium_stock_reservation_ShopId_LotId",
                table: "premium_stock_reservation",
                columns: new[] { "ShopId", "LotId" });

            migrationBuilder.CreateIndex(
                name: "IX_premium_stock_reservation_ShopId_OrderId",
                table: "premium_stock_reservation",
                columns: new[] { "ShopId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_premium_supplier_price_ShopId",
                table: "premium_supplier_price",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_premium_supplier_price_ShopId_Supplier_Sku",
                table: "premium_supplier_price",
                columns: new[] { "ShopId", "Supplier", "Sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_premium_warranty_case_ShopId",
                table: "premium_warranty_case",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_premium_warranty_case_ShopId_OrderId",
                table: "premium_warranty_case",
                columns: new[] { "ShopId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_premium_warranty_case_ShopId_ReservationId",
                table: "premium_warranty_case",
                columns: new[] { "ShopId", "ReservationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "premium_contract_settlement");

            migrationBuilder.DropTable(
                name: "premium_order_business");

            migrationBuilder.DropTable(
                name: "premium_order_expense");

            migrationBuilder.DropTable(
                name: "premium_refurb_event");

            migrationBuilder.DropTable(
                name: "premium_refurb_expense");

            migrationBuilder.DropTable(
                name: "premium_stock_minimum");

            migrationBuilder.DropTable(
                name: "premium_stock_movement");

            migrationBuilder.DropTable(
                name: "premium_supplier_price");

            migrationBuilder.DropTable(
                name: "premium_warranty_case");

            migrationBuilder.DropTable(
                name: "premium_company_equipment");

            migrationBuilder.DropTable(
                name: "premium_refurb_device");

            migrationBuilder.DropTable(
                name: "premium_stock_reservation");

            migrationBuilder.DropTable(
                name: "premium_company_contract");

            migrationBuilder.DropTable(
                name: "premium_stock_lot");

            migrationBuilder.DropTable(
                name: "premium_premium_branch");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_workflows_ShopId_Id",
                table: "workflows");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_inventory_items_ShopId_Id",
                table: "inventory_items");
        }
    }
}
