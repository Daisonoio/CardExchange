using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardExchange.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlanEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasBuylist",
                table: "SubscriptionPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasEarlyAccess",
                table: "SubscriptionPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasFeaturedStore",
                table: "SubscriptionPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasInstantAlerts",
                table: "SubscriptionPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasLocalDemand",
                table: "SubscriptionPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasPriceAlerts",
                table: "SubscriptionPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasReservations",
                table: "SubscriptionPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasSellerShowcase",
                table: "SubscriptionPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HidesBrandSponsors",
                table: "SubscriptionPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxActiveReservations",
                table: "SubscriptionPlans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxBroadcastsPerDay",
                table: "SubscriptionPlans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxRadiusKm",
                table: "SubscriptionPlans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MonthlyPromotionCredits",
                table: "SubscriptionPlans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "SubscriptionPlans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StripePriceId",
                table: "SubscriptionPlans",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasBuylist",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "HasEarlyAccess",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "HasFeaturedStore",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "HasInstantAlerts",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "HasLocalDemand",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "HasPriceAlerts",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "HasReservations",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "HasSellerShowcase",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "HidesBrandSponsors",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "MaxActiveReservations",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "MaxBroadcastsPerDay",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "MaxRadiusKm",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "MonthlyPromotionCredits",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "StripePriceId",
                table: "SubscriptionPlans");
        }
    }
}
