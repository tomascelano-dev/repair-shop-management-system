using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Subscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailVerificationTokenHash",
                table: "users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerifiedAtUtc",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Plan = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    BillingCountry = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ProviderSubscriptionId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ProviderCustomerId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PendingPlan = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    TrialEndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrentPeriodEndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CanceledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastEventAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_EmailVerificationTokenHash",
                table: "users",
                column: "EmailVerificationTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_OrganizationId",
                table: "subscriptions",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_Provider_ProviderSubscriptionId",
                table: "subscriptions",
                columns: new[] { "Provider", "ProviderSubscriptionId" });

            // Users that already exist proved their email (admin-created and invited users), except
            // invitations not accepted yet: accepting one verifies the email.
            migrationBuilder.Sql("""
                UPDATE users SET "EmailVerifiedAtUtc" = "CreatedAtUtc"
                WHERE NOT ("PendingTokenPurpose" IS NOT NULL AND "PendingTokenPurpose" = 0 AND "PendingTokenHash" IS NOT NULL);
                """);

            // Organizations that predate subscriptions keep working: complimentary Pro plan (Plan=3, Active=2, Manual=1).
            migrationBuilder.Sql("""
                INSERT INTO subscriptions ("Id", "OrganizationId", "Plan", "Status", "Provider", "BillingCountry",
                                           "TrialEndsAtUtc", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT gen_random_uuid(), o."OrganizationId", 3, 2, 1, 'AR', now(), now(), now()
                FROM (SELECT DISTINCT "OrganizationId" FROM shops) o
                WHERE NOT EXISTS (SELECT 1 FROM subscriptions s WHERE s."OrganizationId" = o."OrganizationId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_users_EmailVerificationTokenHash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EmailVerificationTokenHash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EmailVerifiedAtUtc",
                table: "users");
        }
    }
}
