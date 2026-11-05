using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardExchange.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MeetupAtStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StoreId",
                table: "TradeMeetups",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StoreNotifiedAt",
                table: "TradeMeetups",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradeMeetups_StoreId_ScheduledAt",
                table: "TradeMeetups",
                columns: new[] { "StoreId", "ScheduledAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_TradeMeetups_Stores_StoreId",
                table: "TradeMeetups",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TradeMeetups_Stores_StoreId",
                table: "TradeMeetups");

            migrationBuilder.DropIndex(
                name: "IX_TradeMeetups_StoreId_ScheduledAt",
                table: "TradeMeetups");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "TradeMeetups");

            migrationBuilder.DropColumn(
                name: "StoreNotifiedAt",
                table: "TradeMeetups");
        }
    }
}
