namespace CardExchange.API.DTOs.Responses
{
    public class WishlistItemDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int CardInfoId { get; set; }
        public string CardName { get; set; } = string.Empty;
        public string CardSetName { get; set; } = string.Empty;
        public string CardSetCode { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string? CardNumber { get; set; }
        public string? Rarity { get; set; }
        public string? PreferredCondition { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? Notes { get; set; }
        public int Priority { get; set; }
        public string PriorityLabel { get; set; } = string.Empty; // "Alta", "Media", "Bassa"
        public DateTime CreatedAt { get; set; }
        public int AvailableMatches { get; set; } // Quante carte corrispondenti disponibili per scambio
    }

    public class WishlistItemDetailDto : WishlistItemDto
    {
        public string? CardType { get; set; }
        public string? CardDescription { get; set; }
        public string? CardImageUrl { get; set; }
        public IEnumerable<MatchingCardDto> MatchingCards { get; set; } = new List<MatchingCardDto>();
    }

    public class MatchingCardDto
    {
        public int CardId { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal? EstimatedValue { get; set; }
        public UserLocationDto? UserLocation { get; set; }
        public double? DistanceKm { get; set; } // Distanza dall'utente che cerca
    }
}