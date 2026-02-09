using CardExchange.API.DTOs.Scryfall;
using CardExchange.Core.Entities;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Text;
using System.Text.Json;

namespace CardExchange.API.Services
{
    public class ScryfallService : IScryfallService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ScryfallService> _logger;

        // Scryfall richiede 50-100ms tra richieste (max 10 req/s)
        private static readonly SemaphoreSlim _rateLimiter = new(1, 1);
        private static DateTime _lastRequestTime = DateTime.MinValue;
        private const int MinDelayMs = 100; // 10 req/s

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ScryfallService(HttpClient httpClient, IMemoryCache cache, ILogger<ScryfallService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
        }

        // ================================================================
        // Lookup
        // ================================================================

        public async Task<ScryfallCard?> GetCardByNameAsync(string exactName)
        {
            var cacheKey = $"scryfall:card:name:{exactName.ToLowerInvariant()}";
            if (_cache.TryGetValue(cacheKey, out ScryfallCard? cached))
                return cached;

            var encoded = Uri.EscapeDataString(exactName);
            var card = await GetAsync<ScryfallCard>($"/cards/named?exact={encoded}");

            if (card != null)
                _cache.Set(cacheKey, card, TimeSpan.FromHours(24));

            return card;
        }

        public async Task<ScryfallCard?> GetCardBySetAndNumberAsync(string setCode, string collectorNumber)
        {
            var cacheKey = $"scryfall:card:{setCode}/{collectorNumber}";
            if (_cache.TryGetValue(cacheKey, out ScryfallCard? cached))
                return cached;

            var card = await GetAsync<ScryfallCard>($"/cards/{Uri.EscapeDataString(setCode)}/{Uri.EscapeDataString(collectorNumber)}");

            if (card != null)
                _cache.Set(cacheKey, card, TimeSpan.FromHours(24));

            return card;
        }

        public async Task<ScryfallCard?> GetCardByScryfallIdAsync(string scryfallId)
        {
            var cacheKey = $"scryfall:card:id:{scryfallId}";
            if (_cache.TryGetValue(cacheKey, out ScryfallCard? cached))
                return cached;

            var card = await GetAsync<ScryfallCard>($"/cards/{Uri.EscapeDataString(scryfallId)}");

            if (card != null)
                _cache.Set(cacheKey, card, TimeSpan.FromHours(24));

            return card;
        }

        // ================================================================
        // Ricerca
        // ================================================================

        public async Task<ScryfallList<ScryfallCard>?> SearchCardsAsync(string query, int page = 1)
        {
            var encoded = Uri.EscapeDataString(query);
            return await GetAsync<ScryfallList<ScryfallCard>>($"/cards/search?q={encoded}&page={page}");
        }

        public async Task<ScryfallCatalog?> AutocompleteAsync(string query)
        {
            var cacheKey = $"scryfall:autocomplete:{query.ToLowerInvariant()}";
            if (_cache.TryGetValue(cacheKey, out ScryfallCatalog? cached))
                return cached;

            var encoded = Uri.EscapeDataString(query);
            var result = await GetAsync<ScryfallCatalog>($"/cards/autocomplete?q={encoded}");

            if (result != null)
                _cache.Set(cacheKey, result, TimeSpan.FromHours(1));

            return result;
        }

        // ================================================================
        // Collection (bulk validate, max 75 per request)
        // ================================================================

        public async Task<ScryfallCollectionResponse?> ValidateCollectionAsync(List<ScryfallIdentifier> identifiers)
        {
            if (identifiers.Count > 75)
                throw new ArgumentException("Scryfall supporta massimo 75 identificatori per richiesta");

            var request = new ScryfallCollectionRequest { Identifiers = identifiers };
            return await PostAsync<ScryfallCollectionResponse>("/cards/collection", request);
        }

        // ================================================================
        // Set
        // ================================================================

        public async Task<List<ScryfallSet>> GetAllSetsAsync()
        {
            var cacheKey = "scryfall:sets:all";
            if (_cache.TryGetValue(cacheKey, out List<ScryfallSet>? cached))
                return cached!;

            var result = await GetAsync<ScryfallList<ScryfallSet>>("/sets");
            var sets = result?.Data ?? new List<ScryfallSet>();

            _cache.Set(cacheKey, sets, TimeSpan.FromHours(24));
            return sets;
        }

        public async Task<ScryfallSet?> GetSetByCodeAsync(string code)
        {
            var cacheKey = $"scryfall:set:{code.ToLowerInvariant()}";
            if (_cache.TryGetValue(cacheKey, out ScryfallSet? cached))
                return cached;

            var set = await GetAsync<ScryfallSet>($"/sets/{Uri.EscapeDataString(code)}");

            if (set != null)
                _cache.Set(cacheKey, set, TimeSpan.FromHours(24));

            return set;
        }

