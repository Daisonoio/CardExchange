using CardExchange.API.DTOs.Requests;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services
{
    public record CardSearchHit(Card Card, decimal? Price, decimal? Market, double? Distance);

    public record CardSearchResult(List<CardSearchHit> Hits, bool Truncated, bool HasLocation, string SortBy, double? OriginLat, double? OriginLng);

    /// <summary>
    /// Ricerca delle carte in scambio di altri utenti (usata da Esplora e dalla ricerca avanzata Premium).
    /// Esclude le proprie carte, gli utenti bloccati e i sospesi. Prezzo = quello indicato o il valore di mercato.
    /// Senza coordinate usa la posizione del profilo.
    /// </summary>
    public class CardSearchService
    {
        public const int CandidateLimit = 3000;

        private readonly ApplicationDbContext _context;
        private readonly CardExchange.API.Services.Moderation.IUserBlockService _blocks;

        public CardSearchService(ApplicationDbContext context, CardExchange.API.Services.Moderation.IUserBlockService blocks)
        {
            _context = context;
            _blocks = blocks;
        }

        public async Task<CardSearchResult> SearchAsync(int userId, AdvancedSearchRequest request)
        {
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

            var blocked = await _blocks.GetBlockedUserIdsAsync(userId);
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

            var hits = candidates
                .Select(c =>
                {
                    var userLoc = c.User.Location;
                    double? distance = lat.HasValue && lng.HasValue && userLoc?.Latitude != null && userLoc.Longitude != null
                        ? Math.Round(Geo.DistanceKm(lat.Value, lng.Value, (double)userLoc.Latitude.Value, (double)userLoc.Longitude.Value), 1)
                        : null;
                    return new CardSearchHit(c, CardPricing.EffectivePriceEur(c), CardPricing.MarketValueEur(c), distance);
                })
                // Senza posizione il raggio non si può applicare: il client lo segnala (hasLocation = false)
                .Where(x => !radius.HasValue || !lat.HasValue || (x.Distance.HasValue && x.Distance.Value <= radius.Value))
                .Where(x => !request.MinPrice.HasValue || (x.Price.HasValue && x.Price.Value >= request.MinPrice.Value))
                .Where(x => !request.MaxPrice.HasValue || (x.Price.HasValue && x.Price.Value <= request.MaxPrice.Value))
                .ToList();

            var sort = (request.SortBy ?? (lat.HasValue ? "distance" : "newest")).ToLowerInvariant();
            hits = sort switch
            {
                "distance" => hits.OrderBy(x => x.Distance ?? double.MaxValue).ThenBy(x => x.Price ?? decimal.MaxValue).ToList(),
                "price" => hits.OrderBy(x => x.Price ?? decimal.MaxValue).ThenBy(x => x.Distance ?? double.MaxValue).ToList(),
                _ => hits.OrderByDescending(x => x.Card.CreatedAt).ToList()
            };

            return new CardSearchResult(hits, candidates.Count == CandidateLimit, lat.HasValue, sort, lat, lng);
        }
    }
}
