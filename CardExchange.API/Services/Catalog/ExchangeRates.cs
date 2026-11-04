using System.Globalization;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;

namespace CardExchange.API.Services.Catalog
{
    public interface IExchangeRates
    {
        /// <summary>Quanti euro vale un dollaro (tasso di riferimento BCE del giorno).</summary>
        Task<decimal> UsdToEurAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Tasso di cambio di riferimento della Banca Centrale Europea, aggiornato una volta al giorno lavorativo.
    /// Se la BCE non risponde si usa Catalog:UsdToEurFallback (impostabile da configurazione).
    /// </summary>
    public class EcbExchangeRates : IExchangeRates
    {
        public const string EcbDailyUrl = "https://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml";
        private const string CacheKey = "fx:usd-eur";

        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EcbExchangeRates> _logger;

        public EcbExchangeRates(HttpClient http, IMemoryCache cache, IConfiguration configuration, ILogger<EcbExchangeRates> logger)
        {
            _http = http;
            _cache = cache;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<decimal> UsdToEurAsync(CancellationToken ct = default)
        {
            if (_cache.TryGetValue(CacheKey, out decimal cached)) return cached;
            decimal rate;
            try
            {
                var xml = await _http.GetStringAsync(EcbDailyUrl, ct);
                rate = ParseUsdToEur(xml);
                _cache.Set(CacheKey, rate, TimeSpan.FromHours(12));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException or System.Xml.XmlException)
            {
                rate = _configuration.GetValue("Catalog:UsdToEurFallback", 0.9m);
                _logger.LogWarning(ex, "Cambio BCE non disponibile, uso il valore di riserva {Rate}", rate);
                _cache.Set(CacheKey, rate, TimeSpan.FromHours(1));
            }
            return rate;
        }

        /// <summary>La BCE pubblica quanti dollari vale un euro: il cambio inverso è 1 / tasso.</summary>
        public static decimal ParseUsdToEur(string xml)
        {
            var doc = XDocument.Parse(xml);
            var usd = doc.Descendants().FirstOrDefault(e => (string?)e.Attribute("currency") == "USD")
                      ?? throw new FormatException("USD non presente nel file BCE");
            var eurToUsd = decimal.Parse((string)usd.Attribute("rate")!, CultureInfo.InvariantCulture);
            if (eurToUsd <= 0) throw new FormatException("Tasso non valido");
            return Math.Round(1m / eurToUsd, 6);
        }
    }
}
