using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TestController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("database-connection")]
        public async Task<IActionResult> TestDatabaseConnection()
        {
            try
            {
                // Test connessione database
                var canConnect = await _context.Database.CanConnectAsync();

                if (!canConnect)
                {
                    return Ok(new { success = false, message = "Impossibile connettersi al database" });
                }

                // Conta le tabelle
                var tablesCount = await _context.Database.ExecuteSqlRawAsync("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'");

                return Ok(new
                {
                    success = true,
                    message = "Connessione al database riuscita",
                    database = _context.Database.GetDbConnection().Database,
                    canConnect = canConnect
                });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = $"Errore: {ex.Message}" });
            }
        }

        [HttpPost("seed-data")]
        public async Task<IActionResult> SeedTestData()
        {
            try
            {
                // Verifica se esistono già dati
                var existingUsers = await _context.Users.CountAsync();
                if (existingUsers > 0)
                {
                    return Ok(new { success = false, message = "Dati di test già esistenti" });
                }

                // Crea un gioco di test
                var game = new Game
                {
                    Name = "Magic: The Gathering",
                    Description = "Il gioco di carte collezionabili più famoso al mondo",
                    Publisher = "Wizards of the Coast"
                };
                _context.Games.Add(game);

                // Crea un utente di test
                var user = new User
                {
                    Email = "test@example.com",
                    Username = "testuser",
                    FirstName = "Mario",
                    LastName = "Rossi",
                    Bio = "Collezionista di carte Magic da 10 anni",
                    PasswordHash = "hashedpassword123", // In produzione sarà hasdata correttamente
                    EmailConfirmed = true
                };
                _context.Users.Add(user);

                await _context.SaveChangesAsync();

                // Aggiungi la location per l'utente
                var userLocation = new UserLocation
                {
                    UserId = user.Id,
                    City = "Milano",
                    Province = "MI",
                    Country = "Italia",
                    PostalCode = "20100",
                    Latitude = 45.4642m,
                    Longitude = 9.1900m,
                    MaxDistanceKm = 50
                };
                _context.UserLocations.Add(userLocation);

                // Aggiungi un set di carte
                var cardSet = new CardSet
                {
                    GameId = game.Id,
                    Name = "Core Set 2023",
                    Code = "M23",
                    ReleaseDate = new DateTime(2023, 7, 15),
                    Description = "Set base di Magic 2023"
                };
                _context.CardSets.Add(cardSet);

                await _context.SaveChangesAsync();

                // Aggiungi informazioni carta
                var cardInfo = new CardInfo
                {
                    CardSetId = cardSet.Id,
                    Name = "Lightning Bolt",
                    CardNumber = "123",
                    Rarity = "Common",
                    Type = "Instant",
                    Description = "Lightning Bolt deals 3 damage to any target."
                };
                _context.CardInfos.Add(cardInfo);

                await _context.SaveChangesAsync();

                // Aggiungi carta alla collezione dell'utente
                var card = new Card
                {
                    UserId = user.Id,
                    CardInfoId = cardInfo.Id,
                    Condition = CardCondition.NearMint,
                    Notes = "In ottime condizioni",
                    EstimatedValue = 2.50m
                };
                _context.Cards.Add(card);

                // Aggiungi carta alla wishlist
                var wishlistItem = new WishlistItem
                {
                    UserId = user.Id,
                    CardInfoId = cardInfo.Id,
                    PreferredCondition = CardCondition.Mint,
                    MaxPrice = 5.00m,
                    Notes = "Cerco questa carta in condizioni perfette",
                    Priority = 1
                };
                _context.WishlistItems.Add(wishlistItem);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Dati di test creati con successo",
                    data = new
                    {
                        userId = user.Id,
                        gameId = game.Id,
                        cardSetId = cardSet.Id,
                        cardInfoId = cardInfo.Id,
                        cardId = card.Id
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = $"Errore durante la creazione dei dati: {ex.Message}" });
            }
        }

        [HttpGet("data-summary")]
        public async Task<IActionResult> GetDataSummary()
        {
            try
            {
                var summary = new
                {
                    users = await _context.Users.CountAsync(),
                    games = await _context.Games.CountAsync(),
                    cardSets = await _context.CardSets.CountAsync(),
                    cardInfos = await _context.CardInfos.CountAsync(),
                    cards = await _context.Cards.CountAsync(),
                    wishlistItems = await _context.WishlistItems.CountAsync(),
                    tradeOffers = await _context.TradeOffers.CountAsync()
                };

                return Ok(new { success = true, summary });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = $"Errore: {ex.Message}" });
            }
        }
    }
}