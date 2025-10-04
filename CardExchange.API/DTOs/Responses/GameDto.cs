namespace CardExchange.API.DTOs.Responses
{
    public class GameDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Publisher { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class GameDetailDto : GameDto
    {
        public int CardSetsCount { get; set; }
        public IEnumerable<CardSetDto> CardSets { get; set; } = new List<CardSetDto>();
    }
}