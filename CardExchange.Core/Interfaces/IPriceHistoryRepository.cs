using CardExchange.Core.Entities;

namespace CardExchange.Core.Interfaces
{
    public interface IPriceHistoryRepository : IBaseRepository<PriceHistory>
    {
        Task<IEnumerable<PriceHistory>> GetHistoryForCardAsync(int cardInfoId, int days = 90);
        Task<PriceHistory?> GetLatestSnapshotAsync(int cardInfoId);
        Task<IEnumerable<PriceHistory>> GetLatestSnapshotsAsync(IEnumerable<int> cardInfoIds);
        Task SaveSnapshotAsync(int cardInfoId, decimal? priceUsd, decimal? priceUsdFoil, decimal? priceEur, decimal? priceEurFoil);
        Task<bool> HasSnapshotForDateAsync(int cardInfoId, DateTime date);
    }
}
