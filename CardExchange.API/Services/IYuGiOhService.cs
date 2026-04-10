using CardExchange.API.DTOs.YuGiOh;
using CardExchange.Core.Entities;

namespace CardExchange.API.Services
{
    public interface IYuGiOhService
    {
        // Lookup
        Task<YuGiOhCard?> GetCardByIdAsync(int yugiohId);
        Task<List<YuGiOhCard>> SearchCardsAsync(string query, int offset = 0, int num = 20);

        // Set
        Task<List<YuGiOhCardSet>> GetAllSetsAsync();

        // Mapping helpers
        void MapToCardInfo(YuGiOhCard src, CardInfo dest);
        void MapToCardSet(YuGiOhCardSet src, CardSet dest);
    }
}
