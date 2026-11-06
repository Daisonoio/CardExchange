using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardExchange.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StoreBuyList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoreBuyAlerts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreBuyAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoreBuyAlerts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoreBuyOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    CardInfoId = table.Column<int>(type: "int", nullable: false),
                    MinCondition = table.Column<int>(type: "int", nullable: false),
                    Finish = table.Column<int>(type: "int", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    PriceCashEur = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    PriceCreditEur = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    QuantityWanted = table.Column<int>(type: "int", nullable: false),
                    QuantityFilled = table.Column<int>(type: "int", nullable: false),
                    QuantityPending = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreBuyOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoreBuyOrders_CardInfos_CardInfoId",
                        column: x => x.CardInfoId,
                        principalTable: "CardInfos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreBuyOrders_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoreBuyOffers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuyOrderId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CardId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DropOffBy = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StoreNote = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReminderSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreBuyOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoreBuyOffers_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreBuyOffers_StoreBuyOrders_BuyOrderId",
                        column: x => x.BuyOrderId,
                        principalTable: "StoreBuyOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StoreBuyOffers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoreBuyAlerts_StoreId_UserId_SentAt",
                table: "StoreBuyAlerts",
                columns: new[] { "StoreId", "UserId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StoreBuyAlerts_UserId",
                table: "StoreBuyAlerts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreBuyOffers_BuyOrderId_Status",
                table: "StoreBuyOffers",
                columns: new[] { "BuyOrderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StoreBuyOffers_CardId",
                table: "StoreBuyOffers",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreBuyOffers_UserId_Status",
                table: "StoreBuyOffers",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StoreBuyOrders_CardInfoId_IsActive",
                table: "StoreBuyOrders",
                columns: new[] { "CardInfoId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_StoreBuyOrders_StoreId_IsActive",
                table: "StoreBuyOrders",
                columns: new[] { "StoreId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoreBuyAlerts");

            migrationBuilder.DropTable(
                name: "StoreBuyOffers");

            migrationBuilder.DropTable(
                name: "StoreBuyOrders");
        }
    }
}
