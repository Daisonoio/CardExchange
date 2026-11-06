using CardExchange.API.Services.Moderation;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services
{
    public record NearbyMatch(
        int CardId, int CardInfoId, string CardName, string CardSetName, string? ImageUrl, string? ImageLarge,
        decimal PriceEur, string Condition, int Quantity, int OwnerId, string OwnerUsername, string? OwnerCity,
        string? OwnerCountry, int DistanceKm);

    /// <summary>
    /// Carte della wishlist disponibili vicino all'utente (sezione "Nelle vicinanze" della Home).
    /// Una sola query sulle carte, già ristretta alla zona; le regole della wishlist si applicano in memoria.
    /// </summary>
    public class NearbyWishlistMatches
    {
        private readonly ApplicationDbContext _db;
        private readonly IUserBlockService _blocks;

        private readonly Entitlements.IEntitlementService _entitlements;

        public NearbyWishlistMatches(ApplicationDbContext db, IUserBlockService blocks, Entitlements.IEntitlementService? entitlements = null)
        {
            _entitlements = Entitlements.EntitlementExtensions.OrDefault(entitlements, db);
            _db = db;
            _blocks = blocks;
        }

        public async Task<List<NearbyMatch>> FindAsync(int userId, double lat, double lon, int radiusKm, int? gameId, int take = 20, CancellationToken ct = default)
        {
            var wishes = await _db.WishlistItems.AsNoTracking()
                .Where(w => w.UserId == userId)
                .Where(w => !gameId.HasValue || w.CardInfo.CardSet.GameId == gameId.Value)
                .ToListAsync(ct);
            if (wishes.Count == 0) return new();

            var byCardInfo = wishes.GroupBy(w => w.CardInfoId).ToDictionary(g => g.Key, g => g.ToList());
            var cardInfoIds = byCardInfo.Keys.ToList();
            var blocked = await _blocks.GetBlockedUserIdsAsync(userId);

            // Riquadro attorno al punto: esclude subito le carte lontane (la distanza esatta si calcola dopo)
            var dLat = (decimal)(radiusKm / 111.0);
            var dLon = (decimal)(radiusKm / Math.Max(1, 111.0 * Math.Cos(lat * Math.PI / 180)));
            decimal minLat = (decimal)lat - dLat, maxLat = (decimal)lat + dLat, minLon = (decimal)lon - dLon, maxLon = (decimal)lon + dLon;

            var cards = await _db.Cards.AsNoTracking()
                .Where(c => cardInfoIds.Contains(c.CardInfoId) && c.IsAvailableForTrade && c.UserId != userId && c.User.IsActive
                            && c.User.Location != null && c.User.Location.Latitude != null && c.User.Location.Longitude != null
                            && c.User.Location.Latitude >= minLat && c.User.Location.Latitude <= maxLat
                            && c.User.Location.Longitude >= minLon && c.User.Location.Longitude <= maxLon)
                .Include(c => c.CardInfo).ThenInclude(ci => ci.CardSet)
                .Include(c => c.User).ThenInclude(u => u.Location)
                .ToListAsync(ct);

            var hidden = await Entitlements.EarlyAccess.HiddenAsync(_entitlements, userId, cards.Select(c => (c.Id, c.UserId, c.CreatedAt)), DateTime.UtcNow);
            var result = new List<NearbyMatch>();
            foreach (var card in cards)
            {
                if (blocked.Contains(card.UserId) || hidden.Contains(card.Id)) continue;
                if (!byCardInfo[card.CardInfoId].Any(w => WishlistMatchRules.Satisfies(card, w))) continue;
                var loc = card.User.Location!;
                var distance = Geo.DistanceKm(lat, lon, (double)loc.Latitude!.Value, (double)loc.Longitude!.Value);
                if (distance > radiusKm) continue;
                var info = card.CardInfo;
                result.Add(new NearbyMatch(
                    card.Id, card.CardInfoId, info?.Name ?? "Unknown", info?.CardSet?.Name ?? "Unknown",
                    info?.ImageNormal ?? info?.ImageSmall ?? info?.ImageUrl, info?.ImageLarge ?? info?.ImageNormal,
                    info?.PriceEur ?? card.EstimatedValue ?? 0, card.Condition.ToString(), card.Quantity,
                    card.UserId, card.User.Username, loc.City, loc.Country,
                    BroadcastRules.DisplayDistanceKm(distance)!.Value));
            }
            return result.OrderByDescending(r => r.PriceEur).ThenBy(r => r.DistanceKm).Take(take).ToList();
        }
    }
}
