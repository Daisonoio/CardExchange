namespace CardExchange.API.DTOs.Responses
{
    public class CardDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserUsername { get; set; } = string.Empty;
        public int CardInfoId { get; set; }
        public string CardName { get; set; } = string.Empty;
        public string CardSetName { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string? CardNumber { get; set; }
        public string? Rarity { get; set; }
        public string Condition { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public bool IsAvailableForTrade { get; set; }
        public decimal? EstimatedValue { get; set; }
        public DateTime CreatedAt { get; set; }
        public UserLocationDto? UserLocation { get; set; }
    }

    public class CardDetailDto : CardDto
    {
        public string? CardType { get; set; }
        public string? CardDescription { get; set; }
        public string? ImageUrl { get; set; }
    }
}