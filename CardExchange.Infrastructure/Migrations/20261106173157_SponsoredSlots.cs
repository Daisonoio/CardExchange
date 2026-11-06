using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardExchange.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SponsoredSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SlotBody",
                table: "Promotions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlotTitle",
                table: "Promotions",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SlotImpressions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SlotId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlotImpressions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SponsoredSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PromotionId = table.Column<int>(type: "int", nullable: true),
                    SponsorshipId = table.Column<int>(type: "int", nullable: true),
                    OwnerUserId = table.Column<int>(type: "int", nullable: true),
                    Placement = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TargetUrl = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FrequencyCapPerDay = table.Column<int>(type: "int", nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    RadiusKm = table.Column<int>(type: "int", nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GameId = table.Column<int>(type: "int", nullable: true),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Impressions = table.Column<int>(type: "int", nullable: false),
                    Clicks = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsoredSlots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sponsors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WebsiteClicks = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sponsors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sponsorships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SponsorId = table.Column<int>(type: "int", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    ScopeRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Placement = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SlotPlacement = table.Column<int>(type: "int", nullable: true),
                    SlotTitle = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    SlotBody = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GameId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sponsorships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sponsorships_Sponsors_SponsorId",
                        column: x => x.SponsorId,
                        principalTable: "Sponsors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SlotImpressions_SlotId_UserId_Day",
                table: "SlotImpressions",
                columns: new[] { "SlotId", "UserId", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlotImpressions_UserId_Day",
                table: "SlotImpressions",
                columns: new[] { "UserId", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_SponsoredSlots_Placement_IsActive_EndsAt",
                table: "SponsoredSlots",
                columns: new[] { "Placement", "IsActive", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sponsorships_Placement_EndsAt",
                table: "Sponsorships",
                columns: new[] { "Placement", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sponsorships_SponsorId",
                table: "Sponsorships",
                column: "SponsorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SlotImpressions");

            migrationBuilder.DropTable(
                name: "SponsoredSlots");

            migrationBuilder.DropTable(
                name: "Sponsorships");

            migrationBuilder.DropTable(
                name: "Sponsors");

            migrationBuilder.DropColumn(
                name: "SlotBody",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "SlotTitle",
                table: "Promotions");
        }
    }
}
