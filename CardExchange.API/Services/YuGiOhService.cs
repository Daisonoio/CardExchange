using CardExchange.API.DTOs.YuGiOh;
using CardExchange.Core.Entities;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Text.Json;

namespace CardExchange.API.Services
{
    public class YuGiOhService : IYuGiOhService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<YuGiOhService> _logger;

        // YGOProDeck: 20 req/s max, IP bloccato per 1h se superato
        private static readonly SemaphoreSlim _rateLimiter = new(1, 1);
        private static DateTime _lastRequestTime = DateTime.MinValue;
        private const int MinDelayMs = 100; // ~10 req/s per sicurezza

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions _compactJsonOptions = new()
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public YuGiOhService(HttpClient httpClient, IMemoryCache cache, ILogger<YuGiOhService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
        }

        // ================================================================
        // Lookup
        // ================================================================

        public async Task<YuGiOhCard?> GetCardByIdAsync(int yugiohId)
        {
            var cacheKey = $"yugioh:card:{yugiohId}";
            if (_cache.TryGetValue(cacheKey, out YuGiOhCard? cached))
                return cached;

            var cards = await GetCardsAsync($"/api/v7/cardinfo.php?id={yugiohId}");
            var card = cards?.FirstOrDefault();

            if (card != null)
                _cache.Set(cacheKey, card, TimeSpan.FromHours(24));

            return card;
        }

        public async Task<List<YuGiOhCard>> SearchCardsAsync(string query, int offset = 0, int num = 20)
        {
            var encoded = Uri.EscapeDataString(query);
            var cards = await GetCardsAsync($"/api/v7/cardinfo.php?fname={encoded}&num={num}&offset={offset}");
            return cards ?? new List<YuGiOhCard>();
        }

        // ================================================================
        // Set
        // ================================================================

        public async Task<List<YuGiOhCardSet>> GetAllSetsAsync()
        {
            var cacheKey = "yugioh:sets:all";
            if (_cache.TryGetValue(cacheKey, out List<YuGiOhCardSet>? cached))
                return cached!;

            await RateLimitAsync();

            try
            {
                var response = await _httpClient.GetAsync("/api/v7/cardsets.php");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("YuGiOh GET cardsets.php -> {StatusCode}", response.StatusCode);
                    return new List<YuGiOhCardSet>();
                }

                var json = await response.Content.ReadAsStringAsync();
                var sets = JsonSerializer.Deserialize<List<YuGiOhCardSet>>(json, _jsonOptions)
                           ?? new List<YuGiOhCardSet>();

                _cache.Set(cacheKey, sets, TimeSpan.FromHours(24));
                return sets;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore chiamata YuGiOh GET cardsets.php");
                return new List<YuGiOhCardSet>();
            }
        }

        // ================================================================
        // Mapping Yu-Gi-Oh! -> Entità locali
        // ================================================================

        public void MapToCardInfo(YuGiOhCard src, CardInfo dest)
        {
            dest.YuGiOhId = src.Id;
            dest.Name = src.Name;
            dest.Description = src.Desc;
            dest.YuGiOhType = src.Type;
            dest.YuGiOhFrameType = src.FrameType;
            dest.YuGiOhAttribute = src.Attribute;
            dest.YuGiOhRace = src.Race;
            dest.YuGiOhLevel = src.Level ?? src.LinkVal;
            dest.YuGiOhAtk = src.Atk;
            dest.YuGiOhDef = src.Def;
            dest.YuGiOhArchetype = src.Archetype;
            dest.Rarity = src.CardSets?.FirstOrDefault()?.SetRarity;
            dest.Type = src.Type;

            // Immagini (prima immagine disponibile)
            var image = src.CardImages?.FirstOrDefault();
            if (image != null)
            {
                dest.YuGiOhImageUrl = image.ImageUrl;
                dest.YuGiOhImageSmall = image.ImageUrlSmall;
                dest.ImageUrl = image.ImageUrl;
            }

            // Prezzi (primo set di prezzi disponibile)
            var prices = src.CardPrices?.FirstOrDefault();
            if (prices != null)
            {
                dest.PriceYuGiOhTcgPlayer = ParseDecimal(prices.TcgPlayerPrice);
                dest.PriceYuGiOhCardmarket = ParseDecimal(prices.CardmarketPrice);
                dest.PriceYuGiOhEbay = ParseDecimal(prices.EbayPrice);
                dest.PriceYuGiOhAmazon = ParseDecimal(prices.AmazonPrice);
                dest.PriceYuGiOhCoolstuffinc = ParseDecimal(prices.CoolStuffIncPrice);

                // Mappa il prezzo principale
                dest.PriceUsd = ParseDecimal(prices.TcgPlayerPrice);
                dest.PriceEur = ParseDecimal(prices.CardmarketPrice);
            }

            // Numero carta dal primo set
            dest.CardNumber = src.CardSets?.FirstOrDefault()?.SetCode;

            // Dati estesi compressi (card_sets, banlist_info, link markers, pendulum)
            var extendedData = new
            {
                cardSets = src.CardSets?.Select(cs => new { cs.SetName, cs.SetCode, cs.SetRarity, cs.SetPrice }),
                banlistInfo = src.BanlistInfo,
                linkMarkers = src.LinkMarkers,
                scale = src.Scale,
                pendulumDesc = src.PendDesc,
                monsterDesc = src.MonsterDesc
            };
            dest.YuGiOhData = JsonSerializer.Serialize(extendedData, _compactJsonOptions);

            dest.YuGiOhUpdatedAt = DateTime.UtcNow;
        }

        public void MapToCardSet(YuGiOhCardSet src, CardSet dest)
        {
            dest.Name = src.SetName;
            dest.Code = src.SetCode.ToUpperInvariant();
            dest.YuGiOhSetCode = src.SetCode;
            dest.YuGiOhNumCards = src.NumOfCards;
            dest.CardCount = src.NumOfCards;
            dest.YuGiOhTcgDate = src.TcgDate;

            if (DateTime.TryParse(src.TcgDate, out var releaseDate))
                dest.ReleaseDate = releaseDate;

            dest.YuGiOhUpdatedAt = DateTime.UtcNow;
        }

        // ================================================================
        // HTTP con rate limiting
        // ================================================================

        private async Task<List<YuGiOhCard>?> GetCardsAsync(string path)
        {
            await RateLimitAsync();

            try
            {
                var response = await _httpClient.GetAsync(path);

                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    // YGOProDeck restituisce 400 quando non trova risultati
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("YuGiOh API rate limit raggiunto per {Path}", path);
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("YuGiOh GET {Path} -> {StatusCode}: {Error}", path, response.StatusCode, errorBody);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<YuGiOhResponse>(json, _jsonOptions);
                return result?.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore chiamata YuGiOh GET {Path}", path);
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
            return decimal.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var result) && result > 0 ? result : null;
        }
    }
}
