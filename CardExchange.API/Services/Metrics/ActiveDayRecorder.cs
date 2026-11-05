using System.Threading.Channels;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CardExchange.API.Services.Metrics
{
    /// <summary>Segna una volta al giorno che un utente ha usato l'app, senza rallentare le richieste.</summary>
    public class ActiveDayRecorder
    {
        private readonly Channel<UserActiveDay> _channel = Channel.CreateBounded<UserActiveDay>(
            new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
        private readonly IMemoryCache _seen;

        public ActiveDayRecorder(IMemoryCache cache) => _seen = cache;

        public ChannelReader<UserActiveDay> Reader => _channel.Reader;

        public void Touch(int userId, DateTime nowUtc)
        {
            var day = PlanMetrics.ItalianDay(nowUtc);
            var key = $"active-day:{userId}:{day:yyyyMMdd}";
            if (_seen.TryGetValue(key, out _)) return;
            _seen.Set(key, true, TimeSpan.FromHours(26));
            _channel.Writer.TryWrite(new UserActiveDay { UserId = userId, Day = day });
        }
    }

    public class ActiveDayWriter : BackgroundService
    {
        public static readonly TimeSpan Retention = TimeSpan.FromDays(400);
        private readonly ActiveDayRecorder _recorder;
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<ActiveDayWriter> _logger;
        private DateTime _lastCleanup = DateTime.MinValue;

        public ActiveDayWriter(ActiveDayRecorder recorder, IServiceScopeFactory scopes, ILogger<ActiveDayWriter> logger)
        {
            _recorder = recorder;
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var batch = new List<UserActiveDay>();
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!await _recorder.Reader.WaitToReadAsync(stoppingToken)) return;
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    while (batch.Count < 1000 && _recorder.Reader.TryRead(out var a)) batch.Add(a);

                    using var scope = _scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    await SaveAsync(db, batch, stoppingToken);

                    if (DateTime.UtcNow - _lastCleanup > TimeSpan.FromDays(1))
                    {
                        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow - Retention);
                        await db.UserActiveDays.Where(d => d.Day < cutoff).ExecuteDeleteAsync(stoppingToken);
                        _lastCleanup = DateTime.UtcNow;
                    }
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Salvataggio dei giorni di attività non riuscito ({Count} persi)", batch.Count);
                }
                finally
                {
                    batch.Clear();
                }
            }
        }

        /// <summary>Inserisce solo le coppie utente/giorno non ancora presenti (anche dopo un riavvio).</summary>
        public static async Task SaveAsync(ApplicationDbContext db, IReadOnlyCollection<UserActiveDay> batch, CancellationToken ct)
        {
            var distinct = batch.DistinctBy(d => (d.UserId, d.Day)).ToList();
            if (distinct.Count == 0) return;
            var userIds = distinct.Select(d => d.UserId).Distinct().ToList();
            var days = distinct.Select(d => d.Day).Distinct().ToList();
            var existing = (await db.UserActiveDays.AsNoTracking()
                    .Where(d => userIds.Contains(d.UserId) && days.Contains(d.Day))
                    .Select(d => new { d.UserId, d.Day }).ToListAsync(ct))
                .Select(d => (d.UserId, d.Day)).ToHashSet();
            var existingUsers = (await db.Users.IgnoreQueryFilters().Where(u => userIds.Contains(u.Id)).Select(u => u.Id).ToListAsync(ct)).ToHashSet();
            db.UserActiveDays.AddRange(distinct.Where(d => !existing.Contains((d.UserId, d.Day)) && existingUsers.Contains(d.UserId)));
            await db.SaveChangesAsync(ct);
        }
    }
}
