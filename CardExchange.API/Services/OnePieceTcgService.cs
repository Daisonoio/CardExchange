using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using CardExchange.API.DTOs.OnePieceTcg;
using CardExchange.API.Services.Catalog;
using CardExchange.Core.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace CardExchange.API.Services
{
    /// <summary>
    /// Catalogo One Piece TCG tramite OPTCG API (optcgapi.com): gratuita, senza chiave, con set principali,
    /// starter deck e promo, immagini e prezzi di mercato in dollari (convertiti in euro al cambio BCE).
    /// Il codice di una carta è quello della singola stampa: "OP01-001" (base), "OP01-001_p1" (parallel), ecc.
    /// </summary>
    public class OnePieceTcgService : IOnePieceTcgService
    {
        public const string BaseUrl = "https://optcgapi.com";

        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly IExchangeRates _fx;
        private readonly ILogger<OnePieceTcgService> _logger;
        private readonly string? _imageTemplate;

        // Servizio gratuito: richieste distanziate
        private static readonly SemaphoreSlim _rateLimiter = new(1, 1);
        private static DateTime _lastRequestTime = DateTime.MinValue;
        private const int MinDelayMs = 150;
        private const int MaxSearchResults = 120;
        private const int PageSize = 40;
        private static readonly Regex CodeQuery = new(@"^(OP|ST|EB|PRB)\d{2}-\d{3}$|^P-\d{3}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Variant = new(@"\s*\([^)]*\)\s*$", RegexOptions.Compiled);

        // Le tre raccolte dell'API: set principali/extra, starter deck, promo
        private static readonly string[] Collections = { "sets", "decks", "promos" };

        private static readonly Regex PrintNumber = new(@"\s*\(\d{3}\)", RegexOptions.Compiled);
        private static readonly Regex StarterCode = new(@"^ST\d", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PromoCode = new(@"^P-", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public OnePieceTcgService(HttpClient httpClient, IMemoryCache cache, IExchangeRates fx, ILogger<OnePieceTcgService> logger,
            IConfiguration? configuration = null)
        {
            _imageTemplate = CardExchange.API.Services.Catalog.OnePieceImageSource.Template(configuration);
            _httpClient = httpClient;
            _cache = cache;
            _fx = fx;
            _logger = logger;
            _httpClient.BaseAddress ??= new Uri(BaseUrl);
        }

        // ================================================================
        // Lookup
        // ================================================================

        public async Task<OnePieceCard?> GetCardByCodeAsync(string code)
        {
            code = code.Trim();
            var cacheKey = $"optcg:card:{code.ToUpperInvariant()}";
            if (_cache.TryGetValue(cacheKey, out OnePieceCard? cached)) return cached;

            // "OP01-001_p1" -> si chiede "OP01-001" e si sceglie la stampa giusta
            var prints = await GetPrintsAsync(code.Split('_')[0].ToUpperInvariant());
            var card = prints?.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));
            if (card != null) _cache.Set(cacheKey, card, TimeSpan.FromHours(6));
            return card;
        }

        public async Task<OnePieceResponse?> SearchCardsAsync(string query, int page = 1)
        {
            query = query.Trim();
            var cacheKey = $"optcg:search:{query.ToLowerInvariant()}";
            if (!_cache.TryGetValue(cacheKey, out List<OnePieceCard>? all) || all == null)
            {
                // Ricerca per codice ("OP01-024"): tutte le stampe di quella carta
                if (CodeQuery.IsMatch(query))
                {
                    var prints = await GetPrintsAsync(query.ToUpperInvariant());
                    if (prints == null) return null;
                    all = prints.OrderBy(c => c.Code.Contains('_') ? 1 : 0).ThenBy(c => c.Code, StringComparer.OrdinalIgnoreCase).ToList();
                    _cache.Set(cacheKey, all, TimeSpan.FromHours(1));
                    return Page(all, page);
                }

                var rate = await _fx.UsdToEurAsync();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                all = new List<OnePieceCard>();
                var anyAnswer = false;
                foreach (var collection in Collections)
                {
                    var prints = await GetListAsync($"/api/{collection}/filtered/?card_name={Uri.EscapeDataString(query)}");
                    if (prints == null) continue;
                    anyAnswer = true;
                    // La stessa stampa può comparire in più raccolte: vale la prima
                    foreach (var p in prints.Where(p => !string.IsNullOrWhiteSpace(p.CardImageId) && seen.Add(p.CardImageId!)))
                        all.Add(Map(p, rate, _imageTemplate));
                }
                if (!anyAnswer) return null;

                // Prima il nome esatto, poi i nomi con una parola che inizia come la ricerca
                // ("luffy" -> "Monkey.D.Luffy"), i personaggi prima degli eventi; poi per codice, base prima delle varianti
                all = all
                    .OrderBy(c => NameRank(c.Name, query))
                    .ThenBy(c => TypeRank(c.Type))
                    .ThenBy(c => c.Code.Split('_')[0], StringComparer.OrdinalIgnoreCase)
                    .ThenBy(c => c.Code.Contains('_') ? 1 : 0)
                    .ThenBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                    .Take(MaxSearchResults)
                    .ToList();
                _cache.Set(cacheKey, all, TimeSpan.FromHours(1));
            }

            // L'aggiunta subito dopo la ricerca non richiama l'API
            foreach (var card in all) _cache.Set($"optcg:card:{card.Code.ToUpperInvariant()}", card, TimeSpan.FromHours(6));

            return Page(all, page);
        }

        private static OnePieceResponse Page(List<OnePieceCard> all, int page)
        {
            page = Math.Max(1, page);
            return new OnePieceResponse
            {
                Data = all.Skip((page - 1) * PageSize).Take(PageSize).ToList(),
                Page = page,
                Limit = PageSize,
                Total = all.Count,
                TotalPages = (int)Math.Ceiling(all.Count / (double)PageSize)
            };
        }

        public static int NameRank(string name, string query)
        {
            var bare = Variant.Replace(name, "").Trim();
            if (string.Equals(bare, query, StringComparison.OrdinalIgnoreCase)) return 0;
            var words = Regex.Split(bare, @"[^\p{L}\p{N}]+");
            if (words.Any(w => w.StartsWith(query, StringComparison.OrdinalIgnoreCase))) return 1;
            return 2;
        }

        private static int TypeRank(string? type) => type?.ToUpperInvariant() switch
        {
            "LEADER" => 0,
            "CHARACTER" => 1,
            "EVENT" => 2,
            "STAGE" => 3,
            _ => 4
        };

        /// <summary>Tutte le stampe di un codice base (cerca nelle raccolte adatte); null se il servizio non risponde.</summary>
        private async Task<List<OnePieceCard>?> GetPrintsAsync(string baseCode)
        {
            var order = PromoCode.IsMatch(baseCode) ? new[] { "promos" }
                : StarterCode.IsMatch(baseCode) ? new[] { "decks", "promos" }
                : new[] { "sets", "promos", "decks" };
            var rate = await _fx.UsdToEurAsync();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<OnePieceCard>();
            var anyAnswer = false;
            foreach (var collection in order)
            {
                var prints = await GetListAsync($"/api/{collection}/card/{Uri.EscapeDataString(baseCode)}/");
                if (prints == null) continue;
                anyAnswer = true;
                foreach (var p in prints.Where(p => !string.IsNullOrWhiteSpace(p.CardImageId) && seen.Add(p.CardImageId!)))
                    result.Add(Map(p, rate, _imageTemplate));
            }
            return anyAnswer ? result : null;
        }

        // ================================================================
        // Mapping
        // ================================================================

        /// <summary>Formato OPTCG API -> formato interno (usato da controller, catalogo e import).</summary>
        public static OnePieceCard Map(OptcgApiCard c, decimal usdToEur, string? imageTemplate = null)
        {
            var code = (c.CardImageId ?? c.CardSetId ?? "").Trim();
            // "Roronoa Zoro (001) (Parallel)" -> "Roronoa Zoro (Parallel)": il numero è già nel codice
            var name = PrintNumber.Replace(c.CardName ?? code, "").Trim();
            var usd = c.MarketPrice ?? c.InventoryPrice;
            return new OnePieceCard
            {
                Code = code,
                Name = string.IsNullOrWhiteSpace(name) ? code : name,
                Type = c.CardType,
                Rarity = c.Rarity,
                Cost = OptcgApiCard.ToInt(c.CardCost),
                Power = OptcgApiCard.ToInt(c.CardPower),
                Counter = OptcgApiCard.ToInt(c.CounterAmount),
                Color = c.CardColor,
                Family = c.SubTypes,
                Ability = c.CardText,
                Attribute = string.IsNullOrWhiteSpace(c.Attribute) ? null : new OnePieceAttribute { Name = c.Attribute },
                // Sorgente configurata (immagini senza filigrana, con i diritti verificati), altrimenti quella dell'API
                Images = CardExchange.API.Services.Catalog.OnePieceImageSource.UrlFor(code, imageTemplate) is { } licensed
                    ? new OnePieceImages { Small = licensed, Large = licensed }
                    : string.IsNullOrWhiteSpace(c.CardImage) ? null : new OnePieceImages { Small = c.CardImage, Large = c.CardImage },
                Set = new OnePieceSetInfo { Name = c.SetName },
                PriceUsd = usd,
                PriceEur = usd.HasValue ? Math.Round(usd.Value * usdToEur, 2) : null
            };
        }

        public void MapToCardInfo(OnePieceCard src, CardInfo dest)
        {
            dest.OnePieceTcgId = src.Code;
            dest.Name = src.Name;
            dest.CardNumber = src.Code.Split('_')[0];
            dest.Rarity = src.Rarity;
            dest.Type = src.Type;
            dest.OnePieceColor = src.Color;
            dest.OnePieceCost = src.Cost;
            dest.OnePiecePower = src.Power;
            dest.OnePieceCounter = src.Counter;
            dest.OnePieceFamily = src.Family;
            dest.OnePieceAbility = src.Ability;
            dest.OnePieceTrigger = src.Trigger;
            dest.Description = src.Ability;

            if (src.Images != null)
            {
                dest.OnePieceImageSmall = src.Images.Small;
                dest.OnePieceImageLarge = src.Images.Large;
                dest.ImageUrl = src.Images.Large ?? src.Images.Small;
                // Con la sorgente configurata le immagini si sostituiscono sempre; altrimenti si tengono quelle già salvate
                if (_imageTemplate != null) { dest.ImageSmall = src.Images.Small; dest.ImageLarge = src.Images.Large; dest.ImageNormal = src.Images.Large; }
                else { dest.ImageSmall ??= src.Images.Small; dest.ImageLarge ??= src.Images.Large; }
            }

            if (src.PriceUsd.HasValue) dest.PriceUsd = src.PriceUsd;
            if (src.PriceEur.HasValue) dest.PriceEur = src.PriceEur;

            dest.OnePieceTcgUpdatedAt = DateTime.UtcNow;
        }

        // ================================================================
        // HTTP
        // ================================================================

        /// <summary>Elenco di stampe; null se il servizio non risponde, lista vuota se non trova nulla.</summary>
        private async Task<List<OptcgApiCard>?> GetListAsync(string path)
        {
            await RateLimitAsync();
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    using var response = await _httpClient.GetAsync(path);
                    // L'API risponde 404 quando non trova carte
                    if (response.StatusCode == HttpStatusCode.NotFound) return new List<OptcgApiCard>();
                    if ((int)response.StatusCode >= 500 && attempt == 1) { await Task.Delay(400); continue; }
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("OPTCG API GET {Path} -> {StatusCode}", path, response.StatusCode);
                        return null;
                    }
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<OptcgApiCard>>(json, _jsonOptions) ?? new List<OptcgApiCard>();
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
                {
                    if (attempt == 2)
                    {
                        _logger.LogWarning(ex, "OPTCG API GET {Path} non riuscita", path);
                        return null;
                    }
                    await Task.Delay(400);
                }
            }
            return null;
        }

        private static async Task RateLimitAsync()
        {
            await _rateLimiter.WaitAsync();
            try
            {
                var elapsed = (DateTime.UtcNow - _lastRequestTime).TotalMilliseconds;
                if (elapsed < MinDelayMs) await Task.Delay(MinDelayMs - (int)elapsed);
                _lastRequestTime = DateTime.UtcNow;
            }
            finally
            {
                _rateLimiter.Release();
            }
        }
    }
}
