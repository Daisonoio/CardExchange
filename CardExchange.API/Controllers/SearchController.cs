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
            [FromServices] CardExchange.API.Services.Moderation.IUserBlockService blocks)
        {
            const int CandidateLimit = 3000;
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();
            request.Page = Math.Max(1, request.Page);
            request.PageSize = Math.Clamp(request.PageSize, 1, 100);

            double? lat = request.Latitude.HasValue ? (double)request.Latitude.Value : null;
            double? lng = request.Longitude.HasValue ? (double)request.Longitude.Value : null;
            if (lat == null || lng == null)
            {
                var loc = await _context.UserLocations.AsNoTracking().FirstOrDefaultAsync(l => l.UserId == userId);
                if (loc?.Latitude != null && loc.Longitude != null)
                {
                    lat = (double)loc.Latitude.Value;
                    lng = (double)loc.Longitude.Value;
                }
            }

            var blocked = await blocks.GetBlockedUserIdsAsync(userId);
            var query = _context.Cards
                .AsNoTracking()
                .Include(c => c.CardInfo).ThenInclude(ci => ci.CardSet).ThenInclude(cs => cs.Game)
                .Include(c => c.User).ThenInclude(u => u.Location)
                .Where(c => c.UserId != userId && c.User.IsActive && !blocked.Contains(c.UserId));

            if (request.OnlyAvailableForTrade)
                query = query.Where(c => c.IsAvailableForTrade);
            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.Trim().ToLower();
                query = query.Where(c => c.CardInfo.Name.ToLower().Contains(term) || c.CardInfo.CardSet.Name.ToLower().Contains(term));
            }
            if (request.GameId.HasValue)
                query = query.Where(c => c.CardInfo.CardSet.GameId == request.GameId.Value);
            if (request.CardSetId.HasValue)
                query = query.Where(c => c.CardInfo.CardSetId == request.CardSetId.Value);
            if (!string.IsNullOrWhiteSpace(request.Rarity))
                query = query.Where(c => c.CardInfo.Rarity == request.Rarity);
            if (!string.IsNullOrWhiteSpace(request.Type))
                query = query.Where(c => c.CardInfo.Type == request.Type);
            if (!string.IsNullOrWhiteSpace(request.Language))
                query = query.Where(c => c.Language == request.Language);
            if (request.Finish.HasValue)
                query = query.Where(c => c.Finish == request.Finish.Value);
            // Condizioni: 1 = Mint ... 8 = Damaged. MaxCondition = condizione peggiore accettata
            if (request.MinCondition.HasValue)
                query = query.Where(c => (int)c.Condition >= request.MinCondition.Value);
            if (request.MaxCondition.HasValue)
                query = query.Where(c => (int)c.Condition <= request.MaxCondition.Value);
            if (!string.IsNullOrWhiteSpace(request.City))
                query = query.Where(c => c.User.Location != null && c.User.Location.City.ToLower() == request.City.Trim().ToLower());
            if (!string.IsNullOrWhiteSpace(request.Country))
                query = query.Where(c => c.User.Location != null && c.User.Location.Country.ToLower() == request.Country.Trim().ToLower());

            var radius = request.RadiusKm;
            if (radius.HasValue && lat.HasValue && lng.HasValue)
            {
                // Prefiltro rettangolare nel database, distanza esatta dopo
                var latRange = (decimal)(radius.Value / 111.0);
                var lngRange = (decimal)(radius.Value / (111.0 * Math.Max(0.01, Math.Cos(lat.Value * Math.PI / 180))));
                var la = (decimal)lat.Value;
                var lo = (decimal)lng.Value;
                query = query.Where(c => c.User.Location != null && c.User.Location.Latitude != null && c.User.Location.Longitude != null
                    && c.User.Location.Latitude >= la - latRange && c.User.Location.Latitude <= la + latRange
                    && c.User.Location.Longitude >= lo - lngRange && c.User.Location.Longitude <= lo + lngRange);
            }

            var candidates = await query.OrderByDescending(c => c.CreatedAt).Take(CandidateLimit).ToListAsync();

            var rows = candidates
                .Select(c =>
                {
                    var userLoc = c.User.Location;
                    double? distance = lat.HasValue && lng.HasValue && userLoc?.Latitude != null && userLoc.Longitude != null
                        ? Math.Round(Geo.DistanceKm(lat.Value, lng.Value, (double)userLoc.Latitude.Value, (double)userLoc.Longitude.Value), 1)
                        : null;
                    return new { card = c, price = CardPricing.EffectivePriceEur(c), market = CardPricing.MarketValueEur(c), distance };
                })
                // Senza posizione il raggio non si può applicare: il client lo segnala (hasLocation = false)
                .Where(x => !radius.HasValue || !lat.HasValue || (x.distance.HasValue && x.distance.Value <= radius.Value))
                .Where(x => !request.MinPrice.HasValue || (x.price.HasValue && x.price.Value >= request.MinPrice.Value))
                .Where(x => !request.MaxPrice.HasValue || (x.price.HasValue && x.price.Value <= request.MaxPrice.Value))
                .ToList();

            var sort = (request.SortBy ?? (lat.HasValue ? "distance" : "newest")).ToLowerInvariant();
            rows = sort switch
            {
                "distance" => rows.OrderBy(x => x.distance ?? double.MaxValue).ThenBy(x => x.price ?? decimal.MaxValue).ToList(),
                "price" => rows.OrderBy(x => x.price ?? decimal.MaxValue).ThenBy(x => x.distance ?? double.MaxValue).ToList(),
                _ => rows.OrderByDescending(x => x.card.CreatedAt).ToList()
            };

            var ownerIds = rows.Select(r => r.card.UserId).Distinct().ToList();
            var ownerPlans = await _subscriptionService.GetActivePaidPlansAsync(ownerIds);

            return Ok(new
            {
                totalCount = rows.Count,
                truncated = candidates.Count == CandidateLimit,
                page = request.Page,
                pageSize = request.PageSize,
                hasLocation = lat.HasValue,
                sortBy = sort,
                cards = rows
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .Select(x => new
                    {
                        id = x.card.Id,
                        cardInfoId = x.card.CardInfoId,
                        cardName = x.card.CardInfo.Name,
                        cardNumber = x.card.CardInfo.CardNumber,
                        cardSet = x.card.CardInfo.CardSet.Name,
                        cardSetCode = x.card.CardInfo.CardSet.Code,
                        game = x.card.CardInfo.CardSet.Game.Name,
                        rarity = x.card.CardInfo.Rarity,
                        condition = x.card.Condition.ToString(),
                        language = x.card.Language,
                        finish = x.card.Finish.ToString(),
                        quantity = x.card.Quantity,
                        price = x.price,
                        marketValue = x.market,
                        askingPrice = x.card.AskingPrice,
                        imageUrl = x.card.CardInfo.ImageSmall ?? x.card.CardInfo.ImageUrl,
                        distanceKm = x.distance,
                        owner = new
                        {
                            id = x.card.User.Id,
                            username = x.card.User.Username,
                            reputationScore = x.card.User.ReputationScore,
                            totalTradesCompleted = x.card.User.TotalTradesCompleted,
                            planBadge = ownerPlans.TryGetValue(x.card.UserId, out var plan) && plan.HasVerifiedBadge ? plan.Tier.ToString() : null
                        },
                        location = x.card.User.Location != null
                            ? new { x.card.User.Location.City, x.card.User.Location.Province }
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
