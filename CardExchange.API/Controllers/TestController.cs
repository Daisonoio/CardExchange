using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Controller per test e sviluppo - disponibile SOLO in ambiente Development
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperAdmin")]
    public class TestController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<TestController> _logger;

        public TestController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            ILogger<TestController> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        [HttpGet("database-connection")]
        public async Task<IActionResult> TestDatabaseConnection()
        {
            if (!_environment.IsDevelopment())
            {
                return NotFound();
            }

            try
            {
                var canConnect = await _context.Database.CanConnectAsync();

                if (!canConnect)
                {
                    return Ok(new { success = false, message = "Impossibile connettersi al database" });
                }

                return Ok(new
                {
                    success = true,
                    message = "Connessione al database riuscita",
                    database = _context.Database.GetDbConnection().Database,
                    canConnect
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il test di connessione al database");
                return StatusCode(500, new { success = false, message = "Errore di connessione al database" });
            }
        }

        [HttpPost("seed-data")]
        public async Task<IActionResult> SeedTestData()
        {
            if (!_environment.IsDevelopment())
            {
                return NotFound();
            }

            try
            {
                var existingUsers = await _context.Users.CountAsync();
                if (existingUsers > 0)
                {
                    return Ok(new { success = false, message = "Dati di test già esistenti" });
                }

                var game = new Game
                {
                    Name = "Magic: The Gathering",
                    Description = "Il gioco di carte collezionabili più famoso al mondo",
                    Publisher = "Wizards of the Coast"
                };
                _context.Games.Add(game);

                var user = new User
                {
                    Email = "test@example.com",
                    Username = "testuser",
                    FirstName = "Mario",
                    LastName = "Rossi",
                    Bio = "Collezionista di carte Magic da 10 anni",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPassword1!"),
                    EmailConfirmed = true
                };
                _context.Users.Add(user);

                await _context.SaveChangesAsync();

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

                var card = new Card
                {
                    UserId = user.Id,
                    CardInfoId = cardInfo.Id,
                    Condition = CardCondition.NearMint,
                    Notes = "In ottime condizioni",
                    EstimatedValue = 2.50m
                };
                _context.Cards.Add(card);

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

                _logger.LogInformation("Dati di test creati con successo");

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
                _logger.LogError(ex, "Errore durante la creazione dei dati di test");
                return StatusCode(500, new { success = false, message = "Errore durante la creazione dei dati di test" });
            }
        }

        [HttpGet("data-summary")]
        public async Task<IActionResult> GetDataSummary()
        {
            if (!_environment.IsDevelopment())
            {
                return NotFound();
            }

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
                _logger.LogError(ex, "Errore durante il recupero del riepilogo dati");
                return StatusCode(500, new { success = false, message = "Errore durante il recupero dei dati" });
            }
        }
    }
}
