namespace CardExchange.API.Services.Catalog
{
    /// <summary>Carta letta da un catalogo esterno, in forma comune a tutti i giochi. Prezzi in euro.</summary>
    public record CatalogCard
    {
        public string ExternalId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        /// <summary>Riga secondaria (tipo, versione, PS…).</summary>
        public string? Subtitle { get; init; }
        public string? SetCode { get; init; }
        public string? SetName { get; init; }
        public string? Number { get; init; }
        public string? Rarity { get; init; }
        public string? Type { get; init; }
        public string? Text { get; init; }
        public string? ImageSmall { get; init; }
        public string? ImageLarge { get; init; }
        public decimal? PriceEur { get; init; }
        public decimal? PriceEurFoil { get; init; }
        public decimal? PriceUsd { get; init; }
        public decimal? PriceUsdFoil { get; init; }
        /// <summary>true se l'euro è convertito dal dollaro (la fonte non ha prezzi europei).</summary>
        public bool PriceConverted { get; init; }
        /// <summary>Attributi specifici del gioco, salvati come JSON.</summary>
        public IReadOnlyDictionary<string, object?>? Data { get; init; }
    }

    /// <summary>Dati di una riga di import: si usa ciò che c'è (set + numero è il più preciso).</summary>
    public record CatalogLookup(string? Name, string? SetCode, string? SetName, string? Number);

    public record CatalogImportResult(int CardInfoId, bool IsNew);

    /// <summary>
    /// Fonte del catalogo di un gioco. Per aggiungere un gioco basta implementare questa interfaccia e registrarla:
    /// ricerca, aggiunta a collezione/wishlist, import CSV e aggiornamento prezzi funzionano senza altre modifiche.
    /// </summary>
    public interface ICatalogProvider
    {
        /// <summary>Chiave stabile (es. "lorcana"), salvata su Game.CatalogKey.</summary>
        string Key { get; }
        string GameName { get; }
        string Publisher { get; }
        string Description { get; }
        /// <summary>Esempio di riferimento mostrato nell'import (es. "set 1, numero 207").</summary>
        string ImportHint { get; }

        Task<IReadOnlyList<CatalogCard>> SearchAsync(string query, int limit, CancellationToken ct = default);
        Task<CatalogCard?> ResolveAsync(CatalogLookup lookup, CancellationToken ct = default);
        /// <summary>Crea o aggiorna la scheda carta nel database e ne restituisce l'id. Null se la carta non esiste.</summary>
        Task<CatalogImportResult?> ImportAsync(string externalId, CancellationToken ct = default);
    }

    public interface ICatalogRegistry
    {
        IReadOnlyList<ICatalogProvider> All { get; }
        ICatalogProvider? Get(string? key);
    }

    public class CatalogRegistry : ICatalogRegistry
    {
        private readonly Dictionary<string, ICatalogProvider> _byKey;

        public CatalogRegistry(IEnumerable<ICatalogProvider> providers)
        {
            All = providers.ToList();
            _byKey = All.ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyList<ICatalogProvider> All { get; }
        public ICatalogProvider? Get(string? key) => key != null && _byKey.TryGetValue(key, out var p) ? p : null;
    }
}
