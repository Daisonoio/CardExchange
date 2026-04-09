using CardExchange.API.DTOs.PokemonTcg;
using CardExchange.Core.Entities;

namespace CardExchange.API.Services
{
    public interface IPokemonTcgService
    {
        // Lookup
        Task<PokemonTcgCard?> GetCardByIdAsync(string pokemonTcgId);
        Task<PokemonTcgResponse<List<PokemonTcgCard>>?> SearchCardsAsync(string query, int page = 1, int pageSize = 20);

        // Set
        Task<List<PokemonTcgSet>> GetAllSetsAsync();
        Task<PokemonTcgSet?> GetSetByIdAsync(string setId);
        Task<PokemonTcgResponse<List<PokemonTcgCard>>?> GetCardsBySetAsync(string setId, int page = 1, int pageSize = 250);

        // Mapping helpers
        void MapToCardInfo(PokemonTcgCard src, CardInfo dest);
        void MapToCardSet(PokemonTcgSet src, CardSet dest);
    }
}
