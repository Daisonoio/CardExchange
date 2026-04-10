using CardExchange.API.Authorization;
using CardExchange.API.DTOs.YuGiOh;
using CardExchange.API.Services;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Integrazione con YGOProDeck API per Yu-Gi-Oh! — DB-first con cache aggressiva
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class YuGiOhController : ControllerBase
    {
        private readonly IYuGiOhService _yugioh;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<YuGiOhController> _logger;

        private const int StalenessHours = 24;
        private const int PriceStalenessHours = 12;

        public YuGiOhController(
            IYuGiOhService yugioh,
            ApplicationDbContext context,
            ILogger<YuGiOhController> logger)
        {
            _yugioh = yugioh;
            _context = context;
            _logger = logger;
        }

        // ================================================================
        // Ricerca — prima DB, fallback API
        // ================================================================

        /// <summary>
        /// Ricerca carte Yu-Gi-Oh! Cerca prima nel DB locale, poi nell'API YGOProDeck.
        /// </summary>
        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<IActionResult> SearchCards([FromQuery] string q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return BadRequest(new { message = "Il termine di ricerca deve essere almeno 2 caratteri" });

            pageSize = Math.Clamp(pageSize, 1, 50);

            // 1. Cerca nel DB locale
            var yugiohGameId = await GetYuGiOhGameIdAsync();
            if (yugiohGameId.HasValue)
            {
                var dbQuery = _context.CardInfos
                    .AsNoTracking()
                    .Where(ci => ci.CardSet.GameId == yugiohGameId.Value
                              && ci.YuGiOhId != null
                              && ci.Name.Contains(q));

                var totalLocal = await dbQuery.CountAsync();
                if (totalLocal > 0)
                {
                    var localCards = await dbQuery
                        .OrderBy(ci => ci.Name)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(ci => MapToResponse(ci))
                        .ToListAsync();

                    return Ok(new
                    {
                        source = "db",
                        query = q,
                        page,
                        pageSize,
                        totalCount = totalLocal,
                        cards = localCards
                    });
                }
            }

            // 2. Fallback: chiama API YGOProDeck
            var offset = (page - 1) * pageSize;
            var apiCards = await _yugioh.SearchCardsAsync(q, offset, pageSize);

            return Ok(new
            {
                source = "api",
                query = q,
                page,
                pageSize,
                totalCount = apiCards.Count,
                cards = apiCards.Select(MapApiCardToResponse)
            });
        }

        /// <summary>
        /// Ottieni carta per Yu-Gi-Oh! passcode ID. Serve da DB se presente, altrimenti API.
        /// </summary>
        [HttpGet("cards/{yugiohId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCard(int yugiohId)
        {
            // 1. Cerca in DB
            var cardInfo = await _context.CardInfos
                .AsNoTracking()
                .Include(ci => ci.CardSet)
                .FirstOrDefaultAsync(ci => ci.YuGiOhId == yugiohId);

            if (cardInfo != null && !IsStale(cardInfo.YuGiOhUpdatedAt))
                return Ok(new { source = "db", card = MapToResponse(cardInfo) });

            // 2. Chiama API
            var apiCard = await _yugioh.GetCardByIdAsync(yugiohId);
            if (apiCard == null)
            {
                if (cardInfo != null)
                    return Ok(new { source = "db_stale", card = MapToResponse(cardInfo) });

                return NotFound(new { message = $"Carta Yu-Gi-Oh! con ID '{yugiohId}' non trovata" });
            }

            return Ok(new { source = "api", card = MapApiCardToResponse(apiCard) });
        }

        // ================================================================
        // Import — salva carta/set nel DB locale
        // ================================================================

        /// <summary>
        /// Importa una carta Yu-Gi-Oh! nel DB locale dall'API. Crea/aggiorna CardInfo + CardSet.
        /// </summary>
        [HttpPost("import")]
        [Authorize]
        public async Task<IActionResult> ImportCard([FromBody] YuGiOhImportRequest request)
        {
            if (request.YuGiOhId <= 0)
                return BadRequest(new { message = "YuGiOhId è obbligatorio e deve essere un numero positivo" });

            // Verifica se esiste già nel DB
            var existingCardInfo = await _context.CardInfos
                .FirstOrDefaultAsync(ci => ci.YuGiOhId == request.YuGiOhId);

            if (existingCardInfo != null && !IsStale(existingCardInfo.YuGiOhUpdatedAt))
            {
                return Ok(new
                {
                    message = "Carta già presente nel DB",
                    cardInfoId = existingCardInfo.Id,
                    isNew = false
                });
            }

            // Chiama API
            var apiCard = await _yugioh.GetCardByIdAsync(request.YuGiOhId);
            if (apiCard == null)
                return NotFound(new { message = $"Carta con ID '{request.YuGiOhId}' non trovata nell'API YGOProDeck" });

            // Trova o crea il Game Yu-Gi-Oh!
            var yugiohGame = await EnsureYuGiOhGameAsync();

            // Trova o crea il CardSet (dal primo set della carta)
            var cardSet = await EnsureCardSetAsync(apiCard, yugiohGame.Id);

            if (existingCardInfo != null)
            {
                _yugioh.MapToCardInfo(apiCard, existingCardInfo);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Carta aggiornata",
                    cardInfoId = existingCardInfo.Id,
                    isNew = false
                });
            }

            // Crea nuova CardInfo
            var cardInfo = new CardInfo { CardSetId = cardSet.Id };
            _yugioh.MapToCardInfo(apiCard, cardInfo);
            _context.CardInfos.Add(cardInfo);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Carta Yu-Gi-Oh! importata: {Name} (ID {YuGiOhId}) -> CardInfo ID {Id}",
                apiCard.Name, apiCard.Id, cardInfo.Id);

            return CreatedAtAction(nameof(GetCard), new { yugiohId = request.YuGiOhId }, new
            {
                message = "Carta importata con successo",
                cardInfoId = cardInfo.Id,
                isNew = true
            });
        }

        // ================================================================
        // Sync — sincronizzazione bulk (solo admin)
        // ================================================================

        /// <summary>
        /// Sincronizza tutti i set Yu-Gi-Oh! dall'API nel DB locale.
        /// </summary>
        [HttpPost("sync-sets")]
        [RequirePermission("ADMIN.PANEL")]
        public async Task<IActionResult> SyncSets()
        {
            var apiSets = await _yugioh.GetAllSetsAsync();
            if (apiSets.Count == 0)
                return StatusCode(502, new { message = "Impossibile recuperare i set dall'API YGOProDeck" });

            var yugiohGame = await EnsureYuGiOhGameAsync();

            var existingSets = await _context.CardSets
                .Where(cs => cs.GameId == yugiohGame.Id && cs.YuGiOhSetCode != null)
                .ToDictionaryAsync(cs => cs.YuGiOhSetCode!);

            int created = 0, updated = 0;

            foreach (var apiSet in apiSets)
            {
                if (existingSets.TryGetValue(apiSet.SetCode, out var existing))
                {
                    _yugioh.MapToCardSet(apiSet, existing);
                    updated++;
                }
                else
                {
                    var newSet = new CardSet { GameId = yugiohGame.Id };
                    _yugioh.MapToCardSet(apiSet, newSet);
                    _context.CardSets.Add(newSet);
                    created++;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Sync set Yu-Gi-Oh!: {Created} creati, {Updated} aggiornati (totale API: {Total})",
                created, updated, apiSets.Count);

            return Ok(new
            {
                message = "Sincronizzazione set completata",
                totalFromApi = apiSets.Count,
                created,
                updated
            });
        }

        /// <summary>
        /// Aggiorna i prezzi di mercato per le carte Yu-Gi-Oh! dell'utente corrente.
        /// </summary>
        [HttpPost("refresh-prices")]
        [Authorize]
        public async Task<IActionResult> RefreshPrices()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var staleBefore = DateTime.UtcNow.AddHours(-PriceStalenessHours);

            var cardInfos = await _context.Cards
                .Where(c => c.UserId == userId)
                .Select(c => c.CardInfo)
                .Where(ci => ci.YuGiOhId != null
                           && (ci.YuGiOhUpdatedAt == null || ci.YuGiOhUpdatedAt < staleBefore))
                .Distinct()
                .ToListAsync();

            if (cardInfos.Count == 0)
                return Ok(new { message = "Nessuna carta Yu-Gi-Oh! da aggiornare (prezzi ancora freschi)", updated = 0 });

            int totalUpdated = 0;

            foreach (var cardInfo in cardInfos)
            {
                var apiCard = await _yugioh.GetCardByIdAsync(cardInfo.YuGiOhId!.Value);
                if (apiCard == null) continue;

                // Aggiorna solo prezzi e immagini
                var prices = apiCard.CardPrices?.FirstOrDefault();
                if (prices != null)
                {
                    cardInfo.PriceYuGiOhTcgPlayer = ParseDecimal(prices.TcgPlayerPrice);
                    cardInfo.PriceYuGiOhCardmarket = ParseDecimal(prices.CardmarketPrice);
                    cardInfo.PriceYuGiOhEbay = ParseDecimal(prices.EbayPrice);
                    cardInfo.PriceYuGiOhAmazon = ParseDecimal(prices.AmazonPrice);
                    cardInfo.PriceYuGiOhCoolstuffinc = ParseDecimal(prices.CoolStuffIncPrice);
                    cardInfo.PriceUsd = ParseDecimal(prices.TcgPlayerPrice);
                    cardInfo.PriceEur = ParseDecimal(prices.CardmarketPrice);
                }

                var image = apiCard.CardImages?.FirstOrDefault();
                if (image != null)
                {
                    cardInfo.YuGiOhImageUrl = image.ImageUrl;
                    cardInfo.YuGiOhImageSmall = image.ImageUrlSmall;
                }

                cardInfo.YuGiOhUpdatedAt = DateTime.UtcNow;
                totalUpdated++;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Prezzi Yu-Gi-Oh! aggiornati per {Count} carte dell'utente {UserId}", totalUpdated, userId);

            return Ok(new
            {
                message = $"Prezzi aggiornati per {totalUpdated} carte Yu-Gi-Oh!",
                updated = totalUpdated,
                total = cardInfos.Count
            });
        }

        // ================================================================
        // Set list — dal DB se popolato
        // ================================================================

        /// <summary>
        /// Lista set Yu-Gi-Oh! Ritorna dal DB se sincronizzati, altrimenti dall'API.
        /// </summary>
        [HttpGet("sets")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSets()
        {
            var yugiohGameId = await GetYuGiOhGameIdAsync();
            if (yugiohGameId.HasValue)
            {
                var dbSets = await _context.CardSets
                    .AsNoTracking()
                    .Where(cs => cs.GameId == yugiohGameId.Value && cs.YuGiOhSetCode != null)
                    .OrderByDescending(cs => cs.ReleaseDate)
                    .Select(cs => new
                    {
                        setCode = cs.YuGiOhSetCode,
                        name = cs.Name,
                        numCards = cs.YuGiOhNumCards,
                        tcgDate = cs.YuGiOhTcgDate,
                        releaseDate = cs.ReleaseDate
                    })
                    .ToListAsync();

                if (dbSets.Count > 0)
                    return Ok(new { source = "db", totalSets = dbSets.Count, sets = dbSets });
            }

            // Fallback: API
            var apiSets = await _yugioh.GetAllSetsAsync();
            return Ok(new
            {
                source = "api",
                totalSets = apiSets.Count,
                sets = apiSets.Select(s => new
                {
                    setCode = s.SetCode,
                    name = s.SetName,
                    numCards = s.NumOfCards,
                    tcgDate = s.TcgDate
                })
            });
        }

        // ================================================================
        // Helpers
        // ================================================================

        private async Task<int?> GetYuGiOhGameIdAsync()
        {
            var game = await _context.Games
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Name == "Yu-Gi-Oh!");
            return game?.Id;
        }

        private async Task<Game> EnsureYuGiOhGameAsync()
        {
            var game = await _context.Games.FirstOrDefaultAsync(g => g.Name == "Yu-Gi-Oh!");
            if (game != null) return game;

            game = new Game
            {
                Name = "Yu-Gi-Oh!",
                Publisher = "Konami",
                Description = "Il gioco di carte collezionabili basato sul manga e anime Yu-Gi-Oh!"
            };
            _context.Games.Add(game);
            await _context.SaveChangesAsync();
            return game;
        }

        private async Task<CardSet> EnsureCardSetAsync(YuGiOhCard apiCard, int gameId)
        {
            // Prendi il primo set della carta (o crea un set generico)
            var firstSet = apiCard.CardSets?.FirstOrDefault();
            var setCode = firstSet?.SetCode ?? "MISC";
            var setName = firstSet?.SetName ?? "Miscellaneous";

            var cardSet = await _context.CardSets
                .FirstOrDefaultAsync(cs => cs.YuGiOhSetCode == setCode && cs.GameId == gameId);

            if (cardSet != null) return cardSet;

            cardSet = new CardSet
            {
                GameId = gameId,
                Name = setName,
                Code = setCode.ToUpperInvariant(),
                YuGiOhSetCode = setCode,
                YuGiOhUpdatedAt = DateTime.UtcNow
            };
            _context.CardSets.Add(cardSet);
            await _context.SaveChangesAsync();
            return cardSet;
        }

        private static bool IsStale(DateTime? lastUpdate)
        {
            if (!lastUpdate.HasValue) return true;
            return (DateTime.UtcNow - lastUpdate.Value).TotalHours > StalenessHours;
        }

        private static object MapToResponse(CardInfo ci)
        {
            return new
            {
                cardInfoId = ci.Id,
                yugiohId = ci.YuGiOhId,
                name = ci.Name,
                type = ci.YuGiOhType,
                frameType = ci.YuGiOhFrameType,
                description = ci.Description,
                attribute = ci.YuGiOhAttribute,
                race = ci.YuGiOhRace,
                level = ci.YuGiOhLevel,
                atk = ci.YuGiOhAtk,
                def = ci.YuGiOhDef,
                archetype = ci.YuGiOhArchetype,
                rarity = ci.Rarity,
                cardNumber = ci.CardNumber,
                images = new
                {
                    url = ci.YuGiOhImageUrl,
                    small = ci.YuGiOhImageSmall
                },
                prices = new
                {
                    tcgPlayer = ci.PriceYuGiOhTcgPlayer,
                    cardmarket = ci.PriceYuGiOhCardmarket,
                    ebay = ci.PriceYuGiOhEbay,
                    amazon = ci.PriceYuGiOhAmazon,
                    coolstuffinc = ci.PriceYuGiOhCoolstuffinc,
                    usd = ci.PriceUsd,
                    eur = ci.PriceEur
                },
                setName = ci.CardSet?.Name,
                lastUpdated = ci.YuGiOhUpdatedAt
            };
        }

        private static object MapApiCardToResponse(YuGiOhCard card)
        {
            var image = card.CardImages?.FirstOrDefault();
            var prices = card.CardPrices?.FirstOrDefault();
            var firstSet = card.CardSets?.FirstOrDefault();

            return new
            {
                yugiohId = card.Id,
                name = card.Name,
                type = card.Type,
                frameType = card.FrameType,
                description = card.Desc,
                attribute = card.Attribute,
                race = card.Race,
                level = card.Level ?? card.LinkVal,
                atk = card.Atk,
                def = card.Def,
                archetype = card.Archetype,
                rarity = firstSet?.SetRarity,
                cardNumber = firstSet?.SetCode,
                images = new
                {
                    url = image?.ImageUrl,
                    small = image?.ImageUrlSmall
                },
                prices = new
                {
                    tcgPlayer = ParseDecimal(prices?.TcgPlayerPrice),
                    cardmarket = ParseDecimal(prices?.CardmarketPrice),
                    ebay = ParseDecimal(prices?.EbayPrice),
                    amazon = ParseDecimal(prices?.AmazonPrice),
                    coolstuffinc = ParseDecimal(prices?.CoolStuffIncPrice)
                },
                setName = firstSet?.SetName,
                totalSets = card.CardSets?.Count ?? 0
            };
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
        }

        private static decimal? ParseDecimal(string? value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            return decimal.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var result) && result > 0 ? result : null;
        }
    }

    // ================================================================
    // Request DTOs
    // ================================================================

    public class YuGiOhImportRequest
    {
        public int YuGiOhId { get; set; }
    }
}
