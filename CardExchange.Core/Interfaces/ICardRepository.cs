using CardExchange.Core.Entities;

namespace CardExchange.Core.Interfaces
{
    public interface ICardRepository : IBaseRepository<Card>
    {
        Task<IEnumerable<Card>> GetUserCardsAsync(int userId);
        Task<IEnumerable<Card>> GetCardsByCardInfoAsync(int cardInfoId);
        Task<IEnumerable<Card>> GetAvailableCardsAsync();
        Task<IEnumerable<Card>> SearchCardsAsync(string searchTerm);
        Task<IEnumerable<Card>> GetCardsByLocationAsync(string city, string province, string country);
        Task<IEnumerable<Card>> GetCardsByConditionAsync(CardCondition condition);
        Task<Card?> GetUserCardAsync(int userId, int cardId);
    }
}