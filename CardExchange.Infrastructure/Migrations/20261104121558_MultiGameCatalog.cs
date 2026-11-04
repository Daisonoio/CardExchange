using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardExchange.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MultiGameCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CatalogKey",
                table: "Games",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CatalogSource",
                table: "CardInfos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CatalogUpdatedAt",
                table: "CardInfos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "CardInfos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GameData",
                table: "CardInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardInfos_CatalogSource_ExternalId",
                table: "CardInfos",
                columns: new[] { "CatalogSource", "ExternalId" },
                unique: true,
                filter: "[CatalogSource] IS NOT NULL AND [ExternalId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CardInfos_CatalogSource_ExternalId",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "CatalogKey",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "CatalogSource",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "CatalogUpdatedAt",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "GameData",
                table: "CardInfos");
        }
    }
}
