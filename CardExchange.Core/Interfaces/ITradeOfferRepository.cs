using CardExchange.Core.Entities;

namespace CardExchange.Core.Interfaces
{
    public interface ITradeOfferRepository : IBaseRepository<TradeOffer>
    {
        Task<TradeOffer?> GetWithDetailsAsync(int id);
        Task<IEnumerable<TradeOffer>> GetUserOffersAsync(int userId, TradeOfferStatus? status = null);
        Task<(IEnumerable<TradeOffer> Items, int TotalCount)> GetUserOffersPagedAsync(int userId, int page, int pageSize, TradeOfferStatus? status = null);
        Task<int> GetActiveOfferCountAsync(int userId);
    }
}
