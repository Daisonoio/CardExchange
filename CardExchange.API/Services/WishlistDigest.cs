using CardExchange.API.Services.Notifications;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services
{
    /// <summary>
    /// Riepilogo serale (alle 18, ora dell'utente) degli avvisi sulla wishlist per chi non ha gli avvisi immediati.
    /// Dice anche quante carte sono già state prese e quanto prima l'avrebbe saputo con Premium.
    /// </summary>
    public class WishlistDigest
    {
        public const int Hour = 18;
        public const int MaxNames = 3;
        private readonly ApplicationDbContext _db;
        private readonly INotificationDispatcher _dispatcher;
        private readonly ILogger<WishlistDigest> _logger;

        public WishlistDigest(ApplicationDbContext db, INotificationDispatcher dispatcher, ILogger<WishlistDigest> logger)
        {
            _db = db;
            _dispatcher = dispatcher;
            _logger = logger;
        }

        public async Task<int> RunAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var pending = await _db.PendingWishlistAlerts.Where(a => a.SentAt == null).ToListAsync(ct);
            if (pending.Count == 0) return 0;
            var userIds = pending.Select(a => a.UserId).Distinct().ToList();
            var zones = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).Select(u => new { u.Id, u.TimeZone }).ToDictionaryAsync(u => u.Id, u => u.TimeZone, ct);
            var cardIds = pending.Select(a => a.CardId).Distinct().ToList();
            var cards = await _db.Cards.IgnoreQueryFilters().AsNoTracking().Include(c => c.CardInfo)
                .Where(c => cardIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

            var payloads = new List<NotificationPayload>();
            foreach (var group in pending.GroupBy(a => a.UserId))
            {
                var zone = TimeZones.Resolve(zones.GetValueOrDefault(group.Key));
                var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone);
                if (local.Hour < Hour) continue;
                var cutoff = TimeZoneInfo.ConvertTimeToUtc(local.Date.AddHours(Hour), zone);
                var due = group.Where(a => a.CreatedAt <= cutoff).ToList();
                if (due.Count == 0) continue;

                var names = due.Select(a => cards.GetValueOrDefault(a.CardId)?.CardInfo?.Name).Where(n => n != null).Distinct().ToList();
                var gone = due.Count(a => !cards.TryGetValue(a.CardId, out var c) || c.IsDeleted || !c.IsAvailableForTrade);
                var hoursEarlier = Math.Max(1, (int)Math.Round((nowUtc - due.Min(a => a.CreatedAt)).TotalHours));
                var list = string.Join(", ", names.Take(MaxNames)) + (names.Count > MaxNames ? $" e altre {names.Count - MaxNames}" : "");
                var body = (due.Count == 1 ? $"Oggi è stata disponibile vicino a te una carta che cerchi: {list}." : $"Oggi sono state disponibili vicino a te {due.Count} carte che cerchi: {list}.")
                           + (gone > 0 ? (gone == 1 ? " Una è già stata presa." : $" {gone} sono già state prese.") : "")
                           + $" Con Premium l'avresti saputo {(hoursEarlier == 1 ? "1 ora" : $"{hoursEarlier} ore")} prima.";
                payloads.Add(new NotificationPayload
                {
                    UserId = group.Key, Type = NotificationType.WishlistMatch,
                    Title = due.Count == 1 ? "Una carta che cerchi è disponibile" : $"{due.Count} carte che cerchi sono disponibili",
                    Body = body, ReferenceType = "Wishlist"
                });
                foreach (var a in due) a.SentAt = nowUtc;
            }
            if (payloads.Count == 0) return 0;
            await _dispatcher.DeliverManyAsync(payloads);
            await _db.SaveChangesAsync(ct);
            // Pulizia: avvisi inviati da più di 30 giorni
            var old = await _db.PendingWishlistAlerts.Where(a => a.SentAt != null && a.SentAt < nowUtc.AddDays(-30)).Take(1000).ToListAsync(ct);
            if (old.Count > 0)
            {
                _db.PendingWishlistAlerts.RemoveRange(old);
                await _db.SaveChangesAsync(ct);
            }
            _logger.LogInformation("Riepiloghi serali wishlist: {Count}", payloads.Count);
            return payloads.Count;
        }
    }
}
