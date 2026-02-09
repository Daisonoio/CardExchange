using CardExchange.API.Authorization;
using CardExchange.API.DTOs.Requests;
using CardExchange.API.DTOs.Responses;
using CardExchange.Core.Entities;
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
        /// Ricerca avanzata con filtri premium (condizione, prezzo, geo)
        /// </summary>
        [HttpPost("advanced")]
        [RequirePremium]
        public async Task<IActionResult> AdvancedSearch([FromBody] AdvancedSearchRequest request)
        {
            request.PageSize = Math.Min(request.PageSize, 100);

            var query = _context.Cards
                .AsNoTracking()
                .Include(c => c.CardInfo)
                    .ThenInclude(ci => ci.CardSet)
                        .ThenInclude(cs => cs.Game)
                .Include(c => c.User)
                    .ThenInclude(u => u.Location)
                .AsQueryable();

            if (request.OnlyAvailableForTrade)
                query = query.Where(c => c.IsAvailableForTrade);

            // Text search
            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.ToLower();
                query = query.Where(c =>
                    c.CardInfo.Name.ToLower().Contains(term) ||
                    c.CardInfo.CardSet.Name.ToLower().Contains(term) ||
                    c.CardInfo.CardSet.Game.Name.ToLower().Contains(term));
            }

            // Filters
            if (request.GameId.HasValue)
                query = query.Where(c => c.CardInfo.CardSet.GameId == request.GameId.Value);

            if (request.CardSetId.HasValue)
                query = query.Where(c => c.CardInfo.CardSetId == request.CardSetId.Value);

            if (!string.IsNullOrWhiteSpace(request.Rarity))
                query = query.Where(c => c.CardInfo.Rarity == request.Rarity);

            if (!string.IsNullOrWhiteSpace(request.Type))
                query = query.Where(c => c.CardInfo.Type == request.Type);

            // Condition filter
            if (request.MinCondition.HasValue)
                query = query.Where(c => (int)c.Condition >= request.MinCondition.Value);

            if (request.MaxCondition.HasValue)
                query = query.Where(c => (int)c.Condition <= request.MaxCondition.Value);

            // Price filter
            if (request.MinPrice.HasValue)
                query = query.Where(c => c.EstimatedValue >= request.MinPrice.Value);

            if (request.MaxPrice.HasValue)
                query = query.Where(c => c.EstimatedValue <= request.MaxPrice.Value);

            // Location filter
            if (!string.IsNullOrWhiteSpace(request.City))
                query = query.Where(c => c.User.Location != null && c.User.Location.City.ToLower() == request.City.ToLower());

            if (!string.IsNullOrWhiteSpace(request.Country))
                query = query.Where(c => c.User.Location != null && c.User.Location.Country.ToLower() == request.Country.ToLower());

            // Geo-radius search
            if (request.Latitude.HasValue && request.Longitude.HasValue && request.RadiusKm.HasValue)
            {
                var lat = request.Latitude.Value;
                var lng = request.Longitude.Value;
                var latRange = request.RadiusKm.Value / 111.0m;
                var lngRange = request.RadiusKm.Value / (111.0m * (decimal)Math.Cos((double)lat * Math.PI / 180));

                query = query.Where(c => c.User.Location != null &&
                    c.User.Location.Latitude != null && c.User.Location.Longitude != null &&
                    c.User.Location.Latitude >= lat - latRange &&
                    c.User.Location.Latitude <= lat + latRange &&
                    c.User.Location.Longitude >= lng - lngRange &&
                    c.User.Location.Longitude <= lng + lngRange);
            }

            var totalCount = await query.CountAsync();
            var cards = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            return Ok(new
            {
                totalCount,
                page = request.Page,
                pageSize = request.PageSize,
                cards = cards.Select(c => new
                {
                    c.Id,
                    cardName = c.CardInfo.Name,
                    cardNumber = c.CardInfo.CardNumber,
                    cardSet = c.CardInfo.CardSet.Name,
                    cardSetCode = c.CardInfo.CardSet.Code,
                    game = c.CardInfo.CardSet.Game.Name,
                    rarity = c.CardInfo.Rarity,
                    type = c.CardInfo.Type,
                    condition = c.Condition.ToString(),
                    c.Quantity,
                    c.EstimatedValue,
                    c.Notes,
                    imageUrl = c.CardInfo.ImageUrl,
                    owner = new
                    {
                        c.User.Id,
                        c.User.Username,
                        c.User.ReputationScore,
                        c.User.TotalTradesCompleted
                    },
                    location = c.User.Location != null
                        ? new { c.User.Location.City, c.User.Location.Province, c.User.Location.Country }
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
