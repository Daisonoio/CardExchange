using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardExchange.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SellerShowcase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SellerShowcases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Bio = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CoverImageUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FeaturedCardIds = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellerShowcases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SellerShowcases_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShowcaseVisits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShowcaseId = table.Column<int>(type: "int", nullable: false),
                    ViewerId = table.Column<int>(type: "int", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShowcaseVisits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SellerShowcases_Slug",
                table: "SellerShowcases",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SellerShowcases_UserId",
                table: "SellerShowcases",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShowcaseVisits_ShowcaseId_Day",
                table: "ShowcaseVisits",
                columns: new[] { "ShowcaseId", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_ShowcaseVisits_ShowcaseId_ViewerId_Day",
                table: "ShowcaseVisits",
                columns: new[] { "ShowcaseId", "ViewerId", "Day" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SellerShowcases");

            migrationBuilder.DropTable(
                name: "ShowcaseVisits");
        }
    }
}
