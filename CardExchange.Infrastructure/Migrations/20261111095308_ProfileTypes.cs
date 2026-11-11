using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardExchange.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProfileTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProfileType",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "HasEventExhibitors",
                table: "SubscriptionPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Chi ha già un negozio verificato passa al profilo Negozio (2), chi ha un piano Banco attivo a Venditore (1)
            migrationBuilder.Sql("UPDATE Users SET ProfileType = 2 WHERE Id IN (SELECT OwnerId FROM Stores WHERE Status = 2)");
            migrationBuilder.Sql(@"UPDATE Users SET ProfileType = 1 WHERE ProfileType = 0 AND Id IN (
                SELECT s.UserId FROM UserSubscriptions s JOIN SubscriptionPlans p ON p.Id = s.PlanId
                WHERE s.Status = 1 AND p.Tier = 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProfileType",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HasEventExhibitors",
                table: "SubscriptionPlans");
        }
    }
}
