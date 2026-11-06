using System.Globalization;
using System.Text.RegularExpressions;
using CardExchange.API.DTOs.OnePieceTcg;
using CardExchange.API.DTOs.PokemonTcg;
using CardExchange.API.DTOs.YuGiOh;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Catalog
{
    /// <summary>
    /// Adattatori dei giochi storici (colonne dedicate già esistenti) all'interfaccia comune dei provider.
    /// Contengono la logica di import che prima stava nei controller, così la usano anche l'import CSV e il catalogo.
    /// </summary>
    public abstract class LegacyProviderBase
    {
        protected static readonly TimeSpan Freshness = TimeSpan.FromHours(24);
        protected readonly ApplicationDbContext Context;

        protected LegacyProviderBase(ApplicationDbContext context)
        {
            Context = context;
        }

        protected static bool IsFresh(DateTime? updatedAt) => updatedAt.HasValue && DateTime.UtcNow - updatedAt.Value < Freshness;

        protected async Task<Game> EnsureGameAsync(string key, string name, string publisher, string description)
        {
            var game = await Context.Games.IgnoreQueryFilters().FirstOrDefaultAsync(g => g.CatalogKey == key || g.Name == name);
            if (game == null)
            {
                game = new Game { Name = name, Publisher = publisher, Description = description };
                Context.Games.Add(game);
            }
            game.CatalogKey = key;
            await Context.SaveChangesAsync();
            return game;
        }

        /// <summary>"4/102" → "4", "007" → "7" (solo se numerico), "TG01" invariato.</summary>
        protected static string NormalizeNumber(string number)
        {
            var n = number.Split('/')[0].Trim();
            return n.All(char.IsDigit) ? n.TrimStart('0') is { Length: > 0 } t ? t : "0" : n;
        }
    }

    // ------------------------------------------------------------------ Pokémon

    public class PokemonCatalogProvider : LegacyProviderBase, ICatalogProvider
    {
        private readonly IPokemonTcgService _api;

        public PokemonCatalogProvider(ApplicationDbContext context, IPokemonTcgService api) : base(context)
        {
            _api = api;
        }

        public string Key => "pokemon";
        public string GameName => "Pokémon TCG";
        public string Publisher => "The Pokémon Company";
        public string Description => "Il gioco di carte collezionabili dei Pokémon, il TCG più venduto al mondo";
        public string ImportHint => "codice del set e numero (es. sv1 / 25 oppure SVI / 25), oppure il nome";

        public async Task<IReadOnlyList<CatalogCard>> SearchAsync(string query, int limit, CancellationToken ct = default)
        {
            var res = await SearchWithRetryAsync($"name:\"{query.Replace("\"", "")}*\"", 1, limit);
            return res?.Data?.Select(Map).ToList() ?? new List<CatalogCard>();
        }

        /// <summary>Il catalogo Pokémon a volte risponde con un errore temporaneo (500): si riprova una volta.</summary>
        private async Task<PokemonTcgResponse<List<PokemonTcgCard>>?> SearchWithRetryAsync(string query, int page, int pageSize)
        {
            var res = await _api.SearchCardsAsync(query, page, pageSize);
            if (res != null) return res;
            await Task.Delay(400);
            return await _api.SearchCardsAsync(query, page, pageSize);
        }

        /// <summary>
        /// Prima la copia locale del catalogo (veloce e disponibile anche se l'API non risponde):
        /// set + numero, oppure nome + numero + totale stampato ("58/102").
        /// </summary>
        private async Task<CatalogCard?> ResolveLocalAsync(CatalogLookup lookup, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(lookup.Number)) return null;
            var parts = lookup.Number.Split('/');
            var number = NormalizeNumber(parts[0]);
            var cards = Context.CardInfos.AsNoTracking().Include(ci => ci.CardSet).Where(ci => ci.PokemonTcgId != null);
            if (!string.IsNullOrWhiteSpace(lookup.SetCode))
            {
                var id = $"{lookup.SetCode.Trim().ToLowerInvariant()}-{number}";
                var bySet = await cards.FirstOrDefaultAsync(ci => ci.PokemonTcgId == id, ct);
                if (bySet != null) return MapLocal(bySet);
            }
            if (!string.IsNullOrWhiteSpace(lookup.Name) && parts.Length > 1 && int.TryParse(parts[1].Trim(), out var total))
            {
                var name = lookup.Name.Trim().ToLower();
                var hits = await cards.Where(ci => ci.Name.ToLower() == name && ci.CardSet.PrintedTotal == total).ToListAsync(ct);
                hits = hits.Where(h => NormalizeNumber(h.CardNumber ?? "") == number).ToList();
                if (hits.Count == 1) return MapLocal(hits[0]);
            }
            return null;
        }

        private static CatalogCard MapLocal(CardInfo ci) => new()
        {
            ExternalId = ci.PokemonTcgId!,
            Name = ci.Name,
            SetCode = ci.CardSet?.PokemonTcgId,
            SetName = ci.CardSet?.Name,
            Number = ci.CardNumber,
            Rarity = ci.Rarity,
            ImageSmall = ci.ImageSmall,
            ImageLarge = ci.ImageLarge ?? ci.ImageSmall,
            PriceEur = ci.PriceEur
        };

        public async Task<CatalogCard?> ResolveAsync(CatalogLookup lookup, CancellationToken ct = default)
        {
            var local = await ResolveLocalAsync(lookup, ct);
            if (local != null) return local;

            if (!string.IsNullOrWhiteSpace(lookup.SetCode) && !string.IsNullOrWhiteSpace(lookup.Number))
            {
                var set = lookup.SetCode.Trim().Replace("\"", "");
                var number = NormalizeNumber(lookup.Number);
                foreach (var q in new[] { $"set.id:\"{set}\" number:\"{number}\"", $"set.ptcgoCode:\"{set}\" number:\"{number}\"" })
                {
                    var hit = (await SearchWithRetryAsync(q, 1, 1))?.Data?.FirstOrDefault();
                    if (hit != null) return Map(hit);
                }
            }
            // Collectr, TCGplayer: nome e "199/165" con il nome commerciale dell'espansione ("Base Set", "Scarlet & Violet 151"),
            // che spesso non coincide con quello del catalogo: decide il totale stampato sulla carta
            if (!string.IsNullOrWhiteSpace(lookup.Number))
            {
                var parts = lookup.Number.Split('/');
                var number = NormalizeNumber(parts[0]);
                int? printedTotal = parts.Length > 1 && int.TryParse(parts[1].Trim(), out var t) ? t : null;
                string? q = !string.IsNullOrWhiteSpace(lookup.Name)
                    ? $"name:\"{lookup.Name.Trim().Replace("\"", "")}\" number:\"{number}\""
                    : !string.IsNullOrWhiteSpace(lookup.SetName) ? $"set.name:\"{lookup.SetName.Trim().Replace("\"", "")}\" number:\"{number}\"" : null;
                if (q != null)
                {
                    var hits = (await SearchWithRetryAsync(q, 1, 30))?.Data ?? new List<PokemonTcgCard>();
                    var best = PickPrint(hits, printedTotal, lookup.SetName);
                    if (best != null) return Map(best);
                }
            }
            if (string.IsNullOrWhiteSpace(lookup.Name)) return null;
            var nameQuery = $"name:\"{lookup.Name.Trim().Replace("\"", "")}\"";
            if (!string.IsNullOrWhiteSpace(lookup.SetName)) nameQuery += $" set.name:\"{lookup.SetName.Trim().Replace("\"", "")}\"";
            // Se il file indica un numero, il solo nome non basta: meglio nessun risultato che una stampa sbagliata
            if (!string.IsNullOrWhiteSpace(lookup.Number))
            {
                var wanted = NormalizeNumber(lookup.Number);
                var hits = (await SearchWithRetryAsync(nameQuery, 1, 50))?.Data ?? new List<PokemonTcgCard>();
                var sameNumber = hits.Where(h => NormalizeNumber(h.Number ?? "") == wanted).ToList();
                var setCode = lookup.SetCode?.Trim();
                var pick = sameNumber.FirstOrDefault(h => setCode != null && (string.Equals(h.Set?.Id, setCode, StringComparison.OrdinalIgnoreCase)
                                                                              || string.Equals(h.Set?.PtcgoCode, setCode, StringComparison.OrdinalIgnoreCase)))
                           ?? (sameNumber.Count == 1 ? sameNumber[0] : null);
                return pick == null ? null : Map(pick);
            }
            var byName = (await SearchWithRetryAsync(nameQuery, 1, 1))?.Data?.FirstOrDefault();
            return byName == null ? null : Map(byName);
        }

        /// <summary>
        /// Tra le stampe con lo stesso nome e numero sceglie quella con il totale stampato indicato ("58/102" → set da 102),
        /// poi quella con il nome dell'espansione più simile; se resta ambiguità non sceglie.
        /// </summary>
        public static PokemonTcgCard? PickPrint(IReadOnlyList<PokemonTcgCard> hits, int? printedTotal, string? setName)
        {
            IEnumerable<PokemonTcgCard> pool = hits;
            if (printedTotal.HasValue)
            {
                var byTotal = hits.Where(h => h.Set?.PrintedTotal == printedTotal || h.Set?.Total == printedTotal).ToList();
                if (byTotal.Count > 0) pool = byTotal;
                else if (hits.Count > 1) return null;
            }
            var list = pool.ToList();
            if (list.Count <= 1) return list.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(setName))
            {
                static string Simple(string s) => new string(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
                var wanted = Simple(setName);
                var exact = list.Where(h => h.Set?.Name != null && Simple(h.Set.Name) == wanted).ToList();
                if (exact.Count == 1) return exact[0];
                var partial = list.Where(h => h.Set?.Name != null && (wanted.EndsWith(Simple(h.Set.Name)) || Simple(h.Set.Name).EndsWith(wanted))).ToList();
                if (partial.Count == 1) return partial[0];
            }
            return null;
        }

        public async Task<CatalogImportResult?> ImportAsync(string externalId, CancellationToken ct = default)
        {
            var existing = await Context.CardInfos.FirstOrDefaultAsync(ci => ci.PokemonTcgId == externalId, ct);
            if (existing != null && IsFresh(existing.PokemonTcgUpdatedAt)) return new CatalogImportResult(existing.Id, false);

            var apiCard = await _api.GetCardByIdAsync(externalId);
            if (apiCard == null) return existing != null ? new CatalogImportResult(existing.Id, false) : null;
            if (apiCard.Set == null) throw new InvalidOperationException("La carta non ha informazioni sul set");

            var game = await EnsureGameAsync(Key, GameName, Publisher, Description);
            var set = await Context.CardSets.FirstOrDefaultAsync(cs => cs.PokemonTcgId == apiCard.Set.Id && cs.GameId == game.Id, ct);
            if (set == null)
            {
                set = new CardSet { GameId = game.Id };
                Context.CardSets.Add(set);
            }
            _api.MapToCardSet(apiCard.Set, set);
            await Context.SaveChangesAsync(ct);

            var info = existing ?? new CardInfo { CardSetId = set.Id };
            _api.MapToCardInfo(apiCard, info);
            if (existing == null) Context.CardInfos.Add(info);
            await Context.SaveChangesAsync(ct);
            return new CatalogImportResult(info.Id, existing == null);
        }

        public static CatalogCard Map(PokemonTcgCard c) => new()
        {
            ExternalId = c.Id,
            Name = c.Name,
            Subtitle = string.Join(" — ", new[] { c.Supertype }.Concat(c.Subtypes ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)))
                       + (c.Hp != null ? $" · PS {c.Hp}" : ""),
            SetCode = c.Set?.Id,
            SetName = c.Set?.Name,
            Number = c.Number,
            Rarity = c.Rarity,
            ImageSmall = c.Images?.Small,
            ImageLarge = c.Images?.Large ?? c.Images?.Small,
            PriceEur = c.Cardmarket?.Prices?.AverageSellPrice ?? c.Cardmarket?.Prices?.TrendPrice
        };
    }

    // ------------------------------------------------------------------ Yu-Gi-Oh!

    public class YuGiOhCatalogProvider : LegacyProviderBase, ICatalogProvider
    {
        private static readonly Regex PrintCode = new(@"^[A-Z0-9]{2,6}-[A-Z]{0,2}\d{2,4}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private readonly IYuGiOhService _api;

        public YuGiOhCatalogProvider(ApplicationDbContext context, IYuGiOhService api) : base(context)
        {
            _api = api;
        }

        public string Key => "yugioh";
        public string GameName => "Yu-Gi-Oh!";
        public string Publisher => "Konami";
        public string Description => "Il gioco di carte collezionabili basato sul manga e anime Yu-Gi-Oh!";
        public string ImportHint => "codice di stampa (es. LOB-EN001) oppure il nome esatto in inglese";

        public async Task<IReadOnlyList<CatalogCard>> SearchAsync(string query, int limit, CancellationToken ct = default) =>
            (await _api.SearchCardsAsync(query, 0, limit)).Select(c => Map(c)).ToList();

        public async Task<CatalogCard?> ResolveAsync(CatalogLookup lookup, CancellationToken ct = default)
        {
            // Il codice di stampa può arrivare nella colonna del numero o in quella del set
            var code = new[] { lookup.Number, lookup.SetCode }
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v) && PrintCode.IsMatch(v.Trim()));
            if (code == null && !string.IsNullOrWhiteSpace(lookup.SetCode) && !string.IsNullOrWhiteSpace(lookup.Number)
                && PrintCode.IsMatch($"{lookup.SetCode.Trim()}-{lookup.Number.Trim()}"))
                code = $"{lookup.SetCode.Trim()}-{lookup.Number.Trim()}";
            if (code != null)
            {
                code = code.Trim().ToUpperInvariant();
                var byCode = await _api.GetCardBySetCodeAsync(code);
                if (byCode != null) return Map(byCode, code);
            }
            if (string.IsNullOrWhiteSpace(lookup.Name)) return null;
            var byName = await _api.GetCardByNameAsync(lookup.Name);
            return byName == null ? null : Map(byName);
        }

        public async Task<CatalogImportResult?> ImportAsync(string externalId, CancellationToken ct = default)
        {
            if (!int.TryParse(externalId, out var passcode)) return null;
            var existing = await Context.CardInfos.FirstOrDefaultAsync(ci => ci.YuGiOhId == passcode, ct);
            if (existing != null && IsFresh(existing.YuGiOhUpdatedAt)) return new CatalogImportResult(existing.Id, false);

            var apiCard = await _api.GetCardByIdAsync(passcode);
            if (apiCard == null) return existing != null ? new CatalogImportResult(existing.Id, false) : null;

            var game = await EnsureGameAsync(Key, GameName, Publisher, Description);
            var firstSet = apiCard.CardSets?.FirstOrDefault();
            var setCode = firstSet?.SetCode ?? "MISC";
            var set = await Context.CardSets.FirstOrDefaultAsync(cs => cs.YuGiOhSetCode == setCode && cs.GameId == game.Id, ct);
            if (set == null)
            {
                set = new CardSet
                {
                    GameId = game.Id,
                    Name = firstSet?.SetName ?? "Miscellaneous",
                    Code = setCode.ToUpperInvariant(),
                    YuGiOhSetCode = setCode,
                    YuGiOhUpdatedAt = DateTime.UtcNow
                };
                Context.CardSets.Add(set);
                await Context.SaveChangesAsync(ct);
            }

            var info = existing ?? new CardInfo { CardSetId = set.Id };
            _api.MapToCardInfo(apiCard, info);
            if (existing == null) Context.CardInfos.Add(info);
            await Context.SaveChangesAsync(ct);
            return new CatalogImportResult(info.Id, existing == null);
        }

        public static CatalogCard Map(YuGiOhCard c, string? printCode = null)
        {
            var print = printCode != null
                ? c.CardSets?.FirstOrDefault(s => string.Equals(s.SetCode, printCode, StringComparison.OrdinalIgnoreCase))
                : null;
            print ??= c.CardSets?.FirstOrDefault();
            var image = c.CardImages?.FirstOrDefault();
            return new CatalogCard
            {
                ExternalId = c.Id.ToString(CultureInfo.InvariantCulture),
                Name = c.Name,
                Subtitle = string.Join(" · ", new[] { c.Type, c.Race }.Where(s => !string.IsNullOrWhiteSpace(s))),
                SetCode = print?.SetCode,
                SetName = print?.SetName,
                Number = print?.SetCode,
                Rarity = print?.SetRarity,
                ImageSmall = image?.ImageUrlSmall,
                ImageLarge = image?.ImageUrl,
                PriceEur = decimal.TryParse(c.CardPrices?.FirstOrDefault()?.CardmarketPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out var p) && p > 0 ? p : null
            };
        }
    }

    // ------------------------------------------------------------------ One Piece

    public class OnePieceCatalogProvider : LegacyProviderBase, ICatalogProvider
    {
        private static readonly Regex CardCode = new(@"^(OP|ST|EB|PRB|P)\d{0,2}-\d{3}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private readonly IOnePieceTcgService _api;

        public OnePieceCatalogProvider(ApplicationDbContext context, IOnePieceTcgService api) : base(context)
        {
            _api = api;
        }

        public string Key => "onepiece";
        public string GameName => "One Piece TCG";
        public string Publisher => "Bandai";
        public string Description => "Il gioco di carte collezionabili basato sul manga e anime One Piece";
        public string ImportHint => "codice della carta (es. OP01-001), oppure set + numero, oppure il nome";

        public async Task<IReadOnlyList<CatalogCard>> SearchAsync(string query, int limit, CancellationToken ct = default) =>
            ((await _api.SearchCardsAsync(query))?.Data ?? new List<OnePieceCard>()).Take(limit).Select(Map).ToList();

        public async Task<CatalogCard?> ResolveAsync(CatalogLookup lookup, CancellationToken ct = default)
        {
            string? code = new[] { lookup.Number, lookup.SetCode }.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v) && CardCode.IsMatch(v.Trim()))?.Trim();
            if (code == null && !string.IsNullOrWhiteSpace(lookup.SetCode) && !string.IsNullOrWhiteSpace(lookup.Number)
                && int.TryParse(lookup.Number.Split('/')[0].Trim(), out var n))
            {
                var candidate = $"{lookup.SetCode.Trim().ToUpperInvariant()}-{n:000}";
                if (CardCode.IsMatch(candidate)) code = candidate;
            }
            if (code != null)
            {
                var byCode = await _api.GetCardByCodeAsync(code.ToUpperInvariant());
                if (byCode != null) return Map(byCode);
            }
            if (string.IsNullOrWhiteSpace(lookup.Name)) return null;
            var hits = (await _api.SearchCardsAsync(lookup.Name.Trim()))?.Data ?? new List<OnePieceCard>();
            var exact = hits.FirstOrDefault(c => string.Equals(c.Name, lookup.Name.Trim(), StringComparison.OrdinalIgnoreCase));
            return exact == null ? null : Map(exact);
        }

        public async Task<CatalogImportResult?> ImportAsync(string externalId, CancellationToken ct = default)
        {
            var existing = await Context.CardInfos.FirstOrDefaultAsync(ci => ci.OnePieceTcgId == externalId, ct);
            if (existing != null && IsFresh(existing.OnePieceTcgUpdatedAt)) return new CatalogImportResult(existing.Id, false);

            var apiCard = await _api.GetCardByCodeAsync(externalId);
            if (apiCard == null) return existing != null ? new CatalogImportResult(existing.Id, false) : null;

            var game = await EnsureGameAsync(Key, GameName, Publisher, Description);
            var dash = apiCard.Code.IndexOf('-');
            var setCode = (dash > 0 ? apiCard.Code[..dash] : apiCard.Code).ToUpperInvariant();
            var set = await Context.CardSets.FirstOrDefaultAsync(cs => cs.Code == setCode && cs.GameId == game.Id, ct);
            if (set == null)
            {
                set = new CardSet { GameId = game.Id, Name = apiCard.Set?.Name ?? setCode, Code = setCode };
                Context.CardSets.Add(set);
            }
            if (apiCard.Set?.Name != null)
            {
                set.OnePieceTcgSetName = apiCard.Set.Name;
                set.OnePieceTcgUpdatedAt = DateTime.UtcNow;
            }
            await Context.SaveChangesAsync(ct);

            var info = existing ?? new CardInfo { CardSetId = set.Id };
            _api.MapToCardInfo(apiCard, info);
            if (existing == null) Context.CardInfos.Add(info);
            await Context.SaveChangesAsync(ct);
            return new CatalogImportResult(info.Id, existing == null);
        }

        public static CatalogCard Map(OnePieceCard c) => new()
        {
            ExternalId = c.Code,
            Name = c.Name,
            Subtitle = string.Join(" — ", new[] { c.Type, c.Color, c.Cost != null ? $"Costo {c.Cost}" : null }.Where(s => !string.IsNullOrWhiteSpace(s))),
            SetCode = c.Code.Contains('-') ? c.Code[..c.Code.IndexOf('-')] : c.Code,
            SetName = c.Set?.Name,
            Number = c.Code.Split('_')[0],
            Rarity = c.Rarity,
            ImageSmall = c.Images?.Small ?? c.Images?.Large,
            ImageLarge = c.Images?.Large ?? c.Images?.Small,
            PriceEur = c.PriceEur,
            PriceConverted = c.PriceEur.HasValue
        };
    }
}
