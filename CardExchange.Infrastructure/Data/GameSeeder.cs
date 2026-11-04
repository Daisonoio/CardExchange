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
                new { Key = "magic", Name = "Magic: The Gathering", Publisher = "Wizards of the Coast", Description = "Il gioco di carte collezionabili più famoso al mondo" },
                new { Key = "pokemon", Name = "Pokémon TCG", Publisher = "The Pokémon Company", Description = "Il gioco di carte collezionabili dei Pokémon, il TCG più venduto al mondo" },
                new { Key = "yugioh", Name = "Yu-Gi-Oh!", Publisher = "Konami", Description = "Il gioco di carte collezionabili basato sul manga e anime Yu-Gi-Oh!" },
                new { Key = "onepiece", Name = "One Piece TCG", Publisher = "Bandai", Description = "Il gioco di carte collezionabili basato sul manga e anime One Piece" },
                new { Key = "lorcana", Name = "Disney Lorcana", Publisher = "Ravensburger", Description = "Il gioco di carte collezionabili con i personaggi Disney" },
            };

            foreach (var g in gamesToSeed)
            {
                var game = await context.Games.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.CatalogKey == g.Key || x.Name == g.Name);
                if (game == null)
                {
                    context.Games.Add(new Game
                    {
                        Name = g.Name,
                        Publisher = g.Publisher,
                        Description = g.Description,
                        IsActive = true,
                        CatalogKey = g.Key
                    });
                }
                else if (game.CatalogKey == null)
                {
                    // Database esistenti: si collega il gioco al suo provider di catalogo
                    game.CatalogKey = g.Key;
                }
            }

            await context.SaveChangesAsync();
        }
    }
}
