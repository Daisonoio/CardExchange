using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardExchange.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OnePieceTcgSetName",
                table: "CardSets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OnePieceTcgUpdatedAt",
                table: "CardSets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YuGiOhNumCards",
                table: "CardSets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YuGiOhSetCode",
                table: "CardSets",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YuGiOhTcgDate",
                table: "CardSets",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "YuGiOhUpdatedAt",
                table: "CardSets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnePieceAbility",
                table: "CardInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnePieceColor",
                table: "CardInfos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OnePieceCost",
                table: "CardInfos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OnePieceCounter",
                table: "CardInfos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnePieceFamily",
                table: "CardInfos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnePieceImageLarge",
                table: "CardInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnePieceImageSmall",
                table: "CardInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OnePiecePower",
                table: "CardInfos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnePieceTcgId",
                table: "CardInfos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OnePieceTcgUpdatedAt",
                table: "CardInfos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnePieceTrigger",
                table: "CardInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceYuGiOhAmazon",
                table: "CardInfos",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceYuGiOhCardmarket",
                table: "CardInfos",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceYuGiOhCoolstuffinc",
                table: "CardInfos",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceYuGiOhEbay",
                table: "CardInfos",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceYuGiOhTcgPlayer",
                table: "CardInfos",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YuGiOhArchetype",
                table: "CardInfos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YuGiOhAtk",
                table: "CardInfos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YuGiOhAttribute",
                table: "CardInfos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YuGiOhData",
                table: "CardInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YuGiOhDef",
                table: "CardInfos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YuGiOhFrameType",
                table: "CardInfos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YuGiOhId",
                table: "CardInfos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YuGiOhImageSmall",
                table: "CardInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YuGiOhImageUrl",
                table: "CardInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YuGiOhLevel",
                table: "CardInfos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YuGiOhRace",
                table: "CardInfos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YuGiOhType",
                table: "CardInfos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "YuGiOhUpdatedAt",
                table: "CardInfos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardSets_YuGiOhSetCode",
                table: "CardSets",
                column: "YuGiOhSetCode",
                unique: true,
                filter: "[YuGiOhSetCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CardInfos_OnePieceTcgId",
                table: "CardInfos",
                column: "OnePieceTcgId",
                unique: true,
                filter: "[OnePieceTcgId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CardInfos_YuGiOhId",
                table: "CardInfos",
                column: "YuGiOhId",
                unique: true,
                filter: "[YuGiOhId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CardSets_YuGiOhSetCode",
                table: "CardSets");

            migrationBuilder.DropIndex(
                name: "IX_CardInfos_OnePieceTcgId",
                table: "CardInfos");

            migrationBuilder.DropIndex(
                name: "IX_CardInfos_YuGiOhId",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "OnePieceTcgSetName",
                table: "CardSets");

            migrationBuilder.DropColumn(
                name: "OnePieceTcgUpdatedAt",
                table: "CardSets");

            migrationBuilder.DropColumn(
                name: "YuGiOhNumCards",
                table: "CardSets");

            migrationBuilder.DropColumn(
                name: "YuGiOhSetCode",
                table: "CardSets");

            migrationBuilder.DropColumn(
                name: "YuGiOhTcgDate",
                table: "CardSets");

            migrationBuilder.DropColumn(
                name: "YuGiOhUpdatedAt",
                table: "CardSets");

            migrationBuilder.DropColumn(
                name: "OnePieceAbility",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "OnePieceColor",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "OnePieceCost",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "OnePieceCounter",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "OnePieceFamily",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "OnePieceImageLarge",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "OnePieceImageSmall",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "OnePiecePower",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "OnePieceTcgId",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "OnePieceTcgUpdatedAt",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "OnePieceTrigger",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "PriceYuGiOhAmazon",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "PriceYuGiOhCardmarket",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "PriceYuGiOhCoolstuffinc",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "PriceYuGiOhEbay",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "PriceYuGiOhTcgPlayer",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "YuGiOhArchetype",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "YuGiOhAtk",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "YuGiOhAttribute",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "YuGiOhData",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "YuGiOhDef",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "YuGiOhFrameType",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "YuGiOhId",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "YuGiOhImageSmall",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "YuGiOhImageUrl",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "YuGiOhLevel",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "YuGiOhRace",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "YuGiOhType",
                table: "CardInfos");

            migrationBuilder.DropColumn(
                name: "YuGiOhUpdatedAt",
                table: "CardInfos");
        }
    }
}
