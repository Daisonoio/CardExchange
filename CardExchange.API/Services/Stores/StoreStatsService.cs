using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Stores
{
    public class StoreStats
    {
        /// <summary>Persone diverse portate fisicamente in negozio: scambi conclusi lì e presenze agli eventi.</summary>
        public int PeopleBroughtIn { get; set; }
        public int ProfileViews { get; set; }
        public int QrScans { get; set; }
        public int QrSignups { get; set; }
        public int NewFollowers { get; set; }
        public int MeetupsScheduled { get; set; }
        public int TradesCompletedAtStore { get; set; }
        public int EventRegistrations { get; set; }
        public int EventCheckIns { get; set; }
    }

    /// <summary>Statistiche di un negozio in un periodo, dalle attività registrate e dalle registrazioni dal QR.</summary>
    public class StoreStatsService
    {
        private readonly ApplicationDbContext _db;
        public StoreStatsService(ApplicationDbContext db) => _db = db;

        public async Task<StoreStats> ComputeAsync(int storeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
        {
            var rows = await _db.StoreActivities.AsNoTracking()
                .Where(a => a.StoreId == storeId && a.OccurredAt >= fromUtc && a.OccurredAt < toUtc)
                .Select(a => new { a.Type, a.UserId, a.RefId })
                .ToListAsync(ct);

            int Count(StoreActivityType t) => rows.Count(r => r.Type == t);
            var broughtIn = rows
                .Where(r => (r.Type == StoreActivityType.MeetupCompleted || r.Type == StoreActivityType.EventCheckIn
                             || r.Type == StoreActivityType.ReservationPickedUp || r.Type == StoreActivityType.BuyOfferCompleted) && r.UserId != null)
                .Select(r => r.UserId!.Value)
                .Distinct()
                .Count();

            return new StoreStats
            {
                PeopleBroughtIn = broughtIn,
                ProfileViews = Count(StoreActivityType.ProfileView),
                QrScans = Count(StoreActivityType.QrScan),
                QrSignups = await _db.StoreAttributions.CountAsync(a => a.StoreId == storeId && a.CreatedAt >= fromUtc && a.CreatedAt < toUtc, ct),
                NewFollowers = Count(StoreActivityType.Follow),
                MeetupsScheduled = Count(StoreActivityType.MeetupScheduled),
                // Ogni scambio concluso registra le due persone: si contano gli incontri
                TradesCompletedAtStore = rows.Where(r => r.Type == StoreActivityType.MeetupCompleted).Select(r => r.RefId).Distinct().Count(),
                EventRegistrations = Count(StoreActivityType.EventRegistration),
                EventCheckIns = Count(StoreActivityType.EventCheckIn)
            };
        }
    }
}
