using CardExchange.API.Services.Catalog;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Monitoring
{
    /// <summary>
    /// Controlla i cataloghi esterni con una ricerca di una carta che esiste di sicuro.
    /// Dopo 2 controlli falliti di fila avvisa gli amministratori; quando torna a funzionare lo dice.
    /// </summary>
    public class CatalogHealthChecker
    {
        public const int FailuresBeforeAlert = 2;

        /// <summary>Carta nota per ogni catalogo: se non si trova, il catalogo ha un problema.</summary>
        public static readonly IReadOnlyDictionary<string, string> Probes = new Dictionary<string, string>
        {
            ["magic"] = "Lightning Bolt",
            ["pokemon"] = "Pikachu",
            ["yugioh"] = "Dark Magician",
            ["onepiece"] = "Luffy",
            ["lorcana"] = "Elsa",
        };

        private readonly ApplicationDbContext _db;
        private readonly ICatalogRegistry _registry;
        private readonly MonitoringAlerts _alerts;
        private readonly ILogger<CatalogHealthChecker> _logger;
        private readonly IScryfallService _scryfall;

        public CatalogHealthChecker(ApplicationDbContext db, ICatalogRegistry registry, MonitoringAlerts alerts, ILogger<CatalogHealthChecker> logger, IScryfallService scryfall)
        {
            _scryfall = scryfall;
            _db = db;
            _registry = registry;
            _alerts = alerts;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            // Magic passa da Scryfall, fuori dal registro dei cataloghi
            string? magicError = null;
            try
            {
                var found = await _scryfall.SearchCardsAsync(Probes["magic"]);
                if (found?.Data == null || found.Data.Count == 0) magicError = "nessun risultato per una carta nota";
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                magicError = $"{ex.GetType().Name}: {ex.Message}";
            }
            await RecordAsync("magic", magicError, DateTime.UtcNow, ct);

            foreach (var provider in _registry.All)
            {
                var probe = Probes.TryGetValue(provider.Key, out var q) ? q : null;
                if (probe == null) continue;

                string? error = null;
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeout.CancelAfter(TimeSpan.FromSeconds(30));
                    var found = await provider.SearchAsync(probe, 1, timeout.Token);
                    if (found.Count == 0) error = "nessun risultato per una carta nota";
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    error = $"{ex.GetType().Name}: {ex.Message}";
                }

                await RecordAsync(provider.Key, error, DateTime.UtcNow, ct);
            }
        }

        public async Task RecordAsync(string key, string? error, DateTime now, CancellationToken ct)
        {
            var status = await _db.CatalogStatuses.FirstOrDefaultAsync(c => c.Key == key, ct);
            if (status == null)
            {
                status = new Core.Entities.CatalogStatus { Key = key };
                _db.CatalogStatuses.Add(status);
            }
            status.LastCheckAt = now;
            if (error == null)
            {
                var wasDown = status.OutageAlerted;
                status.LastOkAt = now;
                status.ConsecutiveFailures = 0;
                status.OutageAlerted = false;
                if (wasDown) await _alerts.CatalogBackAsync(key, ct);
            }
            else
            {
                status.LastFailAt = now;
                status.ConsecutiveFailures++;
                status.LastError = error.Length > 500 ? error[..500] : error;
                _logger.LogWarning("Catalogo {Key} non raggiungibile ({Failures} di fila): {Error}", key, status.ConsecutiveFailures, error);
                if (status.ConsecutiveFailures >= FailuresBeforeAlert && !status.OutageAlerted)
                {
                    await _alerts.CatalogDownAsync(key, error, ct);
                    status.OutageAlerted = true;
                }
            }
            await _db.SaveChangesAsync(ct);
        }
    }

    public class CatalogHealthHostedService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<CatalogHealthHostedService> _logger;
        private readonly IConfiguration _config;

        public CatalogHealthHostedService(IServiceScopeFactory scopes, ILogger<CatalogHealthHostedService> logger, IConfiguration config)
        {
            _scopes = scopes;
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.GetValue("Monitoring:CatalogChecks", true)) return;
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch (OperationCanceledException) { return; }
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<CatalogHealthChecker>().RunAsync(stoppingToken);
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Controllo dei cataloghi non riuscito");
                }
                try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { return; }
            }
        }
    }
}
