namespace CardExchange.API.DTOs.Responses
{
    public class WishlistItemDto
    {
        public int Id { get; set; }
        public int CardInfoId { get; set; }
        public string CardName { get; set; } = string.Empty;
        public string CardSetName { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string? PreferredCondition { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? Notes { get; set; }
        public int Priority { get; set; }
        public DateTime CreatedAt { get; set; }
        public int AvailableMatches { get; set; } // Quante carte corrispondenti disponibili
    }
}