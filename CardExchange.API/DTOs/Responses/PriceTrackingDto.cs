namespace CardExchange.API.DTOs.Responses
{
    public class PortfolioSummaryDto
    {
        public decimal TotalValueEur { get; set; }
        public decimal TotalValueUsd { get; set; }
        public decimal ChangeEur24h { get; set; }
        public decimal ChangeUsd24h { get; set; }
        public decimal ChangePercentage24h { get; set; }
        public int TotalCards { get; set; }
        public int UniqueCards { get; set; }
        public CardValueDto? MostValuableCard { get; set; }
    }

    public class CardValueDto
    {
        public int CardInfoId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SetName { get; set; }
        public decimal? PriceEur { get; set; }
        public decimal? PriceUsd { get; set; }
        public string? ImageSmall { get; set; }
    }

    public class PriceHistoryDto
    {
        public int CardInfoId { get; set; }
        public string CardName { get; set; } = string.Empty;
        public string? SetName { get; set; }
        public IEnumerable<PricePointDto> DataPoints { get; set; } = new List<PricePointDto>();
    }

    public class PricePointDto
    {
        public DateTime Date { get; set; }
        public decimal? PriceUsd { get; set; }
        public decimal? PriceUsdFoil { get; set; }
        public decimal? PriceEur { get; set; }
        public decimal? PriceEurFoil { get; set; }
    }

    public class MoverDto
    {
        public int CardInfoId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SetName { get; set; }
        public decimal? CurrentPrice { get; set; }
        public decimal? PreviousPrice { get; set; }
        public decimal ChangeAmount { get; set; }
        public decimal ChangePercentage { get; set; }
        public string? ImageSmall { get; set; }
    }

    public class PriceAlertDto
    {
        public int Id { get; set; }
        public int CardInfoId { get; set; }
        public string CardName { get; set; } = string.Empty;
        public string? SetName { get; set; }
        public decimal TargetPrice { get; set; }
        public string Direction { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsTriggered { get; set; }
        public DateTime? TriggeredAt { get; set; }
        public string? Notes { get; set; }
        public decimal? CurrentPrice { get; set; }
        public string? ImageSmall { get; set; }
    }

    public class TradeAnalysisDto
    {
        public decimal OfferedValueEur { get; set; }
        public decimal OfferedValueUsd { get; set; }
        public decimal RequestedValueEur { get; set; }
        public decimal RequestedValueUsd { get; set; }
        public decimal DifferenceEur { get; set; }
        public decimal DifferenceUsd { get; set; }
        public string Verdict { get; set; } = string.Empty;
        public string VerdictDescription { get; set; } = string.Empty;
        public IEnumerable<TradeCardValueDto> OfferedCards { get; set; } = new List<TradeCardValueDto>();
        public IEnumerable<TradeCardValueDto> RequestedCards { get; set; } = new List<TradeCardValueDto>();
    }

    public class TradeCardValueDto
    {
        public int CardId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal? PriceEur { get; set; }
        public decimal? PriceUsd { get; set; }
        public string? ImageSmall { get; set; }
    }

    public class CreatePriceAlertRequest
    {
        public int CardInfoId { get; set; }
        public decimal TargetPrice { get; set; }
        public int Direction { get; set; } = 2; // Below
        public int Currency { get; set; } = 2; // Eur
        public string? Notes { get; set; }
    }

    public class TradeAnalyzeRequest
    {
        public List<int> OfferedCardIds { get; set; } = new();
        public List<int> RequestedCardIds { get; set; } = new();
    }

    public class PriceSpikeDto
    {
        public int CardId { get; set; }
        public int CardInfoId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SetName { get; set; }
        public string? ImageSmall { get; set; }
        public decimal CurrentPriceEur { get; set; }
        public decimal OldPriceEur { get; set; }
        public decimal ChangePercentage { get; set; }
        public decimal ChangeAmount { get; set; }
        public List<PriceDayPointDto> Last5Days { get; set; } = new();
    }

    public class PriceDayPointDto
    {
        public DateTime Date { get; set; }
        public decimal? PriceEur { get; set; }
    }

    public class SpikeSettingsDto
    {
        public decimal ThresholdPercentage { get; set; }
    }

    public class UpdateSpikeSettingsRequest
    {
        public decimal ThresholdPercentage { get; set; }
    }
}
