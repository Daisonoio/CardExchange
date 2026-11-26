using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardExchange.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HomeStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HomeStoreId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_HomeStoreId",
                table: "Users",
                column: "HomeStoreId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Stores_HomeStoreId",
                table: "Users",
                column: "HomeStoreId",
                principalTable: "Stores",
                principalColumn: "Id");

            // Negozio di casa = negozio da cui l'utente si è iscritto col QR
            migrationBuilder.Sql(@"UPDATE u SET HomeStoreId = a.StoreId
                FROM Users u JOIN StoreAttributions a ON a.UserId = u.Id
                WHERE u.HomeStoreId IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Stores_HomeStoreId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_HomeStoreId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HomeStoreId",
                table: "Users");
        }
    }
}
