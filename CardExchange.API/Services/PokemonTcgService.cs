using CardExchange.API.DTOs.PokemonTcg;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Text.Json;

namespace CardExchange.API.Services
{
    public class PokemonTcgService : IPokemonTcgService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PokemonTcgService> _logger;

        // Pokémon TCG API: 30 req/min senza API key, molto di più con key
        // Per sicurezza manteniamo un rate limiter conservativo
        private static readonly SemaphoreSlim _rateLimiter = new(1, 1);
        private static DateTime _lastRequestTime = DateTime.MinValue;
        private const int MinDelayMs = 200; // ~5 req/s max

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // Opzioni JSON compatte per il salvataggio in DB (minimizza spazio)
        private static readonly JsonSerializerOptions _compactJsonOptions = new()
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public PokemonTcgService(
            HttpClient httpClient,
            IMemoryCache cache,
            ApplicationDbContext context,
            ILogger<PokemonTcgService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _context = context;
            _logger = logger;
        }

        // ================================================================
        // Lookup — DB-first, API come fallback
        // ================================================================

        public async Task<PokemonTcgCard?> GetCardByIdAsync(string pokemonTcgId)
        {
            var cacheKey = $"ptcg:card:{pokemonTcgId}";
            if (_cache.TryGetValue(cacheKey, out PokemonTcgCard? cached))
                return cached;

            var card = await GetAsync<PokemonTcgCard>($"/v2/cards/{Uri.EscapeDataString(pokemonTcgId)}");

            if (card != null)
                _cache.Set(cacheKey, card, TimeSpan.FromHours(6));

            return card;
        }

        public async Task<PokemonTcgResponse<List<PokemonTcgCard>>?> SearchCardsAsync(string query, int page = 1, int pageSize = 20)
        {
            var encoded = Uri.EscapeDataString(query);
            return await GetListAsync<PokemonTcgCard>($"/v2/cards?q={encoded}&page={page}&pageSize={pageSize}");
        }

        // ================================================================
        // Set
        // ================================================================

        public async Task<List<PokemonTcgSet>> GetAllSetsAsync()
        {
            var cacheKey = "ptcg:sets:all";
            if (_cache.TryGetValue(cacheKey, out List<PokemonTcgSet>? cached))
                return cached!;

            var result = await GetListAsync<PokemonTcgSet>("/v2/sets?orderBy=releaseDate");
            var sets = result?.Data ?? new List<PokemonTcgSet>();

            _cache.Set(cacheKey, sets, TimeSpan.FromHours(24));
            return sets;
        }

        public async Task<PokemonTcgSet?> GetSetByIdAsync(string setId)
        {
            var cacheKey = $"ptcg:set:{setId}";
            if (_cache.TryGetValue(cacheKey, out PokemonTcgSet? cached))
                return cached;

            var set = await GetAsync<PokemonTcgSet>($"/v2/sets/{Uri.EscapeDataString(setId)}");

            if (set != null)
                _cache.Set(cacheKey, set, TimeSpan.FromHours(24));

            return set;
        }

        public async Task<PokemonTcgResponse<List<PokemonTcgCard>>?> GetCardsBySetAsync(string setId, int page = 1, int pageSize = 250)
        {
            var encoded = Uri.EscapeDataString($"set.id:{setId}");
            return await GetListAsync<PokemonTcgCard>($"/v2/cards?q={encoded}&page={page}&pageSize={pageSize}");
        }

        // ================================================================
        // Mapping Pokémon TCG -> Entità locali
        // ================================================================

