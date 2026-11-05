using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardExchange.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StoreSlugAndAttribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Stores",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StoreAttributions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreAttributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoreAttributions_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreAttributions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Stores_Slug",
                table: "Stores",
                column: "Slug",
                unique: true,
                filter: "[Slug] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StoreAttributions_StoreId",
                table: "StoreAttributions",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreAttributions_UserId",
                table: "StoreAttributions",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoreAttributions");

            migrationBuilder.DropIndex(
                name: "IX_Stores_Slug",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Stores");
        }
    }
}
