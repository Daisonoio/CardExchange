using CardExchange.API.Authorization;
using CardExchange.API.DTOs.Requests;
using CardExchange.API.DTOs.Responses;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace CardExchange.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SearchController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<SearchController> _logger;

        public SearchController(
            ApplicationDbContext context,
            ISubscriptionService subscriptionService,
            ILogger<SearchController> logger)
        {
            _context = context;
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        /// <summary>
        /// Ricerca base carte disponibili (FREE)
        /// </summary>
        [HttpGet("cards")]
        [AllowAnonymous]
        public async Task<IActionResult> SearchCards(
            [FromQuery] string? q,
            [FromQuery] int? gameId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            pageSize = Math.Min(pageSize, 50);

            var query = _context.Cards
                .AsNoTracking()
                .Include(c => c.CardInfo)
                    .ThenInclude(ci => ci.CardSet)
                        .ThenInclude(cs => cs.Game)
                .Include(c => c.User)
                    .ThenInclude(u => u.Location)
                .Where(c => c.IsAvailableForTrade);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.ToLower();
                query = query.Where(c =>
                    c.CardInfo.Name.ToLower().Contains(term) ||
                    c.CardInfo.CardSet.Name.ToLower().Contains(term) ||
                    c.CardInfo.CardSet.Game.Name.ToLower().Contains(term));
            }

            if (gameId.HasValue)
            {
                query = query.Where(c => c.CardInfo.CardSet.GameId == gameId.Value);
            }

            var totalCount = await query.CountAsync();
            var cards = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                cards = cards.Select(c => new
                {
                    c.Id,
                    cardName = c.CardInfo.Name,
                    cardSet = c.CardInfo.CardSet.Name,
                    game = c.CardInfo.CardSet.Game.Name,
                    rarity = c.CardInfo.Rarity,
                    condition = c.Condition.ToString(),
                    c.Quantity,
                    c.EstimatedValue,
                    owner = new { c.User.Id, c.User.Username },
                    location = c.User.Location != null
                        ? new { c.User.Location.City, c.User.Location.Country }
                        : null
                })
            });
        }

        /// <summary>
        /// Ricerca avanzata (Premium): carte in scambio di altri utenti filtrate per gioco, rarità, lingua, finitura,
        /// condizione minima, prezzo (quello indicato dal proprietario o, se manca, il valore di mercato) e distanza.
        /// Senza coordinate usa la posizione del profilo. Esclude le proprie carte, gli utenti bloccati e i sospesi.
        /// </summary>
        [HttpPost("advanced")]
        [RequirePremium]
        public async Task<IActionResult> AdvancedSearch([FromBody] AdvancedSearchRequest request,
            [FromServices] CardExchange.API.Services.CardSearchService search)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();
            var result = await search.SearchAsync(userId, request);
            var rows = result.Hits;

            var ownerIds = rows.Select(r => r.Card.UserId).Distinct().ToList();
            var ownerPlans = await _subscriptionService.GetActivePaidPlansAsync(ownerIds);

            return Ok(new
            {
                totalCount = rows.Count,
                truncated = result.Truncated,
                page = request.Page,
                pageSize = request.PageSize,
                hasLocation = result.HasLocation,
                sortBy = result.SortBy,
                cards = rows
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .Select(x => new
                    {
                        id = x.Card.Id,
                        cardInfoId = x.Card.CardInfoId,
                        cardName = x.Card.CardInfo.Name,
                        cardNumber = x.Card.CardInfo.CardNumber,
                        cardSet = x.Card.CardInfo.CardSet.Name,
                        cardSetCode = x.Card.CardInfo.CardSet.Code,
                        game = x.Card.CardInfo.CardSet.Game.Name,
                        rarity = x.Card.CardInfo.Rarity,
                        condition = x.Card.Condition.ToString(),
                        language = x.Card.Language,
                        finish = x.Card.Finish.ToString(),
                        quantity = x.Card.Quantity,
                        price = x.Price,
                        marketValue = x.Market,
                        askingPrice = x.Card.AskingPrice,
                        imageUrl = x.Card.CardInfo.ImageSmall ?? x.Card.CardInfo.ImageUrl,
                        distanceKm = x.Distance,
                        owner = new
                        {
                            id = x.Card.User.Id,
                            username = x.Card.User.Username,
                            reputationScore = x.Card.User.ReputationScore,
                            totalTradesCompleted = x.Card.User.TotalTradesCompleted,
                            planBadge = ownerPlans.TryGetValue(x.Card.UserId, out var plan) && plan.HasVerifiedBadge ? plan.Tier.ToString() : null
                        },
                        location = x.Card.User.Location != null
                            ? new { x.Card.User.Location.City, x.Card.User.Location.Province }
                            : null
                    })
            });
        }

        /// <summary>
        /// Cross-matching: trova carte che matchano la tua wishlist offerte da chi cerca le tue (PREMIUM)
        /// </summary>
        [HttpGet("matches")]
        [RequirePremium]
        public async Task<IActionResult> FindMatches()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            // Le carte che IO cerco
            var myWishlist = await _context.WishlistItems
                .AsNoTracking()
                .Where(w => w.UserId == userId)
                .Select(w => w.CardInfoId)
                .ToListAsync();

            // Le carte che IO offro
            var myAvailableCards = await _context.Cards
                .AsNoTracking()
                .Where(c => c.UserId == userId && c.IsAvailableForTrade)
                .Select(c => c.CardInfoId)
                .ToListAsync();

            // Utenti che HANNO carte che io cerco E cercano carte che io ho
            var matches = await _context.Cards
                .AsNoTracking()
                .Include(c => c.CardInfo)
                .Include(c => c.User)
                    .ThenInclude(u => u.Location)
                .Where(c => c.IsAvailableForTrade
                    && c.UserId != userId
                    && myWishlist.Contains(c.CardInfoId))
                .ToListAsync();

            // Filtra per quelli che a loro volta cercano le mie carte
            var matchedUserIds = matches.Select(m => m.UserId).Distinct().ToList();
            var theirWishlists = await _context.WishlistItems
                .AsNoTracking()
                .Where(w => matchedUserIds.Contains(w.UserId) && myAvailableCards.Contains(w.CardInfoId))
                .ToListAsync();

            var crossMatches = matches
                .Where(m => theirWishlists.Any(tw => tw.UserId == m.UserId))
                .GroupBy(m => m.UserId)
                .Select(g => new
                {
                    user = new
                    {
                        g.First().User.Id,
                        g.First().User.Username,
                        g.First().User.ReputationScore,
                        location = g.First().User.Location != null
                            ? new { g.First().User.Location!.City, g.First().User.Location.Country }
                            : null
                    },
                    theyHaveThatIWant = g.Select(c => new
                    {
                        c.CardInfo.Name,
                        c.Condition,
                        c.EstimatedValue
                    }),
                    theyWantThatIHave = theirWishlists
                        .Where(tw => tw.UserId == g.Key)
                        .Count()
                })
                .OrderByDescending(m => m.theyWantThatIHave)
                .Take(20)
                .ToList();

            return Ok(new
            {
                totalMatches = crossMatches.Count,
                matches = crossMatches
            });
        }

        /// <summary>
        /// Salva una ricerca (PREMIUM)
        /// </summary>
        [HttpPost("saved")]
        [RequirePremium]
        public async Task<IActionResult> SaveSearch([FromBody] SaveSearchRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var savedSearch = new SavedSearch
            {
                UserId = userId,
                Name = request.Name,
                CriteriaJson = JsonSerializer.Serialize(request.Criteria),
                AlertEnabled = request.AlertEnabled
            };

            _context.SavedSearches.Add(savedSearch);
            await _context.SaveChangesAsync();

            return Ok(new { id = savedSearch.Id, message = "Ricerca salvata con successo" });
        }

        /// <summary>
        /// Lista ricerche salvate (PREMIUM)
        /// </summary>
        [HttpGet("saved")]
        [RequirePremium]
        public async Task<IActionResult> GetSavedSearches()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var searches = await _context.SavedSearches
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.AlertEnabled,
                    s.LastAlertSentAt,
                    createdAt = s.CreatedAt
                })
                .ToListAsync();

            return Ok(searches);
        }

        /// <summary>
        /// Elimina ricerca salvata
        /// </summary>
        [HttpDelete("saved/{id}")]
        [RequirePremium]
        public async Task<IActionResult> DeleteSavedSearch(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var search = await _context.SavedSearches
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (search == null) return NotFound();

            search.IsDeleted = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Ricerca eliminata" });
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
        }
    }
}
