using CardExchange.Core.Entities;

namespace CardExchange.Core.Interfaces
{
    public interface ICardInfoRepository : IBaseRepository<CardInfo>
    {
        Task<IEnumerable<CardInfo>> GetByCardSetAsync(int cardSetId);
        Task<CardInfo?> GetByNameAndSetAsync(string name, int cardSetId);
        Task<IEnumerable<CardInfo>> SearchByNameAsync(string name);
        Task<IEnumerable<CardInfo>> GetPopularCardsAsync(int count = 10);
    }
}