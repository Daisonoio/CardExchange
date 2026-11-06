using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Catalog
{
    public record MirrorStatus(int Cards, int Sets, DateTime? LastMirroredAt);

    /// <summary>
    /// Copia locale del catalogo del gioco di punta (Pokémon): le espansioni recenti e quelle che compaiono in
    /// collezioni e wishlist vengono salvate per intero nel database, così ricerca, aggiunta e import funzionano
    /// anche quando l'API esterna non risponde. Ogni set si rinnova al massimo una volta a settimana.
    /// </summary>
    public class PokemonCatalogMirror
    {
        public const string GameName = "Pokémon TCG";
        /// <summary>Espansioni più recenti sempre presenti (quelle che si scambiano di più).</summary>
        public const int RecentSets = 12;
        /// <summary>Set copiati per esecuzione: il carico sull'API resta basso.</summary>
        public const int SetsPerRun = 6;
        public static readonly TimeSpan RefreshEvery = TimeSpan.FromDays(7);

        private readonly ApplicationDbContext _db;
        private readonly IPokemonTcgService _api;
        private readonly ILogger<PokemonCatalogMirror> _logger;

        public PokemonCatalogMirror(ApplicationDbContext db, IPokemonTcgService api, ILogger<PokemonCatalogMirror> logger)
        {
            _db = db;
            _api = api;
            _logger = logger;
        }

        public async Task<MirrorStatus> StatusAsync(CancellationToken ct = default)
        {
            var game = await _db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Name == GameName, ct);
            if (game == null) return new MirrorStatus(0, 0, null);
            return new MirrorStatus(
                await _db.CardInfos.CountAsync(ci => ci.CardSet.GameId == game.Id && ci.PokemonTcgId != null, ct),
                await _db.CardSets.CountAsync(s => s.GameId == game.Id && s.MirroredAt != null, ct),
                await _db.CardSets.Where(s => s.GameId == game.Id).MaxAsync(s => s.MirroredAt, ct));
        }

        /// <summary>Set da tenere in copia: i più recenti, poi quelli con carte nelle collezioni e wishlist (dai più usati).</summary>
        public async Task<List<string>> WantedSetIdsAsync(CancellationToken ct = default)
        {
            var all = await _api.GetAllSetsAsync();
            var recent = all.Where(s => !string.IsNullOrEmpty(s.Id))
                .OrderByDescending(s => s.ReleaseDate).Take(RecentSets).Select(s => s.Id).ToList();
            var used = await _db.CardInfos.AsNoTracking()
                .Where(ci => ci.CardSet.PokemonTcgId != null
                             && (_db.Cards.Any(c => c.CardInfoId == ci.Id) || _db.WishlistItems.Any(w => w.CardInfoId == ci.Id)))
                .GroupBy(ci => ci.CardSet.PokemonTcgId!)
                .Select(g => new { SetId = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Select(g => g.SetId)
                .ToListAsync(ct);
            return recent.Concat(used).Distinct().ToList();
        }

        public async Task<int> RunAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var wanted = await WantedSetIdsAsync(ct);
            if (wanted.Count == 0) return 0;
            var fresh = (await _db.CardSets.AsNoTracking()
                    .Where(s => s.PokemonTcgId != null && s.MirroredAt != null && s.MirroredAt > nowUtc - RefreshEvery)
                    .Select(s => s.PokemonTcgId!).ToListAsync(ct)).ToHashSet();
            var done = 0;
            foreach (var setId in wanted.Where(id => !fresh.Contains(id)).Take(SetsPerRun))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var cards = await MirrorSetAsync(setId, nowUtc, ct);
                    if (cards >= 0) done++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Copia locale del set Pokémon {SetId} non riuscita", setId);
                }
            }
            if (done > 0) _logger.LogInformation("Copia locale Pokémon: {Count} set aggiornati", done);
            return done;
        }

        /// <summary>Copia tutte le carte di un set; -1 se l'API non risponde (si riprova alla prossima esecuzione).</summary>
        public async Task<int> MirrorSetAsync(string setId, DateTime nowUtc, CancellationToken ct = default)
        {
            var game = await _db.Games.FirstOrDefaultAsync(g => g.Name == GameName, ct);
            if (game == null)
            {
                game = new Game { Name = GameName, Publisher = "The Pokémon Company", Description = "Il gioco di carte collezionabili dei Pokémon, il TCG più venduto al mondo", CatalogKey = "pokemon" };
                _db.Games.Add(game);
                await _db.SaveChangesAsync(ct);
            }
            var set = await _db.CardSets.FirstOrDefaultAsync(s => s.PokemonTcgId == setId && s.GameId == game.Id, ct);
            if (set == null)
            {
                var apiSet = await _api.GetSetByIdAsync(setId);
                if (apiSet == null) return -1;
                set = new CardSet { GameId = game.Id };
                _api.MapToCardSet(apiSet, set);
                _db.CardSets.Add(set);
                await _db.SaveChangesAsync(ct);
            }

            var existing = await _db.CardInfos.Where(ci => ci.CardSetId == set.Id && ci.PokemonTcgId != null)
                .ToDictionaryAsync(ci => ci.PokemonTcgId!, ct);
            int page = 1, count = 0;
            while (true)
            {
                var result = await _api.GetCardsBySetAsync(setId, page, 250);
                if (result?.Data == null) return page == 1 ? -1 : count;
                if (result.Data.Count == 0) break;
                foreach (var apiCard in result.Data)
                {
                    if (!existing.TryGetValue(apiCard.Id, out var info))
                    {
                        info = new CardInfo { CardSetId = set.Id };
                        _db.CardInfos.Add(info);
                        existing[apiCard.Id] = info;
                    }
                    _api.MapToCardInfo(apiCard, info);
                    count++;
                }
                await _db.SaveChangesAsync(ct);
                var total = result.TotalCount ?? 0;
                if (page * (result.PageSize ?? 250) >= total) break;
                page++;
            }
            set.MirroredAt = nowUtc;
            await _db.SaveChangesAsync(ct);
            return count;
        }
    }
}
