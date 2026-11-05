using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Metrics
{
    public record RateMetric(int Eligible, int Count, double? Rate);

    public record WeekPoint(DateTime From, int ActiveUsers, int NewUsers, int TradesCompleted, int Broadcasts);

    public record CityUsers(string City, int Users);

    /// <summary>Le metriche del piano di lancio ("Come misuriamo"), per città o in totale.</summary>
    public record PlanMetricsResult(
        string? City,
        RateMetric MatchWithin48h,
        int WeeklyActiveUsers,
        int TradesCompletedLastWeek,
        RateMetric RetentionAfter4Weeks,
        int ActiveStores,
        int BroadcastsLastWeek,
        int TotalUsers,
        IReadOnlyList<WeekPoint> Weeks,
        IReadOnlyList<CityUsers> Cities);

    public class PlanMetrics
    {
        /// <summary>Carte disponibili (quantità) oltre cui un negozio conta come attivo, anche senza eventi.</summary>
        public const int ActiveStoreMinCards = 50;
        private static readonly TimeZoneInfo Italy = ResolveItaly();
        private readonly ApplicationDbContext _db;

        public PlanMetrics(ApplicationDbContext db) => _db = db;

        public static DateOnly ItalianDay(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, Italy));

        public async Task<PlanMetricsResult> ComputeAsync(string? city, DateTime nowUtc, int weeks = 8, CancellationToken ct = default)
        {
            city = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
            var users = UsersIn(city);

            // Match entro 48 ore: nuovi utenti degli ultimi 30 giorni già controllati
            var checkedUsers = users.Where(u => u.MatchWithin48h != null && u.CreatedAt >= nowUtc.AddDays(-30));
            var match = Rate(await checkedUsers.CountAsync(ct), await checkedUsers.CountAsync(u => u.MatchWithin48h == true, ct));

            // Ritorno dopo 4 settimane: iscritti tra 8 e 4 settimane fa, attivi dal 28° giorno in poi
            var cohort = users.Where(u => u.CreatedAt >= nowUtc.AddDays(-56) && u.CreatedAt < nowUtc.AddDays(-28))
                .Select(u => new { u.Id, u.CreatedAt, u.LastLoginAt });
            var cohortList = await cohort.ToListAsync(ct);
            var cohortIds = cohortList.Select(u => u.Id).ToList();
            var lastActive = await _db.UserActiveDays.AsNoTracking()
                .Where(d => cohortIds.Contains(d.UserId))
                .GroupBy(d => d.UserId)
                .Select(g => new { UserId = g.Key, Last = g.Max(d => d.Day) })
                .ToDictionaryAsync(x => x.UserId, x => x.Last, ct);
            var retained = cohortList.Count(u =>
                (lastActive.TryGetValue(u.Id, out var last) && last >= ItalianDay(u.CreatedAt.AddDays(28)))
                || (u.LastLoginAt.HasValue && u.LastLoginAt >= u.CreatedAt.AddDays(28)));
            var retention = Rate(cohortList.Count, retained);

            // Negozi attivi: verificati, con almeno 50 carte disponibili o un evento negli ultimi 30 giorni
            var monthAgo = nowUtc.AddDays(-30);
            var storesQuery = _db.Stores.AsNoTracking().Where(s => s.Status == StoreStatus.Verified);
            if (city != null) storesQuery = storesQuery.Where(s => s.City.ToLower() == city.ToLower());
            var activeStores = await storesQuery.CountAsync(s =>
                _db.Cards.Where(c => c.UserId == s.OwnerId && c.IsAvailableForTrade).Sum(c => (int?)c.Quantity) >= ActiveStoreMinCards
                || _db.Events.Any(e => e.StoreId == s.Id && e.StartDate >= monthAgo && e.StartDate <= nowUtc), ct);

            var series = new List<WeekPoint>();
            for (var i = weeks - 1; i >= 0; i--)
            {
                var to = nowUtc.AddDays(-7 * i);
                var from = to.AddDays(-7);
                series.Add(await WeekAsync(users, from, to, ct));
            }
            var lastWeek = series[^1];

            var cities = await _db.UserLocations.AsNoTracking()
                .Where(l => l.City != "" && l.User.IsActive)
                .GroupBy(l => l.City)
                .Select(g => new { City = g.Key, Users = g.Count() })
                .OrderByDescending(c => c.Users)
                .Take(40)
                .ToListAsync(ct);

            return new PlanMetricsResult(city, match, lastWeek.ActiveUsers, lastWeek.TradesCompleted, retention, activeStores,
                lastWeek.Broadcasts, await users.CountAsync(ct), series, MergeCase(cities.Select(c => new CityUsers(c.City, c.Users))).Take(20).ToList());
        }

        private async Task<WeekPoint> WeekAsync(IQueryable<User> users, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            var ids = users.Select(u => u.Id);
            var fromDay = ItalianDay(fromUtc);
            var toDay = ItalianDay(toUtc);
            var active = await _db.UserActiveDays.AsNoTracking()
                .Where(d => d.Day > fromDay && d.Day <= toDay && ids.Contains(d.UserId))
                .Select(d => d.UserId).Distinct().CountAsync(ct);
            var newUsers = await users.CountAsync(u => u.CreatedAt >= fromUtc && u.CreatedAt < toUtc, ct);
            var trades = await _db.TradeOffers.AsNoTracking()
                .CountAsync(t => t.Status == TradeOfferStatus.Completed && t.CompletedDate >= fromUtc && t.CompletedDate < toUtc
                                 && (ids.Contains(t.SenderId) || ids.Contains(t.ReceiverId)), ct);
            var broadcasts = await _db.LocationBroadcasts.AsNoTracking()
                .CountAsync(b => b.CreatedAt >= fromUtc && b.CreatedAt < toUtc && ids.Contains(b.AuthorId), ct);
            return new WeekPoint(fromUtc, active, newUsers, trades, broadcasts);
        }

        private IQueryable<User> UsersIn(string? city)
        {
            var q = _db.Users.AsNoTracking();
            return city == null ? q : q.Where(u => u.Location != null && u.Location.City.ToLower() == city.ToLower());
        }

        private static RateMetric Rate(int eligible, int count) =>
            new(eligible, count, eligible == 0 ? null : Math.Round((double)count / eligible, 3));

        /// <summary>"Milano" e "milano" sono la stessa città.</summary>
        private static List<CityUsers> MergeCase(IEnumerable<CityUsers> cities) =>
            cities.GroupBy(c => c.City.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new CityUsers(g.OrderByDescending(c => c.Users).First().City.Trim(), g.Sum(c => c.Users)))
                .OrderByDescending(c => c.Users).ToList();

        private static TimeZoneInfo ResolveItaly()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time"); }
        }
    }
}
