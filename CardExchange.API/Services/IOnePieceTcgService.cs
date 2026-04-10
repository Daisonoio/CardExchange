using CardExchange.API.DTOs.OnePieceTcg;
using CardExchange.Core.Entities;

namespace CardExchange.API.Services
{
    public interface IOnePieceTcgService
    {
        // Lookup
        Task<OnePieceCard?> GetCardByCodeAsync(string code);
        Task<OnePieceResponse?> SearchCardsAsync(string query, int page = 1);

        // Mapping helpers
        void MapToCardInfo(OnePieceCard src, CardInfo dest);
    }
}
