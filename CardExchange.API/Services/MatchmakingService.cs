using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;

namespace CardExchange.API.Services
{
    public class MatchmakingService : IMatchmakingService
    {
        private readonly ICardRepository _cardRepository;
        private readonly ICardInfoRepository _cardInfoRepository;
        private readonly IWishlistRepository _wishlistRepository;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<MatchmakingService> _logger;

        public MatchmakingService(
            ICardRepository cardRepository,
            ICardInfoRepository cardInfoRepository,
            IWishlistRepository wishlistRepository,
            IUserRepository userRepository,
            ILogger<MatchmakingService> logger)
        {
            _cardRepository = cardRepository;
            _cardInfoRepository = cardInfoRepository;
            _wishlistRepository = wishlistRepository;
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task<IEnumerable<MatchResult>> FindMatchesAsync(
            int userId, int? radiusKm = null, double? latitude = null, double? longitude = null)
        {
            var user = await _userRepository.GetWithLocationAsync(userId);
            if (user == null) return Enumerable.Empty<MatchResult>();

            // Determine search coordinates
            double? searchLat = latitude ?? (double?)user.Location?.Latitude;
            double? searchLon = longitude ?? (double?)user.Location?.Longitude;
            int searchRadius = radiusKm ?? user.Location?.MaxDistanceKm ?? 100;

            // 1. Get my wishlist (what I want)
            var myWishlist = (await _wishlistRepository.GetUserWishlistAsync(userId)).ToList();
            var myWishlistCardInfoIds = myWishlist.Select(w => w.CardInfoId).ToHashSet();

            // 2. Get my cards available for trade (what I offer)
            var myCards = (await _cardRepository.GetUserCardsAsync(userId))
                .Where(c => c.IsAvailableForTrade)
                .ToList();
            var myCardInfoIds = myCards.Select(c => c.CardInfoId).ToHashSet();

            if (!myWishlistCardInfoIds.Any() && !myCardInfoIds.Any())
                return Enumerable.Empty<MatchResult>();

            // 3. Find all users who have cards I want (theyHaveIWant)
            var theyHaveIWant = new Dictionary<int, List<(Card card, WishlistItem wishlistItem)>>();
            foreach (var wish in myWishlist)
            {
                var availableCards = await _cardRepository.GetCardsByCardInfoAsync(wish.CardInfoId);
                foreach (var card in availableCards.Where(c => c.UserId != userId))
                {
                    // Condition filter
                    if (wish.PreferredCondition.HasValue && card.Condition < wish.PreferredCondition.Value)
                        continue;
                    // Price filter
                    if (wish.MaxPrice.HasValue && card.EstimatedValue.HasValue && card.EstimatedValue > wish.MaxPrice)
                        continue;

                    if (!theyHaveIWant.ContainsKey(card.UserId))
                        theyHaveIWant[card.UserId] = new();
                    theyHaveIWant[card.UserId].Add((card, wish));
                }
            }

            // 4. Find all users who want cards I have (iHaveTheyWant)
            var iHaveTheyWant = new Dictionary<int, List<(Card card, WishlistItem wishlistItem)>>();
            foreach (var myCard in myCards)
            {
                var wishlists = await _wishlistRepository.GetWishlistByCardInfoAsync(myCard.CardInfoId);
                foreach (var wish in wishlists.Where(w => w.UserId != userId))
                {
                    // Condition filter
                    if (wish.PreferredCondition.HasValue && myCard.Condition < wish.PreferredCondition.Value)
                        continue;

                    if (!iHaveTheyWant.ContainsKey(wish.UserId))
                        iHaveTheyWant[wish.UserId] = new();
                    iHaveTheyWant[wish.UserId].Add((myCard, wish));
                }
            }

            // 5. Merge into unified user set
            var allUserIds = theyHaveIWant.Keys.Union(iHaveTheyWant.Keys).ToHashSet();

            var results = new List<MatchResult>();

            foreach (var otherUserId in allUserIds)
            {
                var otherUser = await _userRepository.GetWithLocationAsync(otherUserId);
                if (otherUser == null || !otherUser.IsActive) continue;

                // Calculate distance
                double? distanceKm = null;
                if (searchLat.HasValue && searchLon.HasValue &&
                    otherUser.Location?.Latitude != null && otherUser.Location?.Longitude != null)
                {
                    distanceKm = CalculateDistance(
                        searchLat.Value, searchLon.Value,
                        (double)otherUser.Location.Latitude.Value,
                        (double)otherUser.Location.Longitude.Value);

                    // Filter by radius
                    if (distanceKm > searchRadius) continue;
                }

                // Build match cards: they have, I want
                var theyHave = new List<MatchCard>();
                if (theyHaveIWant.TryGetValue(otherUserId, out var theirCards))
                {
                    foreach (var (card, wish) in theirCards)
                    {
                        var info = card.CardInfo ?? await _cardInfoRepository.GetByIdAsync(card.CardInfoId);
                        theyHave.Add(new MatchCard
                        {
                            CardId = card.Id,
                            CardInfoId = card.CardInfoId,
                            Name = info?.Name ?? "N/A",
                            SetName = info?.CardSet?.Name,
                            ImageSmall = info?.ImageSmall,
                            Condition = card.Condition.ToString(),
                            Quantity = card.Quantity,
                            PriceEur = info?.PriceEur ?? card.EstimatedValue,
                            WishlistPriority = wish.Priority
                        });
                    }
                }

                // Build match cards: I have, they want
                var iHave = new List<MatchCard>();
                if (iHaveTheyWant.TryGetValue(otherUserId, out var myMatchCards))
                {
                    foreach (var (card, wish) in myMatchCards)
                    {
                        var info = card.CardInfo ?? await _cardInfoRepository.GetByIdAsync(card.CardInfoId);
                        iHave.Add(new MatchCard
                        {
                            CardId = card.Id,
                            CardInfoId = card.CardInfoId,
                            Name = info?.Name ?? "N/A",
                            SetName = info?.CardSet?.Name,
                            ImageSmall = info?.ImageSmall,
                            Condition = card.Condition.ToString(),
                            Quantity = card.Quantity,
                            PriceEur = info?.PriceEur ?? card.EstimatedValue,
                            WishlistPriority = wish.Priority
                        });
                    }
                }

                bool isMutual = theyHave.Any() && iHave.Any();
                int mutualCount = Math.Min(theyHave.Count, iHave.Count);

                // Score calculation
                double score = 0;

                // Mutual match bonus (highest weight)
                if (isMutual)
                    score += 100 + (mutualCount * 20);

                // One-way match value
                score += theyHave.Count * 10;
                score += iHave.Count * 5;

                // High priority wishlist bonus
                score += theyHave.Where(c => c.WishlistPriority == 1).Count() * 15;

                // Distance bonus (closer = better)
                if (distanceKm.HasValue)
                {
                    if (distanceKm <= 10) score += 50;
                    else if (distanceKm <= 25) score += 35;
                    else if (distanceKm <= 50) score += 20;
                    else if (distanceKm <= 100) score += 10;
                }

                // Reputation bonus
                if (otherUser.ReputationScore >= 4) score += 15;
                else if (otherUser.ReputationScore >= 3) score += 8;

                // Completed trades bonus
                if (otherUser.TotalTradesCompleted >= 10) score += 10;
                else if (otherUser.TotalTradesCompleted >= 3) score += 5;

                results.Add(new MatchResult
                {
                    UserId = otherUser.Id,
                    Username = otherUser.Username,
                    AvatarUrl = otherUser.AvatarUrl,
                    ReputationScore = otherUser.ReputationScore,
                    TotalTradesCompleted = otherUser.TotalTradesCompleted,
                    City = otherUser.Location?.City,
                    Province = otherUser.Location?.Province,
                    DistanceKm = distanceKm.HasValue ? Math.Round(distanceKm.Value, 1) : null,
                    TheyHaveIWant = theyHave,
                    IHaveTheyWant = iHave,
                    Score = Math.Round(score, 1),
                    MutualCardCount = mutualCount,
                    IsMutual = isMutual
                });
            }

            // Sort: mutual first, then by score descending
            return results
                .OrderByDescending(r => r.IsMutual)
                .ThenByDescending(r => r.Score)
                .ToList();
        }

        private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadiusKm = 6371;
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            lat1 = lat1 * Math.PI / 180;
            lat2 = lat2 * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1) * Math.Cos(lat2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadiusKm * c;
        }
    }
}
