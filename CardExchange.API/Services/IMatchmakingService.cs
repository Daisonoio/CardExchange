using CardExchange.Core.Entities;

namespace CardExchange.API.Services
{
    public interface IMatchmakingService
    {
        Task<IEnumerable<MatchResult>> FindMatchesAsync(int userId, int? radiusKm = null, double? latitude = null, double? longitude = null, int? gameId = null);
    }

    public class MatchResult
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public decimal ReputationScore { get; set; }
        public int TotalTradesCompleted { get; set; }

        // Location
        public string? City { get; set; }
        public string? Province { get; set; }
        public double? DistanceKm { get; set; }

        // Cards they have that I want
        public List<MatchCard> TheyHaveIWant { get; set; } = new();
        // Cards I have that they want
        public List<MatchCard> IHaveTheyWant { get; set; } = new();

        // Scoring
        public double Score { get; set; }
        public int MutualCardCount { get; set; }
        public bool IsMutual { get; set; }
    }

    public class MatchCard
    {
        public int CardId { get; set; }
        public int CardInfoId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SetName { get; set; }
        public string? ImageSmall { get; set; }
        public string Condition { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal? PriceEur { get; set; }
        public int WishlistPriority { get; set; }
    }
}
