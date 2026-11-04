using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CardExchange.API.Services.Catalog
{
    /// <summary>
    /// Disney Lorcana tramite Lorcast (api.lorcast.com, gratuita, senza chiave, max ~10 richieste al secondo).
    /// Lorcast ha solo prezzi in dollari: sono convertiti in euro con il cambio BCE del giorno e segnati come convertiti.
    /// </summary>
    public class LorcanaProvider : GenericCatalogProvider
    {
        public const string BaseUrl = "https://api.lorcast.com/v0/";
        private static readonly SemaphoreSlim Throttle = new(1, 1);
        private static DateTime _lastCall = DateTime.MinValue;

        private readonly HttpClient _http;
        private readonly IExchangeRates _fx;
        private readonly ILogger<LorcanaProvider> _logger;

        public LorcanaProvider(HttpClient http, IExchangeRates fx, CatalogStore store, ILogger<LorcanaProvider> logger) : base(store)
        {
            _http = http;
            _fx = fx;
            _logger = logger;
            _http.BaseAddress ??= new Uri(BaseUrl);
        }

        public override string Key => "lorcana";
        public override string GameName => "Disney Lorcana";
        public override string Publisher => "Ravensburger";
        public override string Description => "Il gioco di carte collezionabili con i personaggi Disney";
        public override string ImportHint => "numero del set e numero della carta (es. set 1, carta 207), oppure il nome completo";

        public override async Task<IReadOnlyList<CatalogCard>> SearchAsync(string query, int limit, CancellationToken ct = default)
        {
            var page = await GetJsonAsync<LorcastSearch>($"cards/search?q={Uri.EscapeDataString(query)}", ct);
            if (page?.Results == null) return Array.Empty<CatalogCard>();
            var rate = await _fx.UsdToEurAsync(ct);
            return page.Results.Take(limit).Select(c => Map(c, rate)).ToList();
        }

        public override async Task<CatalogCard?> GetAsync(string externalId, CancellationToken ct = default)
        {
            var card = await GetJsonAsync<LorcastCard>($"cards/{Uri.EscapeDataString(externalId)}", ct);
            return card == null ? null : Map(card, await _fx.UsdToEurAsync(ct));
        }

        public override async Task<CatalogCard?> ResolveAsync(CatalogLookup lookup, CancellationToken ct = default)
        {
            var rate = await _fx.UsdToEurAsync(ct);
            if (!string.IsNullOrWhiteSpace(lookup.SetCode) && !string.IsNullOrWhiteSpace(lookup.Number))
            {
                var number = lookup.Number.Split('/')[0].Trim().TrimStart('0');
                var card = await GetJsonAsync<LorcastCard>($"cards/{Uri.EscapeDataString(lookup.SetCode.Trim())}/{Uri.EscapeDataString(number)}", ct);
                if (card != null) return Map(card, rate);
            }
            if (string.IsNullOrWhiteSpace(lookup.Name)) return null;

            // "Elsa - Snow Queen": nome e versione
            var parts = lookup.Name.Split(" - ", 2, StringSplitOptions.TrimEntries);
            var query = $"name:\"{parts[0]}\"" + (parts.Length > 1 ? $" version:\"{parts[1]}\"" : "");
            if (!string.IsNullOrWhiteSpace(lookup.SetCode)) query += $" set:{lookup.SetCode.Trim()}";
            var page = await GetJsonAsync<LorcastSearch>($"cards/search?q={Uri.EscapeDataString(query)}", ct);
            var match = page?.Results?
                .Where(c => string.Equals(c.Name, parts[0], StringComparison.OrdinalIgnoreCase)
                            && (parts.Length == 1 || string.Equals(c.Version, parts[1], StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(c => c.ReleasedAt)
                .FirstOrDefault();
            return match == null ? null : Map(match, rate);
        }

        public static CatalogCard Map(LorcastCard c, decimal usdToEur)
        {
            decimal? Eur(decimal? usd) => usd.HasValue ? Math.Round(usd.Value * usdToEur, 2) : null;
            var fullName = string.IsNullOrWhiteSpace(c.Version) ? c.Name : $"{c.Name} - {c.Version}";
            return new CatalogCard
            {
                ExternalId = c.Id,
                Name = fullName,
                Subtitle = string.Join(" · ", new[] { c.Ink, c.Type != null ? string.Join(", ", c.Type) : null, c.Cost.HasValue ? $"Costo {c.Cost}" : null }
                    .Where(x => !string.IsNullOrWhiteSpace(x))),
                SetCode = c.Set?.Code,
                SetName = c.Set?.Name,
                Number = c.CollectorNumber,
                Rarity = c.Rarity?.Replace('_', ' '),
                Type = c.Type != null ? string.Join(", ", c.Type) : null,
                Text = c.Text,
                ImageSmall = c.ImageUris?.Digital?.Small,
                ImageLarge = c.ImageUris?.Digital?.Normal ?? c.ImageUris?.Digital?.Large,
                PriceUsd = c.Prices?.Usd,
                PriceUsdFoil = c.Prices?.UsdFoil,
                PriceEur = Eur(c.Prices?.Usd),
                PriceEurFoil = Eur(c.Prices?.UsdFoil),
                PriceConverted = c.Prices?.Usd != null || c.Prices?.UsdFoil != null,
                Data = new Dictionary<string, object?>
                {
                    ["ink"] = c.Ink,
                    ["cost"] = c.Cost,
                    ["inkwell"] = c.Inkwell,
                    ["strength"] = c.Strength,
                    ["willpower"] = c.Willpower,
                    ["lore"] = c.Lore,
                    ["classifications"] = c.Classifications,
                    ["priceSource"] = "Lorcast (TCGplayer, USD convertiti in EUR)"
                }
            };
        }

        private async Task<T?> GetJsonAsync<T>(string path, CancellationToken ct) where T : class
        {
            await Throttle.WaitAsync(ct);
            try
            {
                var wait = TimeSpan.FromMilliseconds(120) - (DateTime.UtcNow - _lastCall);
                if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
                _lastCall = DateTime.UtcNow;
                using var response = await _http.GetAsync(path, ct);
                if (response.StatusCode == HttpStatusCode.NotFound) return null;
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Lorcast {Path} -> {Status}", path, (int)response.StatusCode);
                    return null;
                }
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: ct);
            }
            finally
            {
                Throttle.Release();
            }
        }

        // ---- DTO Lorcast ----
        public class LorcastSearch
        {
            [JsonPropertyName("results")] public List<LorcastCard>? Results { get; set; }
        }

        public class LorcastCard
        {
            [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
            [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
            [JsonPropertyName("version")] public string? Version { get; set; }
            [JsonPropertyName("released_at")] public string? ReleasedAt { get; set; }
            [JsonPropertyName("image_uris")] public LorcastImages? ImageUris { get; set; }
            [JsonPropertyName("cost")][JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] public int? Cost { get; set; }
            [JsonPropertyName("inkwell")] public bool? Inkwell { get; set; }
            [JsonPropertyName("ink")] public string? Ink { get; set; }
            [JsonPropertyName("type")] public List<string>? Type { get; set; }
            [JsonPropertyName("classifications")] public List<string>? Classifications { get; set; }
            [JsonPropertyName("text")] public string? Text { get; set; }
            [JsonPropertyName("strength")] public int? Strength { get; set; }
            [JsonPropertyName("willpower")] public int? Willpower { get; set; }
            [JsonPropertyName("lore")] public int? Lore { get; set; }
            [JsonPropertyName("rarity")] public string? Rarity { get; set; }
            [JsonPropertyName("collector_number")] public string? CollectorNumber { get; set; }
            [JsonPropertyName("set")] public LorcastSet? Set { get; set; }
            [JsonPropertyName("prices")] public LorcastPrices? Prices { get; set; }
        }

        public class LorcastImages
        {
            [JsonPropertyName("digital")] public LorcastImageSizes? Digital { get; set; }
        }

        public class LorcastImageSizes
        {
            [JsonPropertyName("small")] public string? Small { get; set; }
            [JsonPropertyName("normal")] public string? Normal { get; set; }
            [JsonPropertyName("large")] public string? Large { get; set; }
        }

        public class LorcastSet
        {
            [JsonPropertyName("code")] public string? Code { get; set; }
            [JsonPropertyName("name")] public string? Name { get; set; }
        }

        /// <summary>Lorcast restituisce i prezzi a volte come numero, a volte come stringa ("952.84").</summary>
        public class LorcastPrices
        {
            [JsonPropertyName("usd")]
            [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
            public decimal? Usd { get; set; }

            [JsonPropertyName("usd_foil")]
            [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
            public decimal? UsdFoil { get; set; }
        }
    }
}
