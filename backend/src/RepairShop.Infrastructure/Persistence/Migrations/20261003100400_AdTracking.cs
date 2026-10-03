using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ad_conversions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EventName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EventId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_conversions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "signup_attributions",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Medium = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Campaign = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Term = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Content = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Gclid = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Gbraid = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Wbraid = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Fbclid = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Fbp = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Fbc = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    LandingPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Referrer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AdConsent = table.Column<bool>(type: "boolean", nullable: false),
                    TrialEventId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ClientIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signup_attributions", x => x.OrganizationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ad_conversions_Platform_EventId",
                table: "ad_conversions",
                columns: new[] { "Platform", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_ad_conversions_Status_NextAttemptAtUtc",
                table: "ad_conversions",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_signup_attributions_CreatedAtUtc",
                table: "signup_attributions",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ad_conversions");

            migrationBuilder.DropTable(
                name: "signup_attributions");
        }
    }
}
