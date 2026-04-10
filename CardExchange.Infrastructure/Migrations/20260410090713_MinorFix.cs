using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardExchange.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MinorFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PriceSpikeThreshold",
                table: "Users",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "CardSets",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AddColumn<string>(
                name: "PokemonLogoUrl",
                table: "CardSets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PokemonSymbolUrl",
                table: "CardSets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PokemonTcgId",
                table: "CardSets",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PokemonTcgUpdatedAt",
                table: "CardSets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrintedTotal",
                table: "CardSets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Series",
                table: "CardSets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvolvesFrom",
                table: "CardInfos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Hp",
                table: "CardInfos",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PokemonImageLarge",
                table: "CardInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PokemonImageSmall",
                table: "CardInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PokemonTcgData",
                table: "CardInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PokemonTcgId",
                table: "CardInfos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PokemonTcgUpdatedAt",
                table: "CardInfos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PokemonTypes",
                table: "CardInfos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceCardmarketAvg",
                table: "CardInfos",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceCardmarketTrend",
                table: "CardInfos",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceTcgHolofoil",
                table: "CardInfos",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceTcgNormal",
                table: "CardInfos",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceTcgReverseHolofoil",
                table: "CardInfos",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subtypes",
                table: "CardInfos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Supertype",
                table: "CardInfos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppConfigKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    KeyName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    KeyValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEncrypted = table.Column<bool>(type: "bit", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppConfigKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ValueType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardSets_PokemonTcgId",
                table: "CardSets",
                column: "PokemonTcgId",
                unique: true,
                filter: "[PokemonTcgId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CardInfos_PokemonTcgId",
                table: "CardInfos",
                column: "PokemonTcgId",
                unique: true,
                filter: "[PokemonTcgId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppConfigKeys_ServiceName_KeyName",
                table: "AppConfigKeys",
                columns: new[] { "ServiceName", "KeyName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppConfigs_Category",
                table: "AppConfigs",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_AppConfigs_Key",
                table: "AppConfigs",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppConfigKeys");

            migrationBuilder.DropTable(
                name: "AppConfigs");

            migrationBuilder.DropIndex(
                name: "IX_CardSets_PokemonTcgId",
                table: "CardSets");

            migrationBuilder.DropIndex(
                name: "IX_CardInfos_PokemonTcgId",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "PriceSpikeThreshold",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PokemonLogoUrl",
                table: "CardSets");

            migrationBuilder.DropColumn(
                name: "PokemonSymbolUrl",
                table: "CardSets");

            migrationBuilder.DropColumn(
                name: "PokemonTcgId",
                table: "CardSets");

            migrationBuilder.DropColumn(
                name: "PokemonTcgUpdatedAt",
                table: "CardSets");

            migrationBuilder.DropColumn(
                name: "PrintedTotal",
                table: "CardSets");

            migrationBuilder.DropColumn(
                name: "Series",
                table: "CardSets");

            migrationBuilder.DropColumn(
                name: "EvolvesFrom",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "Hp",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "PokemonImageLarge",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "PokemonImageSmall",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "PokemonTcgData",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "PokemonTcgId",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "PokemonTcgUpdatedAt",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "PokemonTypes",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "PriceCardmarketAvg",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "PriceCardmarketTrend",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "PriceTcgHolofoil",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "PriceTcgNormal",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "PriceTcgReverseHolofoil",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "Subtypes",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "Supertype",
                table: "CardInfos");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "CardSets",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);
        }
    }
}
