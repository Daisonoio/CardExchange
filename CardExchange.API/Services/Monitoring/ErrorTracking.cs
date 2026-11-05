using System.Threading.Channels;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Monitoring
{
    public record ErrorReport(ErrorSource Source, string Category, string Message, string? Stack, string? Path, DateTime OccurredAt);

    /// <summary>Coda in memoria degli errori: chi segnala non aspetta mai il database.</summary>
    public interface IErrorTracker
    {
        void Report(ErrorReport report);
        ChannelReader<ErrorReport> Reader { get; }
    }

    public class ErrorTracker : IErrorTracker
    {
        // Se gli errori arrivano più velocemente di quanto si salvano, si perdono i più vecchi
        private readonly Channel<ErrorReport> _channel = Channel.CreateBounded<ErrorReport>(
            new BoundedChannelOptions(2000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

        public void Report(ErrorReport report) => _channel.Writer.TryWrite(report);
        public ChannelReader<ErrorReport> Reader => _channel.Reader;
    }

    /// <summary>
    /// Inoltra al monitoraggio tutti i messaggi di livello Error o superiore, anche quelli di errori
    /// gestiti dai controller (che non arrivano al gestore globale delle eccezioni).
    /// </summary>
    public sealed class ErrorTrackingLoggerProvider : ILoggerProvider
    {
        private readonly IErrorTracker _tracker;
        public ErrorTrackingLoggerProvider(IErrorTracker tracker) => _tracker = tracker;
        public ILogger CreateLogger(string categoryName) => new TrackingLogger(categoryName, _tracker);
        public void Dispose() { }

        private sealed class TrackingLogger : ILogger
        {
            private readonly string _category;
            private readonly IErrorTracker _tracker;
            // Il monitoraggio non segnala se stesso; gli errori del database sono già registrati dal codice che lo usa
            private readonly bool _ignored;

            public TrackingLogger(string category, IErrorTracker tracker)
            {
                _category = category;
                _tracker = tracker;
                _ignored = category.StartsWith("CardExchange.API.Services.Monitoring") || category.StartsWith("Microsoft.EntityFrameworkCore");
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => !_ignored && logLevel >= LogLevel.Error;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;
                var message = exception != null ? $"{exception.GetType().Name}: {exception.Message}" : formatter(state, exception);
                var stack = exception != null ? $"{formatter(state, exception)}\n{exception}" : null;
                _tracker.Report(new ErrorReport(ErrorSource.Server, _category, message, stack, null, DateTime.UtcNow));
            }
        }
    }

    /// <summary>Salva gli errori raggruppati per impronta e avvisa gli amministratori dei nuovi.</summary>
    public class ErrorTrackingHostedService : BackgroundService
    {
        private readonly IErrorTracker _tracker;
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<ErrorTrackingHostedService> _logger;

        public ErrorTrackingHostedService(IErrorTracker tracker, IServiceScopeFactory scopes, ILogger<ErrorTrackingHostedService> logger)
        {
            _tracker = tracker;
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var batch = new List<ErrorReport>();
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!await _tracker.Reader.WaitToReadAsync(stoppingToken)) return;
                    // Piccola attesa per raccogliere gli errori arrivati insieme
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    while (batch.Count < 500 && _tracker.Reader.TryRead(out var r)) batch.Add(r);
                    await SaveAsync(batch, stoppingToken);
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    // Livello Warning: un errore qui non deve rientrare nel monitoraggio stesso
                    _logger.LogWarning(ex, "Salvataggio degli errori non riuscito");
                }
                finally
                {
                    batch.Clear();
                }
            }
        }

        public async Task SaveAsync(IReadOnlyList<ErrorReport> reports, CancellationToken ct)
        {
            if (reports.Count == 0) return;
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var alerts = scope.ServiceProvider.GetRequiredService<MonitoringAlerts>();
            await SaveAsync(db, alerts, reports, ct);
        }

        public static async Task<int> SaveAsync(ApplicationDbContext db, MonitoringAlerts alerts, IReadOnlyList<ErrorReport> reports, CancellationToken ct)
        {
            var groups = reports
                .Select(r => (r, fp: ErrorFingerprint.Compute(r.Source.ToString(), r.Category, r.Message, r.Stack)))
                .GroupBy(x => x.fp)
                .ToList();
            var fps = groups.Select(g => g.Key).ToList();
            var existing = await db.ErrorGroups.Where(e => fps.Contains(e.Fingerprint)).ToDictionaryAsync(e => e.Fingerprint, ct);
            var toAlert = new List<ErrorGroup>();

            foreach (var g in groups)
            {
                var first = g.First().r;
                var last = g.Max(x => x.r.OccurredAt);
                if (existing.TryGetValue(g.Key, out var group))
                {
                    group.Count += g.Count();
                    group.LastSeenAt = last;
                    if (group.IsResolved)
                    {
                        // Un errore dato per risolto si è ripresentato
                        group.IsResolved = false;
                        toAlert.Add(group);
                    }
                }
                else
                {
                    group = new ErrorGroup
                    {
                        Fingerprint = g.Key,
                        Source = first.Source,
                        Category = Cut(first.Category, 200),
                        Message = Cut(first.Message, 500),
                        Detail = first.Stack == null ? null : Cut(first.Stack, 4000),
                        Path = first.Path == null ? null : Cut(first.Path, 300),
                        Count = g.Count(),
                        FirstSeenAt = g.Min(x => x.r.OccurredAt),
                        LastSeenAt = last
                    };
                    db.ErrorGroups.Add(group);
                    toAlert.Add(group);
                }
            }
            await db.SaveChangesAsync(ct);

            foreach (var group in toAlert)
            {
                if (await alerts.NewErrorAsync(group, ct)) group.NotifiedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
            return toAlert.Count;
        }

        private static string Cut(string s, int max) => s.Length <= max ? s : s[..max];
    }
}
