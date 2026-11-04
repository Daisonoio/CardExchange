using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardExchange.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BillingMethods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActivatedById",
                table: "UserSubscriptions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminNote",
                table: "UserSubscriptions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryReminderSentAt",
                table: "UserSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalCustomerId",
                table: "UserSubscriptions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSubscriptionId",
                table: "UserSubscriptions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "UserSubscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ProcessedWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Provider = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedWebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_ExternalSubscriptionId",
                table: "UserSubscriptions",
                column: "ExternalSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedWebhookEvents_Provider_EventId",
                table: "ProcessedWebhookEvents",
                columns: new[] { "Provider", "EventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedWebhookEvents");

            migrationBuilder.DropIndex(
                name: "IX_UserSubscriptions_ExternalSubscriptionId",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "ActivatedById",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "AdminNote",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "ExpiryReminderSentAt",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "ExternalCustomerId",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "ExternalSubscriptionId",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "UserSubscriptions");
        }
    }
}
