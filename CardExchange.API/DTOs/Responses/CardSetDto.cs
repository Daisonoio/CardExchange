namespace CardExchange.API.DTOs.Responses
{
    public class CardSetDto
    {
        public int Id { get; set; }
        public int GameId { get; set; }
        public string GameName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public DateTime? ReleaseDate { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CardSetDetailDto : CardSetDto
    {
        public int CardInfosCount { get; set; }
        public IEnumerable<CardInfoDto> CardInfos { get; set; } = new List<CardInfoDto>();
    }
}