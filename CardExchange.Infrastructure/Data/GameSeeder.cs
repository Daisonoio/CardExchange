using CardExchange.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.Infrastructure.Data
{
    public static class GameSeeder
    {
        public static async Task SeedGames(ApplicationDbContext context)
        {
            var gamesToSeed = new[]
            {
                new { Name = "Magic: The Gathering", Publisher = "Wizards of the Coast", Description = "Il gioco di carte collezionabili più famoso al mondo" },
                new { Name = "Pokémon TCG", Publisher = "The Pokémon Company", Description = "Il gioco di carte collezionabili dei Pokémon, il TCG più venduto al mondo" },
                new { Name = "Yu-Gi-Oh!", Publisher = "Konami", Description = "Il gioco di carte collezionabili basato sul manga e anime Yu-Gi-Oh!" },
                new { Name = "One Piece TCG", Publisher = "Bandai", Description = "Il gioco di carte collezionabili basato sul manga e anime One Piece" },
            };

            foreach (var g in gamesToSeed)
            {
                var exists = await context.Games.AnyAsync(x => x.Name == g.Name);
                if (!exists)
                {
                    context.Games.Add(new Game
                    {
                        Name = g.Name,
                        Publisher = g.Publisher,
                        Description = g.Description,
                        IsActive = true
                    });
                }
            }

            await context.SaveChangesAsync();
        }
    }
}
