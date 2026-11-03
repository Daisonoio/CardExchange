using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardExchange.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CardLanguageAndFinish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PreferredFinish",
                table: "WishlistItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "WishlistItems",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Finish",
                table: "Cards",
                type: "int",
                nullable: false,
                defaultValue: 1); // CardFinish.Normal per le carte esistenti

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Cards",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreferredFinish",
                table: "WishlistItems");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                table: "WishlistItems");

            migrationBuilder.DropColumn(
                name: "Finish",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Cards");
        }
    }
}
