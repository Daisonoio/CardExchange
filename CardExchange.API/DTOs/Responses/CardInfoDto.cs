namespace CardExchange.API.DTOs.Responses
{
    public class CardInfoDto
    {
        public int Id { get; set; }
        public int CardSetId { get; set; }
        public string CardSetName { get; set; } = string.Empty;
        public string CardSetCode { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? CardNumber { get; set; }
        public string? Rarity { get; set; }
        public string? Type { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CardInfoDetailDto : CardInfoDto
    {
        public int TotalCardsInCollections { get; set; }
        public int AvailableForTradeCount { get; set; }
        public int InWishlistsCount { get; set; }
    }
}