using CardExchange.API.DTOs.Scryfall;
using CardExchange.Core.Entities;

namespace CardExchange.API.Services
{
    public interface IScryfallService
    {
        // Lookup
        Task<ScryfallCard?> GetCardByNameAsync(string exactName);
        Task<ScryfallCard?> GetCardBySetAndNumberAsync(string setCode, string collectorNumber);
        Task<ScryfallCard?> GetCardByScryfallIdAsync(string scryfallId);

        // Ricerca
        Task<ScryfallList<ScryfallCard>?> SearchCardsAsync(string query, int page = 1);
        Task<ScryfallCatalog?> AutocompleteAsync(string query);

        // Collection (bulk validate)
        Task<ScryfallCollectionResponse?> ValidateCollectionAsync(List<ScryfallIdentifier> identifiers);

        // Set
        Task<List<ScryfallSet>> GetAllSetsAsync();
        Task<ScryfallSet?> GetSetByCodeAsync(string code);

        // Mapping helpers
        void MapScryfallToCardInfo(ScryfallCard scryfallCard, CardInfo cardInfo);
        void MapScryfallToCardSet(ScryfallSet scryfallSet, CardSet cardSet);
    }
}
