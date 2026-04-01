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
        public int Quantity { get; set; }
        public string? Notes { get; set; }
        public bool IsAvailableForTrade { get; set; }
        public decimal? EstimatedValue { get; set; }
        public string? ImageSmall { get; set; }
        public string? ImageNormal { get; set; }
        public string? ImageLarge { get; set; }
        public bool HasUserPhotos { get; set; }
        public int UserPhotoCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public UserLocationDto? UserLocation { get; set; }
    }

    public class CardDetailDto : CardDto
    {
        public string? CardType { get; set; }
        public string? CardDescription { get; set; }
        public string? ImageUrl { get; set; }

        // Campi Scryfall
        public string? ScryfallId { get; set; }
        public string? ManaCost { get; set; }
        public decimal? Cmc { get; set; }
        public string? TypeLine { get; set; }
        public string? OracleText { get; set; }
        public string? Colors { get; set; }
        public string? Power { get; set; }
        public string? Toughness { get; set; }
        public string? Loyalty { get; set; }
        public string? Artist { get; set; }
        public string? Keywords { get; set; }
        public CardImagesDto? Images { get; set; }
        public CardPricesDto? Prices { get; set; }
        public string? ScryfallUri { get; set; }
        public List<CardPhotoDto> Photos { get; set; } = new();
    }

    public class CardImagesDto
    {
        public string? Small { get; set; }
        public string? Normal { get; set; }
        public string? Large { get; set; }
        public string? Png { get; set; }
        public string? ArtCrop { get; set; }
        public string? BorderCrop { get; set; }
    }

    public class CardPricesDto
    {
        public decimal? Usd { get; set; }
        public decimal? UsdFoil { get; set; }
        public decimal? Eur { get; set; }
        public decimal? EurFoil { get; set; }
    }
}