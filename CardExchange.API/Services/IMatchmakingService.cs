using CardExchange.Core.Entities;

namespace CardExchange.API.Services
{
    public interface IMatchmakingService
    {
        /// <param name="onlyUserIds">Se valorizzato, cerca match solo tra questi utenti e senza filtro di distanza (es. presenti a un evento).</param>
        Task<IEnumerable<MatchResult>> FindMatchesAsync(int userId, int? radiusKm = null, double? latitude = null, double? longitude = null, int? gameId = null, IReadOnlyCollection<int>? onlyUserIds = null);
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
        /// <summary>"Premium" o "Store" se l'utente ha un piano con badge sul profilo.</summary>
        public string? PlanBadge { get; set; }
    }

    public class MatchCard
    {
        public int CardId { get; set; }
        public int CardInfoId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SetName { get; set; }
        public string? ImageSmall { get; set; }
        public string Condition { get; set; } = string.Empty;
        public string? Language { get; set; }
        public string Finish { get; set; } = "Normal";
        public int Quantity { get; set; }
        /// <summary>Valore di mercato della copia.</summary>
        public decimal? PriceEur { get; set; }
        /// <summary>Prezzo indicato dal proprietario (indicativo), se impostato.</summary>
        public decimal? AskingPrice { get; set; }
        public int WishlistPriority { get; set; }
    }
}
