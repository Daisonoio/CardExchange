using System.Threading.Channels;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CardExchange.API.Services.Stores
{
    /// <summary>
    /// Registra le attività dei negozi senza rallentare le richieste: coda in memoria, scrittura a blocchi
    /// in background. Visite e scansioni del QR contano una volta per persona al giorno.
    /// </summary>
    public interface IStoreActivityRecorder
    {
        /// <param name="visitorKey">Per le visite anonime (es. indirizzo IP): serve solo a non contare due volte.</param>
        void Record(int storeId, StoreActivityType type, int? userId = null, int? refId = null, string? visitorKey = null);
        ChannelReader<StoreActivity> Reader { get; }
    }

    public class StoreActivityRecorder : IStoreActivityRecorder
    {
        private static readonly HashSet<StoreActivityType> OncePerDay = new() { StoreActivityType.ProfileView, StoreActivityType.QrScan };
        private readonly Channel<StoreActivity> _channel = Channel.CreateBounded<StoreActivity>(
            new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
        private readonly IMemoryCache _seen;

        public StoreActivityRecorder(IMemoryCache cache) => _seen = cache;

        public ChannelReader<StoreActivity> Reader => _channel.Reader;

        public void Record(int storeId, StoreActivityType type, int? userId = null, int? refId = null, string? visitorKey = null)
        {
            var now = DateTime.UtcNow;
            if (OncePerDay.Contains(type))
            {
                var who = userId?.ToString() ?? visitorKey ?? "anon";
                var key = $"store-activity:{storeId}:{type}:{who}:{now:yyyyMMdd}";
                if (_seen.TryGetValue(key, out _)) return;
                _seen.Set(key, true, TimeSpan.FromHours(25));
            }
            _channel.Writer.TryWrite(new StoreActivity { StoreId = storeId, Type = type, UserId = userId, RefId = refId, OccurredAt = now });
        }
    }

    public class StoreActivityWriter : BackgroundService
    {
        /// <summary>Dopo questo periodo si tiene il conteggio ma non chi ha fatto l'azione.</summary>
        public static readonly TimeSpan UserIdRetention = TimeSpan.FromDays(395);

        private readonly IStoreActivityRecorder _recorder;
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<StoreActivityWriter> _logger;
        private DateTime _lastCleanup = DateTime.MinValue;

        public StoreActivityWriter(IStoreActivityRecorder recorder, IServiceScopeFactory scopes, ILogger<StoreActivityWriter> logger)
        {
            _recorder = recorder;
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var batch = new List<StoreActivity>();
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!await _recorder.Reader.WaitToReadAsync(stoppingToken)) return;
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    while (batch.Count < 1000 && _recorder.Reader.TryRead(out var a)) batch.Add(a);

                    using var scope = _scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    db.StoreActivities.AddRange(batch);
                    await db.SaveChangesAsync(stoppingToken);

                    if (DateTime.UtcNow - _lastCleanup > TimeSpan.FromDays(1))
                    {
                        var cutoff = DateTime.UtcNow - UserIdRetention;
                        await db.StoreActivities.Where(a => a.UserId != null && a.OccurredAt < cutoff)
                            .ExecuteUpdateAsync(s => s.SetProperty(a => a.UserId, (int?)null), stoppingToken);
                        _lastCleanup = DateTime.UtcNow;
                    }
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Salvataggio delle attività dei negozi non riuscito ({Count} perse)", batch.Count);
                }
                finally
                {
                    batch.Clear();
                }
            }
        }
    }
}