        public void MapToCardInfo(PokemonTcgCard src, CardInfo dest)
        {
            dest.PokemonTcgId = src.Id;
            dest.Name = src.Name;
            dest.CardNumber = src.Number;
            dest.Rarity = src.Rarity;
            dest.Artist = src.Artist;
            dest.Supertype = src.Supertype;
            dest.Subtypes = src.Subtypes != null ? string.Join(",", src.Subtypes) : null;
            dest.Hp = src.Hp;
            dest.PokemonTypes = src.Types != null ? string.Join(",", src.Types) : null;
            dest.EvolvesFrom = src.EvolvesFrom;
            dest.Type = src.Supertype; // Pokémon, Trainer, Energy
            dest.Description = src.FlavorText;

            // Immagini CDN (non consumano API calls)
            if (src.Images != null)
            {
                dest.PokemonImageSmall = src.Images.Small;
                dest.PokemonImageLarge = src.Images.Large;
                dest.ImageUrl = src.Images.Large ?? src.Images.Small;
            }

            // Prezzi TCGPlayer (mercato USD)
            if (src.TcgPlayer?.Prices != null)
            {
                dest.PriceTcgNormal = src.TcgPlayer.Prices.Normal?.Market;
                dest.PriceTcgHolofoil = src.TcgPlayer.Prices.Holofoil?.Market;
                dest.PriceTcgReverseHolofoil = src.TcgPlayer.Prices.ReverseHolofoil?.Market;

                // Mappa il prezzo principale in PriceUsd (il primo disponibile)
                dest.PriceUsd = src.TcgPlayer.Prices.Normal?.Market
                    ?? src.TcgPlayer.Prices.Holofoil?.Market
                    ?? src.TcgPlayer.Prices.ReverseHolofoil?.Market;
            }

            // Prezzi Cardmarket (mercato EUR)
            if (src.Cardmarket?.Prices != null)
            {
                dest.PriceCardmarketAvg = src.Cardmarket.Prices.AverageSellPrice;
                dest.PriceCardmarketTrend = src.Cardmarket.Prices.TrendPrice;

                // Mappa in PriceEur
                dest.PriceEur = src.Cardmarket.Prices.AverageSellPrice
                    ?? src.Cardmarket.Prices.TrendPrice;
            }

            // Legalità
            if (src.Legalities != null)
                dest.Legalities = JsonSerializer.Serialize(src.Legalities, _compactJsonOptions);

            // Dati estesi compressi (attacchi, debolezze, resistenze, abilità, regole)
            var extendedData = new
            {
                attacks = src.Attacks?.Select(a => new { a.Name, a.Cost, a.Damage, a.Text }),
                abilities = src.Abilities?.Select(a => new { a.Name, a.Text, a.Type }),
                weaknesses = src.Weaknesses,
                resistances = src.Resistances,
                retreatCost = src.ConvertedRetreatCost,
                rules = src.Rules,
                evolvesTo = src.EvolvesTo,
                pokedexNumbers = src.NationalPokedexNumbers,
                regulationMark = src.RegulationMark
            };
            dest.PokemonTcgData = JsonSerializer.Serialize(extendedData, _compactJsonOptions);

            dest.PokemonTcgUpdatedAt = DateTime.UtcNow;
        }

        public void MapToCardSet(PokemonTcgSet src, CardSet dest)
        {
            dest.PokemonTcgId = src.Id;
            dest.Name = src.Name;
            dest.Code = src.Id.ToUpperInvariant();
            dest.Series = src.Series;
            dest.CardCount = src.Total;
            dest.PrintedTotal = src.PrintedTotal;

            if (src.Images != null)
            {
                dest.PokemonLogoUrl = src.Images.Logo;
                dest.PokemonSymbolUrl = src.Images.Symbol;
                dest.IconSvgUri = src.Images.Symbol;
            }

            if (DateTime.TryParse(src.ReleaseDate, out var releaseDate))
                dest.ReleaseDate = releaseDate;

            dest.PokemonTcgUpdatedAt = DateTime.UtcNow;
        }

        // ================================================================
        // HTTP con rate limiting e gestione API key da DB
        // ================================================================

        private async Task<T?> GetAsync<T>(string path) where T : class
        {
            await ApplyApiKeyAsync();
            await RateLimitAsync();

            try
            {
                var response = await _httpClient.GetAsync(path);

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return null;

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("Pokémon TCG API rate limit raggiunto per {Path}", path);
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("PokemonTCG GET {Path} -> {StatusCode}: {Error}", path, response.StatusCode, errorBody);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();

                // L'API wrappa i singoli oggetti in { "data": {...} }
                var wrapper = JsonSerializer.Deserialize<PokemonTcgResponse<T>>(json, _jsonOptions);
                return wrapper?.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore chiamata PokemonTCG GET {Path}", path);
                return null;
            }
        }

        private async Task<PokemonTcgResponse<List<T>>?> GetListAsync<T>(string path)
        {
            await ApplyApiKeyAsync();
            await RateLimitAsync();

            try
            {
                var response = await _httpClient.GetAsync(path);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("PokemonTCG GET {Path} -> {StatusCode}: {Error}", path, response.StatusCode, errorBody);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<PokemonTcgResponse<List<T>>>(json, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore chiamata PokemonTCG GET {Path}", path);
                return null;
            }
        }

        private async Task ApplyApiKeyAsync()
        {
            // Leggi API key dalla tabella AppConfigKey (con cache in-memory)
            var cacheKey = "ptcg:apikey";
            if (!_cache.TryGetValue(cacheKey, out string? apiKey))
            {
                var configKey = await _context.AppConfigKeys
                    .AsNoTracking()
                    .FirstOrDefaultAsync(k => k.ServiceName == "PokemonTcg"
                                           && k.KeyName == "ApiKey"
                                           && k.IsActive
                                           && !k.IsDeleted);

                apiKey = configKey?.KeyValue;

                // Cache per 5 minuti per non bombardare il DB
                _cache.Set(cacheKey, apiKey ?? string.Empty, TimeSpan.FromMinutes(5));
            }

            if (!string.IsNullOrEmpty(apiKey))
            {
                // Rimuovi e aggiungi per evitare duplicati
                _httpClient.DefaultRequestHeaders.Remove("X-Api-Key");
                _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
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
}
