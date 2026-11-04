using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardExchange.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MatchmakingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cards_CardInfoId",
                table: "Cards");

            migrationBuilder.CreateIndex(
                name: "IX_UserLocations_Latitude_Longitude",
                table: "UserLocations",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_CardInfoId_IsAvailableForTrade",
                table: "Cards",
                columns: new[] { "CardInfoId", "IsAvailableForTrade" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserLocations_Latitude_Longitude",
                table: "UserLocations");

            migrationBuilder.DropIndex(
                name: "IX_Cards_CardInfoId_IsAvailableForTrade",
                table: "Cards");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_CardInfoId",
                table: "Cards",
                column: "CardInfoId");
        }
    }
}
