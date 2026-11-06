using System.Globalization;
using System.Text.Json;
using CardExchange.API.Configuration;
using CardExchange.API.Services.Email;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CardExchange.API.Services.Stores
{
    public record StoreReportContent(StoreStats Stats, StoreStats Previous);

    /// <summary>
    /// Riepilogo mensile per i negozi verificati: "CardExchange ti ha portato N persone in negozio".
    /// Gira ogni notte; per ogni negozio crea il riepilogo del mese appena concluso una sola volta.
    /// </summary>
    public class StoreMonthlyReports
    {
        public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private static readonly TimeZoneInfo Italy = ResolveItaly();

        private readonly ApplicationDbContext _db;
        private readonly StoreStatsService _stats;
        private readonly INotificationService _notifications;
        private readonly IEmailSender _email;
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<StoreMonthlyReports> _logger;

        public StoreMonthlyReports(ApplicationDbContext db, StoreStatsService stats, INotificationService notifications, IEmailSender email,
            IOptions<EmailSettings> emailSettings, ILogger<StoreMonthlyReports> logger)
        {
            _db = db;
            _stats = stats;
            _notifications = notifications;
            _email = email;
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        /// <summary>Inizio e fine (UTC) di un mese italiano.</summary>
        public static (DateTime FromUtc, DateTime ToUtc) MonthBounds(DateOnly month) =>
            (TimeZoneInfo.ConvertTimeToUtc(month.ToDateTime(TimeOnly.MinValue), Italy),
             TimeZoneInfo.ConvertTimeToUtc(month.AddMonths(1).ToDateTime(TimeOnly.MinValue), Italy));

        /// <summary>Il mese concluso più recente rispetto a "adesso" (ora italiana).</summary>
        public static DateOnly LastCompletedMonth(DateTime nowUtc)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, Italy);
            return new DateOnly(local.Year, local.Month, 1).AddMonths(-1);
        }

        public async Task<int> RunAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var month = LastCompletedMonth(nowUtc);
            var (from, to) = MonthBounds(month);
            var (prevFrom, prevTo) = MonthBounds(month.AddMonths(-1));

            var stores = await _db.Stores.Include(s => s.Owner)
                .Where(s => s.Status == StoreStatus.Verified && s.CreatedAt < to
                            && !_db.StoreMonthlyReports.Any(r => r.StoreId == s.Id && r.Month == month))
                .ToListAsync(ct);

            var created = 0;
            foreach (var store in stores)
            {
                try
                {
                    var content = new StoreReportContent(
                        await _stats.ComputeAsync(store.Id, from, to, ct),
                        await _stats.ComputeAsync(store.Id, prevFrom, prevTo, ct));
                    var report = new StoreMonthlyReport { StoreId = store.Id, Month = month, MetricsJson = JsonSerializer.Serialize(content, Json) };
                    _db.StoreMonthlyReports.Add(report);
                    await _db.SaveChangesAsync(ct);
                    created++;

                    await SendAsync(store, month, content.Stats, ct);
                    report.SentAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Riepilogo mensile non creato per il negozio {StoreId}", store.Id);
                }
            }
            if (created > 0) _logger.LogInformation("Riepiloghi mensili dei negozi: {Count} per {Month}", created, month);
            return created;
        }

        public static string Headline(StoreStats s) => s.PeopleBroughtIn switch
        {
            0 => "Questo mese nessuno è ancora venuto da te tramite CardExchange",
            1 => "CardExchange ti ha portato 1 persona in negozio",
            var n => $"CardExchange ti ha portato {n} persone in negozio"
        };

        private async Task SendAsync(Store store, DateOnly month, StoreStats s, CancellationToken ct)
        {
            var monthName = month.ToDateTime(TimeOnly.MinValue).ToString("MMMM yyyy", new CultureInfo("it-IT"));
            var lines = new List<string>
            {
                $"{s.TradesCompletedAtStore} scambi conclusi da te",
                $"{s.EventCheckIns} presenze ai tuoi eventi",
                $"{s.NewFollowers} nuovi follower, {s.QrSignups} iscritti dal tuo QR",
                $"{s.ProfileViews} visite alla scheda del negozio"
            };
            // Commissioni sul Premium dei clienti portati dal negozio, maturate nel mese
            var (fromUtc, toUtc) = MonthBounds(month);
            var commissions = await _db.StoreCommissions.Where(c => c.StoreId == store.Id && c.PaidAt >= fromUtc && c.PaidAt < toUtc
                && c.Status != CommissionStatus.Reversed).Select(c => c.AmountEur).ToListAsync(ct);
            if (commissions.Count > 0)
                lines.Add($"{commissions.Sum():0.00} € di commissioni dal Premium dei tuoi clienti");
            var link = $"{_emailSettings.FrontendBaseUrl.TrimEnd('/')}/stores/{store.Id}/results";
            await _notifications.SendAsync(store.OwnerId, NotificationType.StoreActivity, $"Il tuo mese a {monthName}: {store.Name}",
                Headline(s) + ". " + string.Join(" · ", lines), store.Id, "StoreReport");
            if (store.Owner.EmailConfirmed)
                await _email.SendAsync(store.Owner.Email, $"{store.Owner.FirstName} {store.Owner.LastName}".Trim(),
                    EmailTemplates.StoreMonthlyReport(store.Owner.FirstName, store.Name, monthName, Headline(s), lines, link, store.Owner.Language), ct);
        }

        private static TimeZoneInfo ResolveItaly()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time"); }
        }
    }
}
