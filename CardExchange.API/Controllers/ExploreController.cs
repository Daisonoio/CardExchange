using CardExchange.API.Services.Entitlements;
using CardExchange.API.Authorization;
using CardExchange.API.DTOs.Requests;
using CardExchange.API.Services;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Esplora: carte in scambio vicino a te, per la griglia, la mappa e la scheda carta.
    /// Privacy: distanze arrotondate al km e zone di circa 2 km, mai coordinate esatte degli altri utenti.
    /// Ricerca, gioco, raggio e ordinamento sono per tutti; prezzo, condizione, lingua e finitura sono Premium.
    /// </summary>
    [ApiController]
    [Route("api/explore")]
    [Authorize]
    public class ExploreController : ControllerBase
    {
        /// <summary>Lato della cella usata per la mappa, in gradi (circa 2 km).</summary>
        public const double AreaCellDegrees = 0.02;

        private readonly ApplicationDbContext _context;
        private readonly CardSearchService _search;
        private readonly ISubscriptionService _subscriptions;

        public ExploreController(ApplicationDbContext context, CardSearchService search, ISubscriptionService subscriptions)
        {
            _context = context;
            _search = search;
            _subscriptions = subscriptions;
        }

        /// <summary>Distanza mostrata: km interi, almeno 1 ("~1 km"), così non si può triangolare un indirizzo.</summary>
        public static int? ApproxKm(double? km) => km.HasValue ? Math.Max(1, (int)Math.Round(km.Value)) : null;

        /// <summary>Centro della cella in cui cade un punto: tutte le carte della stessa zona hanno la stessa posizione.</summary>
        public static (double Lat, double Lng) AreaOf(double lat, double lng) =>
            (Math.Round((Math.Floor(lat / AreaCellDegrees) + 0.5) * AreaCellDegrees, 4),
             Math.Round((Math.Floor(lng / AreaCellDegrees) + 0.5) * AreaCellDegrees, 4));

        [HttpGet("cards")]
        public async Task<IActionResult> Cards(
            [FromQuery] string? q, [FromQuery] int? gameId, [FromQuery] int? radiusKm, [FromQuery] string? sort,
            [FromQuery] decimal? minPrice, [FromQuery] decimal? maxPrice, [FromQuery] int? maxCondition,
            [FromQuery] string? language, [FromQuery] CardFinish? finish,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
        {
            var userId = CurrentUserId();
            if (userId == 0) return Unauthorized();

            var advanced = minPrice.HasValue || maxPrice.HasValue || maxCondition.HasValue || !string.IsNullOrWhiteSpace(language) || finish.HasValue;
            var isPremium = await this.Entitlements(_context).HasAsync(userId, Capability.AdvancedSearch);
            // Raggio massimo del piano (gratuito 30 km, Premium 200 km)
            var maxRadius = await this.Entitlements(_context).LimitAsync(userId, PlanLimit.RadiusKm);
            if (advanced && !isPremium)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "I filtri per prezzo, condizione, lingua e finitura sono Premium.", code = "PREMIUM_FILTERS" });

            var request = new AdvancedSearchRequest
            {
                SearchTerm = q, GameId = gameId, RadiusKm = Math.Min(Math.Clamp(radiusKm ?? 25, 1, 500), maxRadius), SortBy = sort,
                MinPrice = minPrice, MaxPrice = maxPrice, MaxCondition = maxCondition, Language = language, Finish = finish,
                Page = page, PageSize = Math.Clamp(pageSize, 1, 60)
            };
            var result = await _search.SearchAsync(userId, request);
            // Accesso anticipato: le carte appena caricate da negozi e banchi le vedono prima i Premium
            var hidden = await EarlyAccess.HiddenAsync(this.Entitlements(_context), userId,
                result.Hits.Select(h => (h.Card.Id, h.Card.UserId, h.Card.CreatedAt)), DateTime.UtcNow);
            if (hidden.Count > 0) result.Hits.RemoveAll(h => hidden.Contains(h.Card.Id));

            var pageHits = result.Hits.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
            var plans = await _subscriptions.GetActivePaidPlansAsync(pageHits.Select(h => h.Card.UserId).Distinct());
            var wanted = (await _context.WishlistItems.Where(w => w.UserId == userId).Select(w => w.CardInfoId).ToListAsync()).ToHashSet();

            // Zone per la mappa: su tutti i risultati (non solo la pagina), raggruppati per cella
            var areas = result.Hits
                .Where(h => h.Card.User.Location?.Latitude != null && h.Card.User.Location.Longitude != null)
                .GroupBy(h => AreaOf((double)h.Card.User.Location!.Latitude!.Value, (double)h.Card.User.Location.Longitude!.Value))
                .Select(g => new
                {
                    lat = g.Key.Lat,
                    lng = g.Key.Lng,
                    count = g.Count(),
                    approxKm = ApproxKm(g.Min(h => h.Distance)),
                    preview = g.OrderBy(h => h.Distance ?? double.MaxValue).Take(3).Select(h => new
                    {
                        id = h.Card.Id,
                        name = h.Card.CardInfo.Name,
                        imageUrl = h.Card.CardInfo.ImageSmall ?? h.Card.CardInfo.ImageUrl,
                        price = h.Price
                    })
                })
                .ToList();

            (double Lat, double Lng)? origin = result.OriginLat.HasValue && result.OriginLng.HasValue
                ? (Math.Round(result.OriginLat.Value, 2), Math.Round(result.OriginLng.Value, 2))
                : null;

            return Ok(new
            {
                total = result.Hits.Count,
                page = request.Page,
                pageSize = request.PageSize,
                hasMore = request.Page * request.PageSize < result.Hits.Count,
                hasLocation = result.HasLocation,
                isPremium,
                radiusKm = request.RadiusKm,
                origin = origin.HasValue ? new { lat = origin.Value.Lat, lng = origin.Value.Lng } : null,
                areas,
                cards = pageHits.Select(h => new
                {
                    id = h.Card.Id,
                    cardInfoId = h.Card.CardInfoId,
                    name = h.Card.CardInfo.Name,
                    setName = h.Card.CardInfo.CardSet?.Name,
                    number = h.Card.CardInfo.CardNumber,
                    rarity = h.Card.CardInfo.Rarity,
                    game = h.Card.CardInfo.CardSet?.Game?.Name,
                    imageUrl = h.Card.CardInfo.ImageNormal ?? h.Card.CardInfo.ImageSmall ?? h.Card.CardInfo.ImageUrl,
                    condition = h.Card.Condition.ToString(),
                    finish = h.Card.Finish.ToString(),
                    language = h.Card.Language,
                    price = h.Price,
                    priceKind = h.Card.AskingPrice.HasValue ? "asking" : "market",
                    approxKm = ApproxKm(h.Distance),
                    isWanted = wanted.Contains(h.Card.CardInfoId),
                    owner = new
                    {
                        id = h.Card.User.Id,
                        username = h.Card.User.Username,
                        reputationScore = h.Card.User.ReputationScore,
                        planBadge = plans.TryGetValue(h.Card.UserId, out var plan) && plan.HasVerifiedBadge ? CardExchange.Core.Rules.UserEntitlements.BadgeFor(plan.Tier) : null
                    }
                })
            });
        }

        [HttpGet("cards/{id:int}")]
        public async Task<IActionResult> Card(int id)
        {
            var userId = CurrentUserId();
            if (userId == 0) return Unauthorized();

            var card = await _context.Cards.AsNoTracking()
                .Include(c => c.CardInfo).ThenInclude(ci => ci.CardSet).ThenInclude(cs => cs.Game)
                .Include(c => c.User).ThenInclude(u => u.Location)
                .FirstOrDefaultAsync(c => c.Id == id);
            var isOwn = card?.UserId == userId;
            if (card == null || !card.User.IsActive || (!isOwn && !card.IsAvailableForTrade))
                return NotFound(new { message = "Carta non disponibile" });
            var blocked = await HttpContext.RequestServices.GetRequiredService<CardExchange.API.Services.Moderation.IUserBlockService>().IsBlockedAsync(userId, card.UserId);
            if (blocked) return NotFound(new { message = "Carta non disponibile" });

            var me = await _context.UserLocations.AsNoTracking().FirstOrDefaultAsync(l => l.UserId == userId);
            double? distance = me?.Latitude != null && me.Longitude != null && card.User.Location?.Latitude != null && card.User.Location.Longitude != null
                ? Geo.DistanceKm((double)me.Latitude.Value, (double)me.Longitude.Value, (double)card.User.Location.Latitude.Value, (double)card.User.Location.Longitude.Value)
                : null;

            var myTradeable = await _context.Cards.Where(c => c.UserId == userId && c.IsAvailableForTrade).Select(c => c.CardInfoId).Distinct().ToListAsync();
            var ownerWantsFromMe = isOwn ? 0 : await _context.WishlistItems.CountAsync(w => w.UserId == card.UserId && myTradeable.Contains(w.CardInfoId));
            var isWanted = await _context.WishlistItems.AnyAsync(w => w.UserId == userId && w.CardInfoId == card.CardInfoId);
            var plans = await _subscriptions.GetActivePaidPlansAsync(new[] { card.UserId });

            return Ok(new
            {
                id = card.Id,
                cardInfoId = card.CardInfoId,
                name = card.CardInfo.Name,
                setName = card.CardInfo.CardSet?.Name,
                setCode = card.CardInfo.CardSet?.Code,
                number = card.CardInfo.CardNumber,
                rarity = card.CardInfo.Rarity,
                game = card.CardInfo.CardSet?.Game?.Name,
                imageUrl = card.CardInfo.ImageLarge ?? card.CardInfo.ImageNormal ?? card.CardInfo.ImageUrl ?? card.CardInfo.ImageSmall,
                condition = card.Condition.ToString(),
                finish = card.Finish.ToString(),
                language = card.Language,
                quantity = card.Quantity,
                notes = card.Notes,
                price = CardPricing.EffectivePriceEur(card),
                priceKind = card.AskingPrice.HasValue ? "asking" : "market",
                marketValue = CardPricing.MarketValueEur(card),
                askingPrice = card.AskingPrice,
                isAvailableForTrade = card.IsAvailableForTrade,
                approxKm = ApproxKm(distance),
                isOwn,
                isWanted,
                ownerWantsFromMe,
                owner = new
                {
                    id = card.User.Id,
                    username = card.User.Username,
                    avatarUrl = card.User.AvatarUrl,
                    reputationScore = card.User.ReputationScore,
                    totalReviewsReceived = card.User.TotalReviewsReceived,
                    totalTradesCompleted = card.User.TotalTradesCompleted,
                    memberSince = card.User.CreatedAt,
                    city = card.User.Location?.City,
                    planBadge = plans.TryGetValue(card.UserId, out var plan) && plan.HasVerifiedBadge ? CardExchange.Core.Rules.UserEntitlements.BadgeFor(plan.Tier) : null
                }
            });
        }

        private int CurrentUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
    }
}