        // ================================================================
        // Mapping Scryfall -> Entità locali
        // ================================================================

        public void MapScryfallToCardInfo(ScryfallCard sc, CardInfo ci)
        {
            ci.ScryfallId = sc.Id;
            ci.OracleId = sc.OracleId;
            ci.Name = sc.Name;
            ci.CardNumber = sc.CollectorNumber;
            ci.Rarity = sc.Rarity;
            ci.ManaCost = sc.ManaCost;
            ci.Cmc = sc.Cmc;
            ci.TypeLine = sc.TypeLine;
            ci.Type = sc.TypeLine?.Split('—').FirstOrDefault()?.Trim();
            ci.OracleText = sc.OracleText;
            ci.Description = sc.OracleText;
            ci.Colors = sc.Colors != null ? string.Join(",", sc.Colors) : null;
            ci.ColorIdentity = sc.ColorIdentity != null ? string.Join(",", sc.ColorIdentity) : null;
            ci.Power = sc.Power;
            ci.Toughness = sc.Toughness;
            ci.Loyalty = sc.Loyalty;
            ci.Keywords = sc.Keywords != null ? string.Join(",", sc.Keywords) : null;
            ci.Artist = sc.Artist;
            ci.ScryfallUri = sc.ScryfallUri;

            // Immagini - gestione carte a doppia faccia
            var images = sc.ImageUris ?? sc.CardFaces?.FirstOrDefault()?.ImageUris;
            if (images != null)
            {
                ci.ImageUrl = images.Normal;
                ci.ImageSmall = images.Small;
                ci.ImageNormal = images.Normal;
                ci.ImageLarge = images.Large;
                ci.ImagePng = images.Png;
                ci.ImageArtCrop = images.ArtCrop;
                ci.ImageBorderCrop = images.BorderCrop;
            }

            // Prezzi
            if (sc.Prices != null)
            {
                ci.PriceUsd = ParseDecimal(sc.Prices.Usd);
                ci.PriceUsdFoil = ParseDecimal(sc.Prices.UsdFoil);
                ci.PriceEur = ParseDecimal(sc.Prices.Eur);
                ci.PriceEurFoil = ParseDecimal(sc.Prices.EurFoil);
            }

            // Legalità (serializza come JSON)
            if (sc.Legalities != null)
                ci.Legalities = JsonSerializer.Serialize(sc.Legalities);

            ci.ScryfallUpdatedAt = DateTime.UtcNow;
        }

        public void MapScryfallToCardSet(ScryfallSet ss, CardSet cs)
        {
            cs.ScryfallId = ss.Id;
            cs.Name = ss.Name;
            cs.Code = ss.Code.ToUpperInvariant();
            cs.SetType = ss.SetType;
            cs.CardCount = ss.CardCount;
            cs.IconSvgUri = ss.IconSvgUri;
            cs.IsDigital = ss.Digital;
            cs.ScryfallUpdatedAt = DateTime.UtcNow;

            if (DateTime.TryParse(ss.ReleasedAt, out var releaseDate))
                cs.ReleaseDate = releaseDate;
        }

        // ================================================================
        // HTTP con rate limiting
        // ================================================================

        private async Task<T?> GetAsync<T>(string path) where T : class
        {
            await RateLimitAsync();

            try
            {
                var response = await _httpClient.GetAsync(path);

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return null;

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Scryfall GET {Path} -> {StatusCode}: {Error}", path, response.StatusCode, errorBody);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(json, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore chiamata Scryfall GET {Path}", path);
                return null;
            }
        }

        private async Task<T?> PostAsync<T>(string path, object body) where T : class
        {
            await RateLimitAsync();

            try
            {
                var json = JsonSerializer.Serialize(body, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(path, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Scryfall POST {Path} -> {StatusCode}: {Error}", path, response.StatusCode, errorBody);
                    return null;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(responseJson, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore chiamata Scryfall POST {Path}", path);
                return null;
            }
        }

        private static async Task RateLimitAsync()
        {
            await _rateLimiter.WaitAsync();
            try
            {
                var elapsed = (DateTime.UtcNow - _lastRequestTime).TotalMilliseconds;
                if (elapsed < MinDelayMs)
                    await Task.Delay(MinDelayMs - (int)elapsed);

                _lastRequestTime = DateTime.UtcNow;
            }
            finally
            {
                _rateLimiter.Release();
            }
        }

        private static decimal? ParseDecimal(string? value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            return decimal.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : null;
        }
    }
}
