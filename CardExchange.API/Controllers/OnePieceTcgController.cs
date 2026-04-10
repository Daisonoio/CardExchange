using CardExchange.API.Authorization;
using CardExchange.API.DTOs.OnePieceTcg;
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
    /// Integrazione con ApiTCG per One Piece TCG — DB-first con cache aggressiva
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class OnePieceTcgController : ControllerBase
    {
        private readonly IOnePieceTcgService _onePiece;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OnePieceTcgController> _logger;

        private const int StalenessHours = 24;

        public OnePieceTcgController(
            IOnePieceTcgService onePiece,
            ApplicationDbContext context,
            ILogger<OnePieceTcgController> logger)
        {
            _onePiece = onePiece;
            _context = context;
            _logger = logger;
        }

        // ================================================================
        // Ricerca — prima DB, fallback API
        // ================================================================

        /// <summary>
        /// Ricerca carte One Piece TCG. Cerca prima nel DB locale, poi nell'API ApiTCG.
        /// </summary>
        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<IActionResult> SearchCards([FromQuery] string q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return BadRequest(new { message = "Il termine di ricerca deve essere almeno 2 caratteri" });

            pageSize = Math.Clamp(pageSize, 1, 50);

            // 1. Cerca nel DB locale
            var onePieceGameId = await GetOnePieceGameIdAsync();
            if (onePieceGameId.HasValue)
            {
                var dbQuery = _context.CardInfos
                    .AsNoTracking()
                    .Where(ci => ci.CardSet.GameId == onePieceGameId.Value
                              && ci.OnePieceTcgId != null
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

            // 2. Fallback: chiama API ApiTCG
            var result = await _onePiece.SearchCardsAsync(q, page);
            if (result == null)
                return Ok(new { source = "api", query = q, totalCount = 0, cards = Array.Empty<object>() });

            return Ok(new
            {
                source = "api",
                query = q,
                page = result.Page,
                pageSize = result.Limit,
                totalCount = result.Total,
                totalPages = result.TotalPages,
                cards = result.Data.Select(MapApiCardToResponse)
            });
        }

        /// <summary>
        /// Ottieni carta per codice One Piece TCG (es. "OP06-014"). Serve da DB se presente, altrimenti API.
        /// </summary>
        [HttpGet("cards/{code}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCard(string code)
        {
            // 1. Cerca in DB
            var cardInfo = await _context.CardInfos
                .AsNoTracking()
                .Include(ci => ci.CardSet)
                .FirstOrDefaultAsync(ci => ci.OnePieceTcgId == code);

            if (cardInfo != null && !IsStale(cardInfo.OnePieceTcgUpdatedAt))
                return Ok(new { source = "db", card = MapToResponse(cardInfo) });

            // 2. Chiama API
            var apiCard = await _onePiece.GetCardByCodeAsync(code);
            if (apiCard == null)
            {
                if (cardInfo != null)
                    return Ok(new { source = "db_stale", card = MapToResponse(cardInfo) });

                return NotFound(new { message = $"Carta '{code}' non trovata" });
            }

            return Ok(new { source = "api", card = MapApiCardToResponse(apiCard) });
        }

        // ================================================================
        // Import — salva carta nel DB locale
        // ================================================================

        /// <summary>
        /// Importa una carta One Piece nel DB locale dall'API. Crea/aggiorna CardInfo + CardSet.
        /// </summary>
        [HttpPost("import")]
        [Authorize]
        public async Task<IActionResult> ImportCard([FromBody] OnePieceImportRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
                return BadRequest(new { message = "Il codice carta è obbligatorio (es. 'OP06-014')" });

            // Verifica se esiste già nel DB
            var existingCardInfo = await _context.CardInfos
                .FirstOrDefaultAsync(ci => ci.OnePieceTcgId == request.Code);

            if (existingCardInfo != null && !IsStale(existingCardInfo.OnePieceTcgUpdatedAt))
            {
                return Ok(new
                {
                    message = "Carta già presente nel DB",
                    cardInfoId = existingCardInfo.Id,
                    isNew = false
                });
            }

            // Chiama API
            var apiCard = await _onePiece.GetCardByCodeAsync(request.Code);
            if (apiCard == null)
                return NotFound(new { message = $"Carta '{request.Code}' non trovata nell'API One Piece TCG" });

            // Trova o crea il Game One Piece TCG
            var onePieceGame = await EnsureOnePieceGameAsync();

            // Trova o crea il CardSet
            var cardSet = await EnsureCardSetAsync(apiCard, onePieceGame.Id);

            if (existingCardInfo != null)
            {
                _onePiece.MapToCardInfo(apiCard, existingCardInfo);
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
            _onePiece.MapToCardInfo(apiCard, cardInfo);
            _context.CardInfos.Add(cardInfo);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Carta One Piece importata: {Name} ({Code}) -> CardInfo ID {Id}",
                apiCard.Name, apiCard.Code, cardInfo.Id);

            return CreatedAtAction(nameof(GetCard), new { code = request.Code }, new
            {
                message = "Carta importata con successo",
                cardInfoId = cardInfo.Id,
                isNew = true
            });
        }

        // ================================================================
        // Helpers
        // ================================================================

        private async Task<int?> GetOnePieceGameIdAsync()
        {
            var game = await _context.Games
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Name == "One Piece TCG");
            return game?.Id;
        }

        private async Task<Game> EnsureOnePieceGameAsync()
        {
            var game = await _context.Games.FirstOrDefaultAsync(g => g.Name == "One Piece TCG");
            if (game != null) return game;

            game = new Game
            {
                Name = "One Piece TCG",
                Publisher = "Bandai",
                Description = "Il gioco di carte collezionabili basato sul manga e anime One Piece"
            };
            _context.Games.Add(game);
            await _context.SaveChangesAsync();
            return game;
        }

        private async Task<CardSet> EnsureCardSetAsync(OnePieceCard apiCard, int gameId)
        {
            // Estrai il prefisso del set dal codice carta (es. "OP06" da "OP06-014")
            var setCode = ExtractSetCode(apiCard.Code);
            var setName = apiCard.Set?.Name ?? setCode;

            var cardSet = await _context.CardSets
                .FirstOrDefaultAsync(cs => cs.Code == setCode && cs.GameId == gameId);

            if (cardSet != null)
            {
                if (apiCard.Set?.Name != null)
                {
                    cardSet.OnePieceTcgSetName = apiCard.Set.Name;
                    cardSet.OnePieceTcgUpdatedAt = DateTime.UtcNow;
                }
                return cardSet;
            }

            cardSet = new CardSet
            {
                GameId = gameId,
                Name = setName,
                Code = setCode,
                OnePieceTcgSetName = apiCard.Set?.Name,
                OnePieceTcgUpdatedAt = DateTime.UtcNow
            };
            _context.CardSets.Add(cardSet);
            await _context.SaveChangesAsync();
            return cardSet;
        }

        private static string ExtractSetCode(string cardCode)
        {
            // "OP06-014" -> "OP06", "ST01-001" -> "ST01", "P-001" -> "P"
            var dashIndex = cardCode.IndexOf('-');
            return dashIndex > 0 ? cardCode[..dashIndex] : cardCode;
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
                code = ci.OnePieceTcgId,
                name = ci.Name,
                type = ci.Type,
                rarity = ci.Rarity,
                color = ci.OnePieceColor,
                cost = ci.OnePieceCost,
                power = ci.OnePiecePower,
                counter = ci.OnePieceCounter,
                family = ci.OnePieceFamily,
                ability = ci.OnePieceAbility,
                trigger = ci.OnePieceTrigger,
                images = new
                {
                    small = ci.OnePieceImageSmall,
                    large = ci.OnePieceImageLarge
                },
                setName = ci.CardSet?.Name,
                lastUpdated = ci.OnePieceTcgUpdatedAt
            };
        }

        private static object MapApiCardToResponse(OnePieceCard card)
        {
            return new
            {
                code = card.Code,
                name = card.Name,
                type = card.Type,
                rarity = card.Rarity,
                color = card.Color,
                cost = card.Cost,
                power = card.Power,
                counter = card.Counter,
                family = card.Family,
                ability = card.Ability,
                trigger = card.Trigger,
                attribute = card.Attribute?.Name,
                images = new
                {
                    small = card.Images?.Small,
                    large = card.Images?.Large
                },
                setName = card.Set?.Name
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

    public class OnePieceImportRequest
    {
        public string Code { get; set; } = string.Empty;
    }
}
