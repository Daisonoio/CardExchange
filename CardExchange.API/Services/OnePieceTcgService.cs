using CardExchange.API.DTOs.OnePieceTcg;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Text.Json;

namespace CardExchange.API.Services
{
    public class OnePieceTcgService : IOnePieceTcgService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OnePieceTcgService> _logger;

        // ApiTCG: rispettiamo un rate limit conservativo
        private static readonly SemaphoreSlim _rateLimiter = new(1, 1);
        private static DateTime _lastRequestTime = DateTime.MinValue;
        private const int MinDelayMs = 200; // ~5 req/s max

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public OnePieceTcgService(
            HttpClient httpClient,
            IMemoryCache cache,
            ApplicationDbContext context,
            ILogger<OnePieceTcgService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _context = context;
            _logger = logger;
        }

        // ================================================================
        // Lookup
        // ================================================================

        public async Task<OnePieceCard?> GetCardByCodeAsync(string code)
        {
            var cacheKey = $"optcg:card:{code}";
            if (_cache.TryGetValue(cacheKey, out OnePieceCard? cached))
                return cached;

            await ApplyApiKeyAsync();
            await RateLimitAsync();

            try
            {
                var response = await _httpClient.GetAsync($"/api/one-piece/cards/{Uri.EscapeDataString(code)}");

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return null;

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("OnePieceTCG GET card/{Code} -> {StatusCode}: {Error}", code, response.StatusCode, errorBody);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();

                // La risposta è wrappata in { "data": {...} }
                var wrapper = JsonSerializer.Deserialize<OnePieceSingleResponse>(json, _jsonOptions);
                var card = wrapper?.Data;

                if (card != null)
                    _cache.Set(cacheKey, card, TimeSpan.FromHours(6));

                return card;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore chiamata OnePieceTCG GET card/{Code}", code);
                return null;
            }
        }

        public async Task<OnePieceResponse?> SearchCardsAsync(string query, int page = 1)
        {
            var cacheKey = $"optcg:search:{query.ToLowerInvariant()}:p{page}";
            if (_cache.TryGetValue(cacheKey, out OnePieceResponse? cached))
                return cached;

            await ApplyApiKeyAsync();
            await RateLimitAsync();

            try
            {
                var encoded = Uri.EscapeDataString(query);
                var response = await _httpClient.GetAsync($"/api/one-piece/cards?name={encoded}&page={page}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("OnePieceTCG GET cards?name={Query} -> {StatusCode}: {Error}", query, response.StatusCode, errorBody);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OnePieceResponse>(json, _jsonOptions);

                if (result != null)
                    _cache.Set(cacheKey, result, TimeSpan.FromHours(1));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore chiamata OnePieceTCG search cards: {Query}", query);
                return null;
            }
        }

        // ================================================================
        // Mapping One Piece TCG -> Entità locali
        // ================================================================

        public void MapToCardInfo(OnePieceCard src, CardInfo dest)
        {
            dest.OnePieceTcgId = src.Code;
            dest.Name = src.Name;
            dest.CardNumber = src.Code;
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

            // Immagini
            if (src.Images != null)
            {
                dest.OnePieceImageSmall = src.Images.Small;
                dest.OnePieceImageLarge = src.Images.Large;
                dest.ImageUrl = src.Images.Large ?? src.Images.Small;
            }

            dest.OnePieceTcgUpdatedAt = DateTime.UtcNow;
        }

        // ================================================================
        // HTTP helpers
        // ================================================================

        private async Task ApplyApiKeyAsync()
        {
            var cacheKey = "optcg:apikey";
            if (!_cache.TryGetValue(cacheKey, out string? apiKey))
            {
                var configKey = await _context.AppConfigKeys
                    .AsNoTracking()
                    .FirstOrDefaultAsync(k => k.ServiceName == "OnePieceTcg"
                                           && k.KeyName == "ApiKey"
                                           && k.IsActive
                                           && !k.IsDeleted);

                apiKey = configKey?.KeyValue;
                _cache.Set(cacheKey, apiKey ?? string.Empty, TimeSpan.FromMinutes(5));
            }

            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Remove("x-api-key");
                _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
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
    }

    // Wrapper per la risposta singola carta
    internal class OnePieceSingleResponse
    {
        public OnePieceCard? Data { get; set; }
    }
}
