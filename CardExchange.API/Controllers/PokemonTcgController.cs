using CardExchange.API.Authorization;
using CardExchange.API.DTOs.PokemonTcg;
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
    /// Integrazione con Pokémon TCG API — DB-first con cache aggressiva
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PokemonTcgController : ControllerBase
    {
        private readonly IPokemonTcgService _pokemonTcg;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PokemonTcgController> _logger;

        // Soglia di aggiornamento: se i dati in DB sono più vecchi di N ore, li aggiorna dall'API
        private const int StalenessHours = 24;
        private const int PriceStalenessHours = 12;

        public PokemonTcgController(
            IPokemonTcgService pokemonTcg,
            ApplicationDbContext context,
            ILogger<PokemonTcgController> logger)
        {
            _pokemonTcg = pokemonTcg;
            _context = context;
            _logger = logger;
        }

        // ================================================================
        // Ricerca — prima DB, fallback API
        // ================================================================

        /// <summary>
        /// Ricerca carte Pokémon TCG. Cerca prima nel DB locale, poi nell'API esterna.
        /// </summary>
        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<IActionResult> SearchCards([FromQuery] string q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return BadRequest(new { message = "Il termine di ricerca deve essere almeno 2 caratteri" });

            pageSize = Math.Clamp(pageSize, 1, 50);

            // 1. Cerca nel DB locale
            var pokemonGameId = await GetPokemonGameIdAsync();
            if (pokemonGameId.HasValue)
            {
                var dbQuery = _context.CardInfos
                    .AsNoTracking()
                    .Where(ci => ci.CardSet.GameId == pokemonGameId.Value
                              && ci.PokemonTcgId != null
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

            // 2. Fallback: chiama API
            var apiQuery = $"name:\"{q}\"";
            var result = await _pokemonTcg.SearchCardsAsync(apiQuery, page, pageSize);
            if (result == null)
                return Ok(new { source = "api", query = q, totalCount = 0, cards = Array.Empty<object>() });

            return Ok(new
            {
                source = "api",
                query = q,
                page = result.Page,
                pageSize = result.PageSize,
                totalCount = result.TotalCount,
                cards = result.Data.Select(MapApiCardToResponse)
            });
        }

        /// <summary>
        /// Ottieni carta per Pokémon TCG ID (es. "base1-4"). Serve da DB se presente, altrimenti API.
        /// </summary>
        [HttpGet("cards/{pokemonTcgId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCard(string pokemonTcgId)
        {
            // 1. Cerca in DB
            var cardInfo = await _context.CardInfos
                .AsNoTracking()
                .Include(ci => ci.CardSet)
                .FirstOrDefaultAsync(ci => ci.PokemonTcgId == pokemonTcgId);

            if (cardInfo != null && !IsStale(cardInfo.PokemonTcgUpdatedAt))
                return Ok(new { source = "db", card = MapToResponse(cardInfo) });

            // 2. Chiama API
            var apiCard = await _pokemonTcg.GetCardByIdAsync(pokemonTcgId);
            if (apiCard == null)
            {
                if (cardInfo != null)
                    return Ok(new { source = "db_stale", card = MapToResponse(cardInfo) });

                return NotFound(new { message = $"Carta '{pokemonTcgId}' non trovata" });
            }

            return Ok(new { source = "api", card = MapApiCardToResponse(apiCard) });
        }

        // ================================================================
        // Import — salva carta/set nel DB locale
        // ================================================================

        /// <summary>
        /// Importa una carta Pokémon nel DB locale dall'API. Crea/aggiorna CardInfo + CardSet.
        /// </summary>
        [HttpPost("import")]
        [Authorize]
        public async Task<IActionResult> ImportCard([FromBody] PokemonImportRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PokemonTcgId))
                return BadRequest(new { message = "PokemonTcgId è obbligatorio" });

            // Verifica se esiste già nel DB
            var existingCardInfo = await _context.CardInfos
                .FirstOrDefaultAsync(ci => ci.PokemonTcgId == request.PokemonTcgId);

            if (existingCardInfo != null && !IsStale(existingCardInfo.PokemonTcgUpdatedAt))
            {
                return Ok(new
                {
                    message = "Carta già presente nel DB",
                    cardInfoId = existingCardInfo.Id,
                    isNew = false
                });
            }

            // Chiama API
            var apiCard = await _pokemonTcg.GetCardByIdAsync(request.PokemonTcgId);
            if (apiCard == null)
                return NotFound(new { message = $"Carta '{request.PokemonTcgId}' non trovata nell'API Pokémon TCG" });

            // Trova o crea il Game Pokémon TCG
            var pokemonGame = await EnsurePokemonGameAsync();

            // Trova o crea il CardSet
            var cardSet = await EnsureCardSetAsync(apiCard, pokemonGame.Id);

            if (existingCardInfo != null)
            {
                // Aggiorna
                _pokemonTcg.MapToCardInfo(apiCard, existingCardInfo);
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
            _pokemonTcg.MapToCardInfo(apiCard, cardInfo);
            _context.CardInfos.Add(cardInfo);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Carta Pokémon importata: {Name} ({SetId} #{Number}) -> CardInfo ID {Id}",
                apiCard.Name, apiCard.Set?.Id, apiCard.Number, cardInfo.Id);

            return CreatedAtAction(nameof(GetCard), new { pokemonTcgId = request.PokemonTcgId }, new
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
        /// Sincronizza tutti i set Pokémon TCG dall'API nel DB locale.
        /// </summary>
        [HttpPost("sync-sets")]
        [RequirePermission("ADMIN.PANEL")]
        public async Task<IActionResult> SyncSets()
        {
            var apiSets = await _pokemonTcg.GetAllSetsAsync();
            if (apiSets.Count == 0)
                return StatusCode(502, new { message = "Impossibile recuperare i set dall'API Pokémon TCG" });

            var pokemonGame = await EnsurePokemonGameAsync();

            var existingSets = await _context.CardSets
                .Where(cs => cs.GameId == pokemonGame.Id && cs.PokemonTcgId != null)
                .ToDictionaryAsync(cs => cs.PokemonTcgId!);

            int created = 0, updated = 0;

            foreach (var apiSet in apiSets)
            {
                if (existingSets.TryGetValue(apiSet.Id, out var existing))
                {
                    _pokemonTcg.MapToCardSet(apiSet, existing);
                    updated++;
                }
                else
                {
                    var newSet = new CardSet { GameId = pokemonGame.Id };
                    _pokemonTcg.MapToCardSet(apiSet, newSet);
                    _context.CardSets.Add(newSet);
                    created++;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Sync set Pokémon TCG: {Created} creati, {Updated} aggiornati (totale API: {Total})",
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
        /// Sincronizza tutte le carte di un set specifico nel DB locale.
        /// </summary>
        [HttpPost("sync-set-cards/{setId}")]
        [RequirePermission("ADMIN.PANEL")]
        public async Task<IActionResult> SyncSetCards(string setId)
        {
            var pokemonGame = await EnsurePokemonGameAsync();
            var cardSet = await EnsureCardSetFromApiAsync(setId, pokemonGame.Id);
            if (cardSet == null)
                return NotFound(new { message = $"Set '{setId}' non trovato" });

            // Carica tutte le carte del set dall'API (paginando)
            int page = 1, created = 0, updated = 0;
            bool hasMore = true;

            var existingCards = await _context.CardInfos
                .Where(ci => ci.CardSetId == cardSet.Id && ci.PokemonTcgId != null)
                .ToDictionaryAsync(ci => ci.PokemonTcgId!);

            while (hasMore)
            {
                var result = await _pokemonTcg.GetCardsBySetAsync(setId, page, 250);
                if (result == null || result.Data == null || result.Data.Count == 0)
                    break;

                foreach (var apiCard in result.Data)
                {
                    if (existingCards.TryGetValue(apiCard.Id, out var existing))
                    {
                        _pokemonTcg.MapToCardInfo(apiCard, existing);
                        updated++;
                    }
                    else
                    {
                        var cardInfo = new CardInfo { CardSetId = cardSet.Id };
                        _pokemonTcg.MapToCardInfo(apiCard, cardInfo);
                        _context.CardInfos.Add(cardInfo);
                        existingCards[apiCard.Id] = cardInfo;
                        created++;
                    }
                }

                // Salva in batch per efficienza
                await _context.SaveChangesAsync();

                hasMore = result.Count.HasValue && result.TotalCount.HasValue
                    && (page * (result.PageSize ?? 250)) < result.TotalCount.Value;
                page++;
            }

            _logger.LogInformation("Sync carte set Pokémon {SetId}: {Created} create, {Updated} aggiornate",
                setId, created, updated);

            return Ok(new
            {
                message = $"Sincronizzazione carte del set '{setId}' completata",
                setId,
                created,
                updated
            });
        }

        /// <summary>
        /// Aggiorna i prezzi di mercato per le carte Pokémon dell'utente corrente.
        /// </summary>
        [HttpPost("refresh-prices")]
        [Authorize]
        public async Task<IActionResult> RefreshPrices()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            // Trova CardInfo Pokémon delle carte dell'utente con prezzi obsoleti
            var staleBefore = DateTime.UtcNow.AddHours(-PriceStalenessHours);

            var cardInfos = await _context.Cards
                .Where(c => c.UserId == userId)
                .Select(c => c.CardInfo)
                .Where(ci => ci.PokemonTcgId != null
                           && (ci.PokemonTcgUpdatedAt == null || ci.PokemonTcgUpdatedAt < staleBefore))
                .Distinct()
                .ToListAsync();

            if (cardInfos.Count == 0)
                return Ok(new { message = "Nessuna carta Pokémon da aggiornare (prezzi ancora freschi)", updated = 0 });

            int totalUpdated = 0;

            // Aggiorna una alla volta (API non ha endpoint batch)
            foreach (var cardInfo in cardInfos)
            {
                var apiCard = await _pokemonTcg.GetCardByIdAsync(cardInfo.PokemonTcgId!);
                if (apiCard == null) continue;

                // Aggiorna solo prezzi e immagini
                if (apiCard.TcgPlayer?.Prices != null)
                {
                    cardInfo.PriceTcgNormal = apiCard.TcgPlayer.Prices.Normal?.Market;
                    cardInfo.PriceTcgHolofoil = apiCard.TcgPlayer.Prices.Holofoil?.Market;
                    cardInfo.PriceTcgReverseHolofoil = apiCard.TcgPlayer.Prices.ReverseHolofoil?.Market;
                    cardInfo.PriceUsd = apiCard.TcgPlayer.Prices.Normal?.Market
                        ?? apiCard.TcgPlayer.Prices.Holofoil?.Market
                        ?? apiCard.TcgPlayer.Prices.ReverseHolofoil?.Market;
                }

                if (apiCard.Cardmarket?.Prices != null)
                {
                    cardInfo.PriceCardmarketAvg = apiCard.Cardmarket.Prices.AverageSellPrice;
                    cardInfo.PriceCardmarketTrend = apiCard.Cardmarket.Prices.TrendPrice;
                    cardInfo.PriceEur = apiCard.Cardmarket.Prices.AverageSellPrice
                        ?? apiCard.Cardmarket.Prices.TrendPrice;
                }

                if (apiCard.Images != null)
                {
                    cardInfo.PokemonImageSmall = apiCard.Images.Small;
                    cardInfo.PokemonImageLarge = apiCard.Images.Large;
                }

                cardInfo.PokemonTcgUpdatedAt = DateTime.UtcNow;
                totalUpdated++;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Prezzi Pokémon aggiornati per {Count} carte dell'utente {UserId}", totalUpdated, userId);

            return Ok(new
            {
                message = $"Prezzi aggiornati per {totalUpdated} carte Pokémon",
                updated = totalUpdated,
                total = cardInfos.Count
            });
        }

        // ================================================================
        // Set list — dal DB se popolato
        // ================================================================

        /// <summary>
        /// Lista set Pokémon TCG. Ritorna dal DB se sincronizzati, altrimenti dall'API.
        /// </summary>
        [HttpGet("sets")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSets()
        {
            var pokemonGameId = await GetPokemonGameIdAsync();
            if (pokemonGameId.HasValue)
            {
                var dbSets = await _context.CardSets
                    .AsNoTracking()
                    .Where(cs => cs.GameId == pokemonGameId.Value && cs.PokemonTcgId != null)
                    .OrderByDescending(cs => cs.ReleaseDate)
                    .Select(cs => new
                    {
                        id = cs.PokemonTcgId,
                        name = cs.Name,
                        code = cs.Code,
                        series = cs.Series,
                        cardCount = cs.CardCount,
                        printedTotal = cs.PrintedTotal,
                        releaseDate = cs.ReleaseDate,
                        logoUrl = cs.PokemonLogoUrl,
                        symbolUrl = cs.PokemonSymbolUrl
                    })
                    .ToListAsync();

                if (dbSets.Count > 0)
                    return Ok(new { source = "db", totalSets = dbSets.Count, sets = dbSets });
            }

            // Fallback: API
            var apiSets = await _pokemonTcg.GetAllSetsAsync();
            return Ok(new
            {
                source = "api",
                totalSets = apiSets.Count,
                sets = apiSets.Select(s => new
                {
                    id = s.Id,
                    name = s.Name,
                    series = s.Series,
                    cardCount = s.Total,
                    printedTotal = s.PrintedTotal,
                    releaseDate = s.ReleaseDate,
                    logoUrl = s.Images?.Logo,
                    symbolUrl = s.Images?.Symbol
                })
            });
        }

        // ================================================================
        // Helpers
        // ================================================================

        private async Task<int?> GetPokemonGameIdAsync()
        {
            var game = await _context.Games
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Name == "Pokémon TCG");
            return game?.Id;
        }

        private async Task<Game> EnsurePokemonGameAsync()
        {
            var game = await _context.Games.FirstOrDefaultAsync(g => g.Name == "Pokémon TCG");
            if (game != null) return game;

            game = new Game
            {
                Name = "Pokémon TCG",
                Publisher = "The Pokémon Company",
                Description = "Il gioco di carte collezionabili dei Pokémon, il TCG più venduto al mondo"
            };
            _context.Games.Add(game);
            await _context.SaveChangesAsync();
            return game;
        }

        private async Task<CardSet> EnsureCardSetAsync(PokemonTcgCard apiCard, int gameId)
        {
            if (apiCard.Set == null)
                throw new ArgumentException("La carta non ha informazioni sul set");

            var setId = apiCard.Set.Id;
            var cardSet = await _context.CardSets
                .FirstOrDefaultAsync(cs => cs.PokemonTcgId == setId && cs.GameId == gameId);

            if (cardSet != null)
            {
                _pokemonTcg.MapToCardSet(apiCard.Set, cardSet);
                await _context.SaveChangesAsync();
                return cardSet;
            }

            cardSet = new CardSet { GameId = gameId };
            _pokemonTcg.MapToCardSet(apiCard.Set, cardSet);
            _context.CardSets.Add(cardSet);
            await _context.SaveChangesAsync();
            return cardSet;
        }

        private async Task<CardSet?> EnsureCardSetFromApiAsync(string setId, int gameId)
        {
            var cardSet = await _context.CardSets
                .FirstOrDefaultAsync(cs => cs.PokemonTcgId == setId && cs.GameId == gameId);

            if (cardSet != null) return cardSet;

            var apiSet = await _pokemonTcg.GetSetByIdAsync(setId);
            if (apiSet == null) return null;

            cardSet = new CardSet { GameId = gameId };
            _pokemonTcg.MapToCardSet(apiSet, cardSet);
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
                pokemonTcgId = ci.PokemonTcgId,
                name = ci.Name,
                cardNumber = ci.CardNumber,
                rarity = ci.Rarity,
                supertype = ci.Supertype,
                subtypes = ci.Subtypes?.Split(',', StringSplitOptions.RemoveEmptyEntries),
                hp = ci.Hp,
                types = ci.PokemonTypes?.Split(',', StringSplitOptions.RemoveEmptyEntries),
                evolvesFrom = ci.EvolvesFrom,
                artist = ci.Artist,
                images = new
                {
                    small = ci.PokemonImageSmall,
                    large = ci.PokemonImageLarge
                },
                prices = new
                {
                    tcgNormal = ci.PriceTcgNormal,
                    tcgHolofoil = ci.PriceTcgHolofoil,
                    tcgReverseHolofoil = ci.PriceTcgReverseHolofoil,
                    cardmarketAvg = ci.PriceCardmarketAvg,
                    cardmarketTrend = ci.PriceCardmarketTrend,
                    usd = ci.PriceUsd,
                    eur = ci.PriceEur
                },
                setName = ci.CardSet?.Name,
                lastUpdated = ci.PokemonTcgUpdatedAt
            };
        }

        private static object MapApiCardToResponse(PokemonTcgCard card)
        {
            return new
            {
                pokemonTcgId = card.Id,
                name = card.Name,
                cardNumber = card.Number,
                rarity = card.Rarity,
                supertype = card.Supertype,
                subtypes = card.Subtypes,
                hp = card.Hp,
                types = card.Types,
                evolvesFrom = card.EvolvesFrom,
                artist = card.Artist,
                images = new
                {
                    small = card.Images?.Small,
                    large = card.Images?.Large
                },
                prices = new
                {
                    tcgNormal = card.TcgPlayer?.Prices?.Normal?.Market,
                    tcgHolofoil = card.TcgPlayer?.Prices?.Holofoil?.Market,
                    tcgReverseHolofoil = card.TcgPlayer?.Prices?.ReverseHolofoil?.Market,
                    cardmarketAvg = card.Cardmarket?.Prices?.AverageSellPrice,
                    cardmarketTrend = card.Cardmarket?.Prices?.TrendPrice
                },
                setName = card.Set?.Name,
                setId = card.Set?.Id
            };
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
        }
    }

    // ================================================================
    // Request DTOs
    // ================================================================

    public class PokemonImportRequest
    {
        public string PokemonTcgId { get; set; } = string.Empty;
    }
}
