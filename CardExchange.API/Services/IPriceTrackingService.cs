using CardExchange.Core.Entities;

namespace CardExchange.API.Services
{
    public interface IPriceTrackingService
    {
        // Portfolio
        Task<PortfolioSummary> GetPortfolioSummaryAsync(int userId);
        Task<IEnumerable<PortfolioMovers>> GetPortfolioMoversAsync(int userId, int limit = 10);

        // Storico prezzi
        Task<IEnumerable<PriceHistory>> GetPriceHistoryAsync(int cardInfoId, int days = 90);
        Task SnapshotPricesForUserAsync(int userId);

        // Alert
        Task<IEnumerable<PriceAlert>> GetUserAlertsAsync(int userId);
        Task<PriceAlert> CreateAlertAsync(PriceAlert alert);
        Task<bool> DeleteAlertAsync(int alertId, int userId);
        Task CheckAndTriggerAlertsAsync(int userId);

        // Trade Analyzer
        Task<TradeAnalysis> AnalyzeTradeAsync(IEnumerable<int> offeredCardIds, IEnumerable<int> requestedCardIds);
    }

    public class PortfolioSummary
    {
        public decimal TotalValueEur { get; set; }
        public decimal TotalValueUsd { get; set; }
        public decimal ChangeEur24h { get; set; }
        public decimal ChangeUsd24h { get; set; }
        public decimal ChangePercentage24h { get; set; }
        public int TotalCards { get; set; }
        public int UniqueCards { get; set; }
        public CardValueInfo? MostValuableCard { get; set; }
    }

    public class CardValueInfo
    {
        public int CardInfoId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SetName { get; set; }
        public decimal? PriceEur { get; set; }
        public decimal? PriceUsd { get; set; }
        public string? ImageSmall { get; set; }
    }

    public class PortfolioMovers
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

    public class TradeAnalysis
    {
        public decimal OfferedValueEur { get; set; }
        public decimal OfferedValueUsd { get; set; }
        public decimal RequestedValueEur { get; set; }
        public decimal RequestedValueUsd { get; set; }
        public decimal DifferenceEur { get; set; }
        public decimal DifferenceUsd { get; set; }
        public string Verdict { get; set; } = string.Empty; // "fair", "in_your_favor", "against_you"
        public IEnumerable<TradeCardValue> OfferedCards { get; set; } = new List<TradeCardValue>();
        public IEnumerable<TradeCardValue> RequestedCards { get; set; } = new List<TradeCardValue>();
    }

    public class TradeCardValue
    {
        public int CardId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal? PriceEur { get; set; }
        public decimal? PriceUsd { get; set; }
        public string? ImageSmall { get; set; }
    }
}
