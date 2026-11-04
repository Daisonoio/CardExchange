using System.Text.Json;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Catalog
{
    /// <summary>
    /// Salvataggio nel database delle carte dei provider generici: crea gioco e set se mancano e aggiorna
    /// la scheda carta (chiave: fonte + id esterno). Nessuna colonna specifica per gioco.
    /// </summary>
    public class CatalogStore
    {
        public static readonly TimeSpan Freshness = TimeSpan.FromHours(24);
        private readonly ApplicationDbContext _context;

        public CatalogStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<CardInfo?> FindAsync(string source, string externalId, CancellationToken ct = default) =>
            _context.CardInfos.FirstOrDefaultAsync(ci => ci.CatalogSource == source && ci.ExternalId == externalId, ct);

        public async Task<Game> EnsureGameAsync(ICatalogProvider provider, CancellationToken ct = default)
        {
            var game = await _context.Games.IgnoreQueryFilters()
                .FirstOrDefaultAsync(g => g.CatalogKey == provider.Key || g.Name == provider.GameName, ct);
            if (game == null)
            {
                game = new Game { Name = provider.GameName, Publisher = provider.Publisher, Description = provider.Description };
                _context.Games.Add(game);
            }
            game.CatalogKey = provider.Key;
            await _context.SaveChangesAsync(ct);
            return game;
        }

        public async Task<CatalogImportResult> UpsertAsync(ICatalogProvider provider, CatalogCard card, CancellationToken ct = default)
        {
            var game = await EnsureGameAsync(provider, ct);
            var setCode = string.IsNullOrWhiteSpace(card.SetCode) ? "MISC" : card.SetCode.Trim();
            var set = await _context.CardSets.FirstOrDefaultAsync(s => s.GameId == game.Id && s.Code == setCode, ct);
            if (set == null)
            {
                set = new CardSet { GameId = game.Id, Code = setCode, Name = card.SetName ?? setCode };
                _context.CardSets.Add(set);
                await _context.SaveChangesAsync(ct);
            }
            else if (card.SetName != null && set.Name != card.SetName)
            {
                set.Name = card.SetName;
            }

            var info = await FindAsync(provider.Key, card.ExternalId, ct);
            var isNew = info == null;
            info ??= new CardInfo { CatalogSource = provider.Key, ExternalId = card.ExternalId };
            info.CardSetId = set.Id;
            Apply(card, info);
            if (isNew) _context.CardInfos.Add(info);
            await _context.SaveChangesAsync(ct);
            return new CatalogImportResult(info.Id, isNew);
        }

        /// <summary>Copia i dati comuni nella scheda carta (anche per l'aggiornamento notturno dei prezzi).</summary>
        public static void Apply(CatalogCard card, CardInfo info)
        {
            info.Name = Trim(card.Name, 200)!;
            info.CardNumber = Trim(card.Number, 50);
            info.Rarity = Trim(card.Rarity, 50);
            info.Type = Trim(card.Type, 100);
            info.TypeLine = Trim(card.Subtitle, 200);
            info.Description = Trim(card.Text, 1000);
            info.ImageSmall = card.ImageSmall ?? info.ImageSmall;
            info.ImageNormal = card.ImageLarge ?? info.ImageNormal;
            info.ImageLarge = card.ImageLarge ?? info.ImageLarge;
            info.ImageUrl = card.ImageLarge ?? card.ImageSmall ?? info.ImageUrl;
            info.PriceEur = card.PriceEur;
            info.PriceEurFoil = card.PriceEurFoil;
            info.PriceUsd = card.PriceUsd;
            info.PriceUsdFoil = card.PriceUsdFoil;
            if (card.Data != null) info.GameData = JsonSerializer.Serialize(card.Data);
            info.CatalogUpdatedAt = DateTime.UtcNow;
        }

        private static string? Trim(string? s, int max) => s == null ? null : s.Length > max ? s[..max] : s;
    }

    /// <summary>Base per i provider di giochi nuovi: si implementano solo lettura, ricerca e risoluzione.</summary>
    public abstract class GenericCatalogProvider : ICatalogProvider
    {
        private readonly CatalogStore _store;

        protected GenericCatalogProvider(CatalogStore store)
        {
            _store = store;
        }

        public abstract string Key { get; }
        public abstract string GameName { get; }
        public abstract string Publisher { get; }
        public abstract string Description { get; }
        public abstract string ImportHint { get; }
        public abstract Task<IReadOnlyList<CatalogCard>> SearchAsync(string query, int limit, CancellationToken ct = default);
        public abstract Task<CatalogCard?> ResolveAsync(CatalogLookup lookup, CancellationToken ct = default);
        public abstract Task<CatalogCard?> GetAsync(string externalId, CancellationToken ct = default);

        public async Task<CatalogImportResult?> ImportAsync(string externalId, CancellationToken ct = default)
        {
            var existing = await _store.FindAsync(Key, externalId, ct);
            if (existing?.CatalogUpdatedAt != null && DateTime.UtcNow - existing.CatalogUpdatedAt.Value < CatalogStore.Freshness)
                return new CatalogImportResult(existing.Id, false);
            var card = await GetAsync(externalId, ct);
            if (card == null) return existing != null ? new CatalogImportResult(existing.Id, false) : null;
            return await _store.UpsertAsync(this, card, ct);
        }

        /// <summary>Aggiorna prezzi e dati delle schede indicate (lavoro notturno). Restituisce quante ne ha aggiornate.</summary>
        public virtual async Task<int> RefreshAsync(IReadOnlyList<CardInfo> infos, CancellationToken ct = default)
        {
            var updated = 0;
            foreach (var info in infos.Where(i => i.ExternalId != null))
            {
                ct.ThrowIfCancellationRequested();
                var card = await GetAsync(info.ExternalId!, ct);
                if (card == null) { info.CatalogUpdatedAt = DateTime.UtcNow; continue; }
                CatalogStore.Apply(card, info);
                updated++;
            }
            return updated;
        }
    }
}
