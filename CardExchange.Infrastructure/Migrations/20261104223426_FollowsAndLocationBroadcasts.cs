using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardExchange.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FollowsAndLocationBroadcasts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Follows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FollowerId = table.Column<int>(type: "int", nullable: false),
                    FollowedUserId = table.Column<int>(type: "int", nullable: true),
                    FollowedStoreId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Follows", x => x.Id);
                    table.CheckConstraint("CK_Follow_OneTarget", "([FollowedUserId] IS NULL AND [FollowedStoreId] IS NOT NULL) OR ([FollowedUserId] IS NOT NULL AND [FollowedStoreId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_Follows_Stores_FollowedStoreId",
                        column: x => x.FollowedStoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Follows_Users_FollowedUserId",
                        column: x => x.FollowedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Follows_Users_FollowerId",
                        column: x => x.FollowerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LocationBroadcasts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthorId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: true),
                    EventId = table.Column<int>(type: "int", nullable: true),
                    PlaceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SpotDetail = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(280)", maxLength: 280, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecipientCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationBroadcasts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationBroadcasts_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LocationBroadcasts_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LocationBroadcasts_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Follows_FollowedStoreId",
                table: "Follows",
                column: "FollowedStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Follows_FollowedUserId",
                table: "Follows",
                column: "FollowedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Follows_FollowerId_FollowedStoreId",
                table: "Follows",
                columns: new[] { "FollowerId", "FollowedStoreId" },
                unique: true,
                filter: "[FollowedStoreId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Follows_FollowerId_FollowedUserId",
                table: "Follows",
                columns: new[] { "FollowerId", "FollowedUserId" },
                unique: true,
                filter: "[FollowedUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LocationBroadcasts_AuthorId_CreatedAt",
                table: "LocationBroadcasts",
                columns: new[] { "AuthorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LocationBroadcasts_EndsAt",
                table: "LocationBroadcasts",
                column: "EndsAt");

            migrationBuilder.CreateIndex(
                name: "IX_LocationBroadcasts_EventId",
                table: "LocationBroadcasts",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationBroadcasts_StoreId",
                table: "LocationBroadcasts",
                column: "StoreId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Follows");

            migrationBuilder.DropTable(
                name: "LocationBroadcasts");
        }
    }
}
