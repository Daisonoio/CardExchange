using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services
{
    /// <summary>
    /// Matchmaking: chi ha le carte che cerco e chi cerca le carte che ho, con distanza e punteggio.
    /// Numero di query costante (circa 6) indipendente dalle dimensioni di wishlist e collezione; con una posizione
    /// nota gli utenti lontani sono scartati già nel database con un prefiltro rettangolare.
    /// </summary>
    public class MatchmakingService : IMatchmakingService
    {
        /// <summary>
        /// Spinta in classifica per chi ha un piano con visibilità prioritaria: vale quanto avvicinarsi di una fascia
        /// di distanza, quindi non scavalca mai un match reciproco né un match molto più ricco.
        /// </summary>
        public const double PriorityListingBonus = 15;

        private readonly ApplicationDbContext _context;
        private readonly CardExchange.API.Services.Moderation.IUserBlockService _blocks;
        private readonly CardExchange.API.Authorization.ISubscriptionService? _subscriptions;
        private readonly ILogger<MatchmakingService> _logger;

        public MatchmakingService(
            ApplicationDbContext context,
            CardExchange.API.Services.Moderation.IUserBlockService blocks,
            ILogger<MatchmakingService> logger,
            CardExchange.API.Authorization.ISubscriptionService? subscriptions = null)
        {
            _context = context;
            _blocks = blocks;
            _logger = logger;
            _subscriptions = subscriptions;
        }

        public async Task<IEnumerable<MatchResult>> FindMatchesAsync(
            int userId, int? radiusKm = null, double? latitude = null, double? longitude = null, int? gameId = null, IReadOnlyCollection<int>? onlyUserIds = null)
        {
            var me = await _context.Users.AsNoTracking().Include(u => u.Location).FirstOrDefaultAsync(u => u.Id == userId);
            if (me == null) return Enumerable.Empty<MatchResult>();

            double? searchLat = latitude ?? (double?)me.Location?.Latitude;
            double? searchLon = longitude ?? (double?)me.Location?.Longitude;
            int searchRadius = radiusKm ?? me.Location?.MaxDistanceKm ?? 100;

            // 1-2. Cosa cerco e cosa offro (filtrati per gioco nel database)
            var myWishlist = await _context.WishlistItems.AsNoTracking()
                .Where(w => w.UserId == userId && (gameId == null || w.CardInfo.CardSet.GameId == gameId))
                .ToListAsync();
            var myCards = await _context.Cards.AsNoTracking()
                .Include(c => c.CardInfo).ThenInclude(ci => ci.CardSet)
                .Where(c => c.UserId == userId && c.IsAvailableForTrade && (gameId == null || c.CardInfo.CardSet.GameId == gameId))
                .ToListAsync();

            var wantedIds = myWishlist.Select(w => w.CardInfoId).Distinct().ToList();
            var offeredIds = myCards.Select(c => c.CardInfoId).Distinct().ToList();
            if (wantedIds.Count == 0 && offeredIds.Count == 0) return Enumerable.Empty<MatchResult>();

            var blocked = await _blocks.GetBlockedUserIdsAsync(userId);
            var onlyIds = onlyUserIds?.ToList();

            // Prefiltro geografico: rettangolo attorno al raggio. Gli utenti senza posizione restano (distanza ignota),
            // come prima. Non si applica ai match tra presenti a un evento (sono nello stesso posto).
            var useBox = onlyIds == null && searchLat.HasValue && searchLon.HasValue;
            decimal minLat = 0, maxLat = 0, minLon = 0, maxLon = 0;
            if (useBox)
            {
                var latRange = searchRadius / 111.0;
                var lonRange = searchRadius / (111.0 * Math.Max(0.01, Math.Cos(searchLat!.Value * Math.PI / 180)));
                minLat = (decimal)(searchLat.Value - latRange);
                maxLat = (decimal)(searchLat.Value + latRange);
                minLon = (decimal)(searchLon!.Value - lonRange);
                maxLon = (decimal)(searchLon.Value + lonRange);
            }

            // 3. Carte degli altri che cerco
            var theirCardsQuery = _context.Cards.AsNoTracking()
                .Include(c => c.CardInfo).ThenInclude(ci => ci.CardSet)
                .Where(c => wantedIds.Contains(c.CardInfoId) && c.IsAvailableForTrade && c.UserId != userId
                            && c.User.IsActive && !blocked.Contains(c.UserId));
            if (onlyIds != null) theirCardsQuery = theirCardsQuery.Where(c => onlyIds.Contains(c.UserId));
            if (useBox)
                theirCardsQuery = theirCardsQuery.Where(c => c.User.Location == null || c.User.Location.Latitude == null || c.User.Location.Longitude == null
                    || (c.User.Location.Latitude >= minLat && c.User.Location.Latitude <= maxLat
                        && c.User.Location.Longitude >= minLon && c.User.Location.Longitude <= maxLon));
            var theirCards = wantedIds.Count == 0 ? new List<Card>() : await theirCardsQuery.ToListAsync();

            // 4. Wishlist degli altri che contengono le carte che offro
            var theirWishesQuery = _context.WishlistItems.AsNoTracking()
                .Where(w => offeredIds.Contains(w.CardInfoId) && w.UserId != userId && w.User.IsActive && !blocked.Contains(w.UserId));
            if (onlyIds != null) theirWishesQuery = theirWishesQuery.Where(w => onlyIds.Contains(w.UserId));
            if (useBox)
                theirWishesQuery = theirWishesQuery.Where(w => w.User.Location == null || w.User.Location.Latitude == null || w.User.Location.Longitude == null
                    || (w.User.Location.Latitude >= minLat && w.User.Location.Latitude <= maxLat
                        && w.User.Location.Longitude >= minLon && w.User.Location.Longitude <= maxLon));
            var theirWishes = offeredIds.Count == 0 ? new List<WishlistItem>() : await theirWishesQuery.ToListAsync();

            // Abbinamenti con le regole condivise (condizione, lingua, finitura, prezzo massimo)
            var wishesByCard = myWishlist.ToLookup(w => w.CardInfoId);
            var theyHaveIWant = theirCards
                .SelectMany(card => wishesByCard[card.CardInfoId].Where(wish => WishlistMatchRules.Satisfies(card, wish)).Select(wish => (card, wish)))
                .GroupBy(x => x.card.UserId)
                .ToDictionary(g => g.Key, g => g.ToList());
            var myCardsByInfo = myCards.ToLookup(c => c.CardInfoId);
            var iHaveTheyWant = theirWishes
                .SelectMany(wish => myCardsByInfo[wish.CardInfoId].Where(card => WishlistMatchRules.Satisfies(card, wish)).Select(card => (card, wish)))
                .GroupBy(x => x.wish.UserId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var otherIds = theyHaveIWant.Keys.Union(iHaveTheyWant.Keys).ToList();
            if (otherIds.Count == 0) return Enumerable.Empty<MatchResult>();

            // 5. Profili degli altri utenti in un'unica query
            var others = await _context.Users.AsNoTracking().Include(u => u.Location)
                .Where(u => otherIds.Contains(u.Id) && u.IsActive)
                .ToDictionaryAsync(u => u.Id);

            var results = new List<MatchResult>();
            foreach (var otherId in otherIds)
            {
                if (!others.TryGetValue(otherId, out var other)) continue;

                double? distanceKm = null;
                if (searchLat.HasValue && searchLon.HasValue && other.Location?.Latitude != null && other.Location?.Longitude != null)
                {
                    distanceKm = Geo.DistanceKm(searchLat.Value, searchLon.Value,
                        (double)other.Location.Latitude.Value, (double)other.Location.Longitude.Value);
                    if (onlyIds == null && distanceKm > searchRadius) continue;
                }

                var theyHave = theyHaveIWant.TryGetValue(otherId, out var th) ? th.Select(x => ToMatchCard(x.card, x.wish)).ToList() : new List<MatchCard>();
                var iHave = iHaveTheyWant.TryGetValue(otherId, out var ih) ? ih.Select(x => ToMatchCard(x.card, x.wish)).ToList() : new List<MatchCard>();
                var isMutual = theyHave.Count > 0 && iHave.Count > 0;
                var mutualCount = Math.Min(theyHave.Count, iHave.Count);

                results.Add(new MatchResult
                {
                    UserId = other.Id,
                    Username = other.Username,
                    AvatarUrl = other.AvatarUrl,
                    ReputationScore = other.ReputationScore,
                    TotalTradesCompleted = other.TotalTradesCompleted,
                    City = other.Location?.City,
                    Province = other.Location?.Province,
                    DistanceKm = distanceKm.HasValue ? Math.Round(distanceKm.Value, 1) : null,
                    TheyHaveIWant = theyHave,
                    IHaveTheyWant = iHave,
                    Score = Math.Round(Score(theyHave, iHave, distanceKm, other.ReputationScore, other.TotalTradesCompleted), 1),
                    MutualCardCount = mutualCount,
                    IsMutual = isMutual
                });
            }

            if (_subscriptions != null && results.Count > 0)
            {
                var plans = await _subscriptions.GetActivePaidPlansAsync(results.Select(r => r.UserId));
                foreach (var r in results)
                {
                    if (!plans.TryGetValue(r.UserId, out var plan)) continue;
                    if (plan.HasPriorityListing) r.Score = Math.Round(r.Score + PriorityListingBonus, 1);
                    if (plan.HasVerifiedBadge) r.PlanBadge = plan.Tier.ToString();
                }
            }

            _logger.LogDebug("Matchmaking per {UserId}: {Count} risultati da {Cards} carte e {Wishes} desideri candidati",
                userId, results.Count, theirCards.Count, theirWishes.Count);

            // Prima i reciproci, poi per punteggio
            return results
                .OrderByDescending(r => r.IsMutual)
                .ThenByDescending(r => r.Score)
                .ThenBy(r => r.DistanceKm ?? double.MaxValue) // a parità di punteggio, il più vicino
                .ThenBy(r => r.UserId)
                .ToList();
        }

        /// <summary>
        /// Punteggio di un match: reciprocità, numero di carte, priorità della wishlist, vicinanza, reputazione ed esperienza.
        /// </summary>
        public static double Score(IReadOnlyCollection<MatchCard> theyHave, IReadOnlyCollection<MatchCard> iHave,
            double? distanceKm, decimal reputation, int completedTrades)
        {
            double score = 0;
            if (theyHave.Count > 0 && iHave.Count > 0)
                score += 100 + Math.Min(theyHave.Count, iHave.Count) * 20;
            score += theyHave.Count * 10;
            score += iHave.Count * 5;
            score += theyHave.Count(c => c.WishlistPriority == 1) * 15;

            if (distanceKm.HasValue)
            {
                if (distanceKm <= 10) score += 50;
                else if (distanceKm <= 25) score += 35;
                else if (distanceKm <= 50) score += 20;
                else if (distanceKm <= 100) score += 10;
            }

            if (reputation >= 4) score += 15;
            else if (reputation >= 3) score += 8;

            if (completedTrades >= 10) score += 10;
            else if (completedTrades >= 3) score += 5;
            return score;
        }

        private static MatchCard ToMatchCard(Card card, WishlistItem wish) => new()
        {
            CardId = card.Id,
            CardInfoId = card.CardInfoId,
            Name = card.CardInfo?.Name ?? "N/A",
            SetName = card.CardInfo?.CardSet?.Name,
            ImageSmall = card.CardInfo?.ImageSmall,
            Condition = card.Condition.ToString(),
            Language = card.Language,
            Finish = card.Finish.ToString(),
            Quantity = card.Quantity,
            PriceEur = CardPricing.ReferencePriceEur(card.CardInfo, card.Finish, card.EstimatedValue),
            AskingPrice = card.AskingPrice,
            WishlistPriority = wish.Priority
        };
    }
}
