namespace CardExchange.API.DTOs.Responses
{
    public class CardInfoDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CardSetName { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string? CardNumber { get; set; }
        public string? Rarity { get; set; }
        public string? Type { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int AvailableCount { get; set; } // Quante carte disponibili per lo scambio
    }
}