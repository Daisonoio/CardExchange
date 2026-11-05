using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Monitoring
{
    /// <summary>Avvisi agli amministratori (notifica in-app e push), con un tetto per non sommergerli.</summary>
    public class MonitoringAlerts
    {
        public const int MaxErrorAlertsPerHour = 10;
        private static readonly object Gate = new();
        private static readonly Queue<DateTime> RecentErrorAlerts = new();

        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;

        public MonitoringAlerts(ApplicationDbContext db, INotificationService notifications)
        {
            _db = db;
            _notifications = notifications;
        }

        public async Task<List<int>> AdminIdsAsync(CancellationToken ct = default) =>
            await _db.UserRoles.AsNoTracking()
                .Where(ur => ur.Role.Name == "Admin" || ur.Role.Name == "SuperAdmin")
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync(ct);

        /// <summary>Nuovo errore (o errore risolto che si ripresenta). False se il tetto orario è raggiunto.</summary>
        public async Task<bool> NewErrorAsync(ErrorGroup group, CancellationToken ct = default)
        {
            lock (Gate)
            {
                var hourAgo = DateTime.UtcNow.AddHours(-1);
                while (RecentErrorAlerts.Count > 0 && RecentErrorAlerts.Peek() < hourAgo) RecentErrorAlerts.Dequeue();
                if (RecentErrorAlerts.Count >= MaxErrorAlertsPerHour) return false;
                RecentErrorAlerts.Enqueue(DateTime.UtcNow);
            }
            var where = group.Source == ErrorSource.Client ? "nell'app" : "sul server";
            await SendAsync($"Nuovo errore {where}", group.Message, ct);
            return true;
        }

        public Task CatalogDownAsync(string key, string? error, CancellationToken ct = default) =>
            SendAsync($"Catalogo {key} non raggiungibile", $"Le ricerche e le aggiunte di carte {key} possono non funzionare. Ultimo errore: {error ?? "nessun risultato"}", ct);

        public Task CatalogBackAsync(string key, CancellationToken ct = default) =>
            SendAsync($"Catalogo {key} di nuovo raggiungibile", "Le ricerche funzionano di nuovo.", ct);

        private async Task SendAsync(string title, string body, CancellationToken ct)
        {
            foreach (var adminId in await AdminIdsAsync(ct))
                await _notifications.SendAsync(adminId, NotificationType.SystemAnnouncement, title, body.Length > 900 ? body[..900] : body, null, "Monitoring");
        }
    }
}
