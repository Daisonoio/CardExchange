using CardExchange.API.Authorization;
using CardExchange.API.DTOs.Scryfall;
using CardExchange.API.Services;
using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Proxy e integrazione con Scryfall API per Magic: The Gathering
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ScryfallController : ControllerBase
    {
        private readonly IScryfallService _scryfall;
        private readonly ApplicationDbContext _context;
        private readonly IBaseRepository<Game> _gameRepository;
        private readonly ILogger<ScryfallController> _logger;

        public ScryfallController(
            IScryfallService scryfall,
            ApplicationDbContext context,
            IBaseRepository<Game> gameRepository,
            ILogger<ScryfallController> logger)
        {
            _scryfall = scryfall;
            _context = context;
            _gameRepository = gameRepository;
            _logger = logger;
        }

        /// <summary>
        /// Autocomplete per nomi di carte MTG (per il frontend)
        /// </summary>
        [HttpGet("autocomplete")]
        [AllowAnonymous]
        public async Task<IActionResult> Autocomplete([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return BadRequest(new { message = "Il termine di ricerca deve essere di almeno 2 caratteri" });

            var result = await _scryfall.AutocompleteAsync(q);
            if (result == null)
                return StatusCode(502, new { message = "Errore nella comunicazione con Scryfall" });

            return Ok(new
            {
                query = q,
                totalResults = result.TotalValues,
                suggestions = result.Data
            });
        }

        /// <summary>
        /// Ricerca avanzata carte su Scryfall (syntax completa Scryfall)
        /// </summary>
        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<IActionResult> SearchCards([FromQuery] string q, [FromQuery] int page = 1)
        {
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest(new { message = "Il termine di ricerca è obbligatorio" });

            var result = await _scryfall.SearchCardsAsync(q, page);
            if (result == null)
                return Ok(new { query = q, totalCards = 0, hasMore = false, cards = Array.Empty<object>() });

            return Ok(new
            {
                query = q,
                totalCards = result.TotalCards,
                hasMore = result.HasMore,
                page,
                cards = result.Data.Select(MapToResponse)
            });
        }

        /// <summary>
        /// Lookup carta per nome esatto
        /// </summary>
        [HttpGet("cards/named")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCardByName([FromQuery] string exact)
        {
            if (string.IsNullOrWhiteSpace(exact))
                return BadRequest(new { message = "Il nome della carta è obbligatorio" });

            var card = await _scryfall.GetCardByNameAsync(exact);
            if (card == null)
                return NotFound(new { message = $"Carta '{exact}' non trovata su Scryfall" });

            return Ok(MapToResponse(card));
        }

        /// <summary>
        /// Lookup carta per set code e collector number
        /// </summary>
        [HttpGet("cards/{setCode}/{collectorNumber}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCardBySetAndNumber(string setCode, string collectorNumber)
        {
            var card = await _scryfall.GetCardBySetAndNumberAsync(setCode, collectorNumber);
            if (card == null)
                return NotFound(new { message = $"Carta {setCode}/{collectorNumber} non trovata su Scryfall" });

            return Ok(MapToResponse(card));
        }

        /// <summary>
        /// Lookup carta per Scryfall ID (UUID)
        /// </summary>
        [HttpGet("cards/{scryfallId:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCardByScryfallId(string scryfallId)
        {
            var card = await _scryfall.GetCardByScryfallIdAsync(scryfallId);
            if (card == null)
                return NotFound(new { message = "Carta non trovata su Scryfall" });

            return Ok(MapToResponse(card));
        }

        /// <summary>
        /// Valida una lista di carte (bulk, max 75) e indica quali esistono su Scryfall
        /// </summary>
        [HttpPost("validate-collection")]
        [Authorize]
        public async Task<IActionResult> ValidateCollection([FromBody] ValidateCollectionRequest request)
        {
            if (request.Cards == null || request.Cards.Count == 0)
                return BadRequest(new { message = "Almeno una carta è richiesta" });

            if (request.Cards.Count > 75)
                return BadRequest(new { message = "Massimo 75 carte per richiesta (limite Scryfall)" });

            var identifiers = request.Cards.Select(c =>
            {
                if (!string.IsNullOrEmpty(c.ScryfallId))
                    return new ScryfallIdentifier { Id = c.ScryfallId };
                if (!string.IsNullOrEmpty(c.SetCode) && !string.IsNullOrEmpty(c.CollectorNumber))
                    return new ScryfallIdentifier { Set = c.SetCode, CollectorNumber = c.CollectorNumber };
                return new ScryfallIdentifier { Name = c.Name };
            }).ToList();

            var result = await _scryfall.ValidateCollectionAsync(identifiers);
            if (result == null)
                return StatusCode(502, new { message = "Errore nella comunicazione con Scryfall" });

            return Ok(new
            {
                found = result.Data.Select(MapToResponse),
                foundCount = result.Data.Count,
                notFound = result.NotFound,
                notFoundCount = result.NotFound.Count
            });
        }

        /// <summary>
        /// Importa una carta da Scryfall nel DB locale (crea CardInfo + CardSet se necessario)
        /// </summary>
        [HttpPost("import")]
        [Authorize]
        public async Task<IActionResult> ImportCard([FromBody] ImportCardRequest request)
        {
            ScryfallCard? scryfallCard = null;

            // Cerca la carta su Scryfall
            if (!string.IsNullOrEmpty(request.ScryfallId))
                scryfallCard = await _scryfall.GetCardByScryfallIdAsync(request.ScryfallId);
            else if (!string.IsNullOrEmpty(request.SetCode) && !string.IsNullOrEmpty(request.CollectorNumber))
                scryfallCard = await _scryfall.GetCardBySetAndNumberAsync(request.SetCode, request.CollectorNumber);
            else if (!string.IsNullOrEmpty(request.Name))
                scryfallCard = await _scryfall.GetCardByNameAsync(request.Name);

            if (scryfallCard == null)
                return NotFound(new { message = "Carta non trovata su Scryfall. Verificare nome, set code o Scryfall ID." });

            // Verifica se esiste già nel nostro DB
            var existingCardInfo = await _context.CardInfos
                .FirstOrDefaultAsync(ci => ci.ScryfallId == scryfallCard.Id);

            if (existingCardInfo != null)
            {
                // Aggiorna i dati (prezzi, immagini possono cambiare)
                _scryfall.MapScryfallToCardInfo(scryfallCard, existingCardInfo);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Carta già presente, dati aggiornati",
                    cardInfoId = existingCardInfo.Id,
                    isNew = false
                });
            }

            // Trova o crea il Game "Magic: The Gathering"
            var mtgGame = await _context.Games
                .FirstOrDefaultAsync(g => g.Name == "Magic: The Gathering");

            if (mtgGame == null)
            {
                mtgGame = new Game
                {
                    Name = "Magic: The Gathering",
                    Publisher = "Wizards of the Coast",
                    Description = "Il gioco di carte collezionabili più famoso al mondo"
                };
                _context.Games.Add(mtgGame);
                await _context.SaveChangesAsync();
            }

            // Trova o crea il CardSet
            var setCode = scryfallCard.SetCode.ToUpperInvariant();
            var cardSet = await _context.CardSets
                .FirstOrDefaultAsync(cs => cs.Code == setCode && cs.GameId == mtgGame.Id);

            if (cardSet == null)
            {
                var scryfallSet = await _scryfall.GetSetByCodeAsync(scryfallCard.SetCode);
                cardSet = new CardSet { GameId = mtgGame.Id };

                if (scryfallSet != null)
                    _scryfall.MapScryfallToCardSet(scryfallSet, cardSet);
                else
                {
                    cardSet.Code = setCode;
                    cardSet.Name = scryfallCard.SetName;
                }

                _context.CardSets.Add(cardSet);
                await _context.SaveChangesAsync();
            }

            // Crea CardInfo
            var cardInfo = new CardInfo { CardSetId = cardSet.Id };
            _scryfall.MapScryfallToCardInfo(scryfallCard, cardInfo);
            _context.CardInfos.Add(cardInfo);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Carta importata da Scryfall: {CardName} ({SetCode} #{Number}) -> CardInfo ID {CardInfoId}",
                scryfallCard.Name, scryfallCard.SetCode, scryfallCard.CollectorNumber, cardInfo.Id);

            return CreatedAtAction(nameof(GetCardByScryfallId), new { scryfallId = scryfallCard.Id }, new
            {
                message = "Carta importata con successo",
                cardInfoId = cardInfo.Id,
                isNew = true,
                card = MapToResponse(scryfallCard)
            });
        }

        /// <summary>
        /// Sincronizza tutti i set MTG da Scryfall (solo admin)
        /// </summary>
        [HttpPost("sync-sets")]
        [RequirePermission("ADMIN.PANEL")]
        public async Task<IActionResult> SyncSets()
        {
            var scryfallSets = await _scryfall.GetAllSetsAsync();
            if (scryfallSets.Count == 0)
                return StatusCode(502, new { message = "Impossibile recuperare i set da Scryfall" });

            // Trova o crea il Game MTG
            var mtgGame = await _context.Games.FirstOrDefaultAsync(g => g.Name == "Magic: The Gathering");
            if (mtgGame == null)
            {
                mtgGame = new Game
                {
                    Name = "Magic: The Gathering",
                    Publisher = "Wizards of the Coast",
                    Description = "Il gioco di carte collezionabili più famoso al mondo"
                };
                _context.Games.Add(mtgGame);
                await _context.SaveChangesAsync();
            }

            var existingSets = await _context.CardSets
                .Where(cs => cs.GameId == mtgGame.Id)
                .ToDictionaryAsync(cs => cs.Code);

            int created = 0, updated = 0, skipped = 0;

            foreach (var ss in scryfallSets)
            {
                var code = ss.Code.ToUpperInvariant();

                // Salta set solo digitali
                if (ss.Digital)
                {
                    skipped++;
                    continue;
                }

                if (existingSets.TryGetValue(code, out var existing))
                {
                    _scryfall.MapScryfallToCardSet(ss, existing);
                    updated++;
                }
                else
                {
                    var newSet = new CardSet { GameId = mtgGame.Id };
                    _scryfall.MapScryfallToCardSet(ss, newSet);
                    _context.CardSets.Add(newSet);
                    created++;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Sync set Scryfall completata: {Created} creati, {Updated} aggiornati, {Skipped} digitali saltati",
                created, updated, skipped);

            return Ok(new
            {
                message = "Sincronizzazione set completata",
                totalFromScryfall = scryfallSets.Count,
                created,
                updated,
                skippedDigital = skipped
            });
        }

        /// <summary>
        /// Aggiorna i prezzi di mercato per le carte dell'utente corrente
        /// </summary>
        [HttpPost("refresh-prices")]
        [Authorize]
        public async Task<IActionResult> RefreshPrices()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            // Trova tutte le CardInfo con ScryfallId delle carte dell'utente
            var cardInfoIds = await _context.Cards
                .Where(c => c.UserId == userId)
                .Select(c => c.CardInfoId)
                .Distinct()
                .ToListAsync();

            var cardInfos = await _context.CardInfos
                .Where(ci => cardInfoIds.Contains(ci.Id) && ci.ScryfallId != null)
                .ToListAsync();

            if (cardInfos.Count == 0)
                return Ok(new { message = "Nessuna carta con dati Scryfall da aggiornare", updated = 0 });

            // Batch da 75 (limite Scryfall collection endpoint)
            int totalUpdated = 0;
            var batches = cardInfos.Chunk(75);

            foreach (var batch in batches)
            {
                var identifiers = batch
                    .Select(ci => new ScryfallIdentifier { Id = ci.ScryfallId })
                    .ToList();

                var result = await _scryfall.ValidateCollectionAsync(identifiers);
                if (result == null) continue;

                foreach (var scryfallCard in result.Data)
                {
                    var cardInfo = batch.FirstOrDefault(ci => ci.ScryfallId == scryfallCard.Id);
                    if (cardInfo == null) continue;

                    // Aggiorna solo prezzi e immagini
                    if (scryfallCard.Prices != null)
                    {
                        cardInfo.PriceUsd = ParseDecimal(scryfallCard.Prices.Usd);
                        cardInfo.PriceUsdFoil = ParseDecimal(scryfallCard.Prices.UsdFoil);
                        cardInfo.PriceEur = ParseDecimal(scryfallCard.Prices.Eur);
                        cardInfo.PriceEurFoil = ParseDecimal(scryfallCard.Prices.EurFoil);
                    }

                    var images = scryfallCard.ImageUris ?? scryfallCard.CardFaces?.FirstOrDefault()?.ImageUris;
                    if (images != null)
                    {
                        cardInfo.ImageUrl = images.Normal;
                        cardInfo.ImageSmall = images.Small;
                        cardInfo.ImageNormal = images.Normal;
                        cardInfo.ImageLarge = images.Large;
                    }

                    cardInfo.ScryfallUpdatedAt = DateTime.UtcNow;
                    totalUpdated++;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Prezzi aggiornati per {Count} carte dell'utente {UserId}", totalUpdated, userId);

            return Ok(new
            {
                message = $"Prezzi aggiornati per {totalUpdated} carte",
                updated = totalUpdated,
                total = cardInfos.Count
            });
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static object MapToResponse(ScryfallCard card)
        {
            var images = card.ImageUris ?? card.CardFaces?.FirstOrDefault()?.ImageUris;

            return new
            {
                scryfallId = card.Id,
                oracleId = card.OracleId,
                name = card.Name,
                manaCost = card.ManaCost,
                cmc = card.Cmc,
                typeLine = card.TypeLine,
                oracleText = card.OracleText,
                colors = card.Colors,
                colorIdentity = card.ColorIdentity,
                power = card.Power,
                toughness = card.Toughness,
                loyalty = card.Loyalty,
                keywords = card.Keywords,
                setCode = card.SetCode,
                setName = card.SetName,
                collectorNumber = card.CollectorNumber,
                rarity = card.Rarity,
                artist = card.Artist,
                images = images != null ? new
                {
                    small = images.Small,
                    normal = images.Normal,
                    large = images.Large,
                    png = images.Png,
                    artCrop = images.ArtCrop,
                    borderCrop = images.BorderCrop
                } : null,
                prices = card.Prices != null ? new
                {
                    usd = card.Prices.Usd,
                    usdFoil = card.Prices.UsdFoil,
                    eur = card.Prices.Eur,
                    eurFoil = card.Prices.EurFoil
                } : null,
                legalities = card.Legalities,
                scryfallUri = card.ScryfallUri
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
            return decimal.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : null;
        }
    }

    // ================================================================
    // Request DTOs specifici per ScryfallController
    // ================================================================

    public class ValidateCollectionRequest
    {
        public List<CollectionCardIdentifier> Cards { get; set; } = new();
    }

    public class CollectionCardIdentifier
    {
        public string? ScryfallId { get; set; }
        public string? Name { get; set; }
        public string? SetCode { get; set; }
        public string? CollectorNumber { get; set; }
    }

    public class ImportCardRequest
    {
        public string? ScryfallId { get; set; }
        public string? Name { get; set; }
        public string? SetCode { get; set; }
        public string? CollectorNumber { get; set; }
    }
}
