using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardExchange.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EventWaitlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EntryFeeEur",
                table: "Events",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Format",
                table: "Events",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GameId",
                table: "Events",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OrganizerSummarySentAt",
                table: "Events",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationDeadline",
                table: "Events",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OfferExpiresAt",
                table: "EventParticipants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSentAt",
                table: "EventParticipants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WaitlistedAt",
                table: "EventParticipants",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EntryFeeEur",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "Format",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "GameId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "OrganizerSummarySentAt",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "RegistrationDeadline",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "OfferExpiresAt",
                table: "EventParticipants");

            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                table: "EventParticipants");

            migrationBuilder.DropColumn(
                name: "WaitlistedAt",
                table: "EventParticipants");
        }
    }
}
