using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WithdrawalRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "withdrawal_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Email = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_withdrawal_requests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_withdrawal_requests_Code",
                table: "withdrawal_requests",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_withdrawal_requests_CreatedAtUtc",
                table: "withdrawal_requests",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "withdrawal_requests");
        }
    }
}
