using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExchangeRatesPerOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_exchange_rates_Date_BaseCurrency_QuoteCurrency_Source",
                table: "exchange_rates");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "exchange_rates",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_exchange_rates_OrganizationId_Date_BaseCurrency_QuoteCurren~",
                table: "exchange_rates",
                columns: new[] { "OrganizationId", "Date", "BaseCurrency", "QuoteCurrency", "Source" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_exchange_rates_OrganizationId_Date_BaseCurrency_QuoteCurren~",
                table: "exchange_rates");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "exchange_rates");

            migrationBuilder.CreateIndex(
                name: "IX_exchange_rates_Date_BaseCurrency_QuoteCurrency_Source",
                table: "exchange_rates",
                columns: new[] { "Date", "BaseCurrency", "QuoteCurrency", "Source" },
                unique: true);
        }
    }
}
