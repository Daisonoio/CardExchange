using System.Text.Json;
using CardExchange.API.Services.Moderation;
using CardExchange.API.Services.Notifications;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Stores
{
    public record DemandCard(int CardInfoId, string Name, string SetName, string? ImageUrl, int Users,
        decimal? AvgMaxPriceEur, decimal? MarketPriceEur, int CopiesInStock);

    public record StoreDemand(DateTime ComputedAt, int RadiusKm, int ActiveUsersInRadius, int InterestedUsers,
        IReadOnlyList<DemandCard> InStock, IReadOnlyList<DemandCard> Missing);

    public record StockAlertResult(int Sent, int Skipped, string? Error);

    /// <summary>
    /// Domanda locale per i negozi: voci di wishlist degli utenti attivi entro il raggio confrontate con le carte
    /// disponibili del negozio (stesse regole della wishlist). Solo aggregati con almeno 3 persone per carta.
    /// </summary>
    public class StoreDemandService
    {
        public const int DefaultRadiusKm = 10;
        public const int MinUsers = 3;
        public const int ActiveDays = 60;
        public const int MaxCardsPerList = 50;
        /// <summary>Un utente riceve al massimo un avviso dai negozi a settimana.</summary>
        public static readonly TimeSpan UserAlertInterval = TimeSpan.FromDays(7);
        /// <summary>Lo stesso negozio non avvisa due volte la stessa persona per la stessa carta entro 30 giorni.</summary>
        public static readonly TimeSpan SameCardInterval = TimeSpan.FromDays(30);
        public const int MaxAlertsPerStorePerDay = 3;

        public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly ApplicationDbContext _db;
        private readonly IUserBlockService _blocks;

        public StoreDemandService(ApplicationDbContext db, IUserBlockService blocks)
        {
            _db = db;
            _blocks = blocks;
        }

        private record Wish(int UserId, WishlistItem Item);

        /// <summary>Utenti attivi entro il raggio (escluso il proprietario e chi ha un blocco con lui) e le loro voci di wishlist.</summary>
        private async Task<(int ActiveUsers, List<Wish> Wishes, Dictionary<int, User> Users)> AreaAsync(Store store, int radiusKm, DateTime nowUtc, CancellationToken ct)
        {
            var lat = (double)store.Latitude;
            var lon = (double)store.Longitude;
            var dLat = (decimal)(radiusKm / 111.0);
            var dLon = (decimal)(radiusKm / Math.Max(1, 111.0 * Math.Cos(lat * Math.PI / 180)));
            var since = nowUtc.AddDays(-ActiveDays);
            var sinceDay = DateOnly.FromDateTime(since);
            var blocked = await _blocks.GetBlockedUserIdsAsync(store.OwnerId);

            var candidates = await _db.Users.AsNoTracking().Include(u => u.Location)
                .Where(u => u.Id != store.OwnerId && u.Location != null && u.Location.Latitude != null && u.Location.Longitude != null
                            && u.Location.Latitude >= store.Latitude - dLat && u.Location.Latitude <= store.Latitude + dLat
                            && u.Location.Longitude >= store.Longitude - dLon && u.Location.Longitude <= store.Longitude + dLon
                            && ((u.LastLoginAt != null && u.LastLoginAt >= since) || _db.UserActiveDays.Any(d => d.UserId == u.Id && d.Day >= sinceDay)))
                .ToListAsync(ct);
            var users = candidates
                .Where(u => !blocked.Contains(u.Id)
                            && Geo.DistanceKm(lat, lon, (double)u.Location!.Latitude!.Value, (double)u.Location.Longitude!.Value) <= radiusKm)
                .ToDictionary(u => u.Id);
            var ids = users.Keys.ToList();
            var wishes = await _db.WishlistItems.AsNoTracking()
                .Where(w => ids.Contains(w.UserId))
                .ToListAsync(ct);
            return (users.Count, wishes.Select(w => new Wish(w.UserId, w)).ToList(), users);
        }

        public async Task<StoreDemand> ComputeAsync(Store store, DateTime nowUtc, int radiusKm = DefaultRadiusKm, CancellationToken ct = default)
        {
            var (active, wishes, _) = await AreaAsync(store, radiusKm, nowUtc, ct);
            var cardInfoIds = wishes.Select(w => w.Item.CardInfoId).Distinct().ToList();
            var stock = await _db.Cards.AsNoTracking().Include(c => c.CardInfo)
                .Where(c => c.UserId == store.OwnerId && c.IsAvailableForTrade && cardInfoIds.Contains(c.CardInfoId))
                .ToListAsync(ct);
            var stockByInfo = stock.GroupBy(c => c.CardInfoId).ToDictionary(g => g.Key, g => g.ToList());

            var inStockWishes = new List<Wish>();
            var missingWishes = new List<Wish>();
            foreach (var w in wishes)
            {
                // Una carta "c'è" se almeno una copia soddisfa la voce; se le copie non soddisfano (condizione, lingua…) conta come mancante
                if (stockByInfo.TryGetValue(w.Item.CardInfoId, out var copies) && copies.Any(c => WishlistMatchRules.Satisfies(c, w.Item)))
                    inStockWishes.Add(w);
                else
                    missingWishes.Add(w);
            }

            var infos = await _db.CardInfos.AsNoTracking().Include(ci => ci.CardSet)
                .Where(ci => cardInfoIds.Contains(ci.Id))
                .ToDictionaryAsync(ci => ci.Id, ct);

            List<DemandCard> Aggregate(List<Wish> list) => list
                .GroupBy(w => w.Item.CardInfoId)
                .Select(g =>
                {
                    var users = g.Select(w => w.UserId).Distinct().Count();
                    var maxPrices = g.Where(w => w.Item.MaxPrice.HasValue).Select(w => w.Item.MaxPrice!.Value).ToList();
                    infos.TryGetValue(g.Key, out var info);
                    return new DemandCard(g.Key, info?.Name ?? "?", info?.CardSet?.Name ?? "", info?.ImageSmall ?? info?.ImageNormal ?? info?.ImageUrl,
                        users,
                        maxPrices.Count >= MinUsers ? Math.Round(maxPrices.Average(), 2) : null,
                        info?.PriceEur,
                        stockByInfo.TryGetValue(g.Key, out var c) ? c.Sum(x => x.Quantity) : 0);
                })
                .Where(c => c.Users >= MinUsers)
                .OrderByDescending(c => c.Users).ThenByDescending(c => c.MarketPriceEur ?? 0)
                .Take(MaxCardsPerList)
                .ToList();

            var interested = inStockWishes.Select(w => w.UserId).Distinct().Count();
            return new StoreDemand(nowUtc, radiusKm, active, interested >= MinUsers ? interested : 0,
                Aggregate(inStockWishes), Aggregate(missingWishes));
        }

        /// <summary>Calcola e salva l'istantanea (una per negozio, sostituita a ogni calcolo).</summary>
        public async Task<StoreDemand> RefreshAsync(Store store, DateTime nowUtc, CancellationToken ct = default)
        {
            var demand = await ComputeAsync(store, nowUtc, DefaultRadiusKm, ct);
            var snap = await _db.StoreDemandSnapshots.FirstOrDefaultAsync(s => s.StoreId == store.Id, ct);
            if (snap == null)
            {
                snap = new StoreDemandSnapshot { StoreId = store.Id };
                _db.StoreDemandSnapshots.Add(snap);
            }
            snap.ComputedAt = demand.ComputedAt;
            snap.RadiusKm = demand.RadiusKm;
            snap.ActiveUsersInRadius = demand.ActiveUsersInRadius;
            snap.InterestedUsers = demand.InterestedUsers;
            snap.InStockJson = JsonSerializer.Serialize(demand.InStock, Json);
            snap.MissingJson = JsonSerializer.Serialize(demand.Missing, Json);
            await _db.SaveChangesAsync(ct);
            return demand;
        }

        public static StoreDemand FromSnapshot(StoreDemandSnapshot s) => new(s.ComputedAt, s.RadiusKm, s.ActiveUsersInRadius, s.InterestedUsers,
            JsonSerializer.Deserialize<List<DemandCard>>(s.InStockJson, Json) ?? new(),
            JsonSerializer.Deserialize<List<DemandCard>>(s.MissingJson, Json) ?? new());

        /// <summary>Calcolo notturno per tutti i negozi verificati.</summary>
        public async Task<int> RefreshAllAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var stores = await _db.Stores.Where(s => s.Status == StoreStatus.Verified).ToListAsync(ct);
            foreach (var s in stores) await RefreshAsync(s, nowUtc, ct);
            return stores.Count;
        }

        /// <summary>
        /// "Avvisa gli interessati": chi nella zona cerca questa carta e la può avere dal negozio riceve una notifica.
        /// Esclusi i minorenni e chi non ha indicato l'età (niente proposte basate sulla wishlist), chi ha disattivato le proposte dei negozi,
        /// chi ha già ricevuto un avviso dai negozi in settimana o da questo negozio per questa carta nel mese.
        /// </summary>
        public async Task<StockAlertResult> NotifyInterestedAsync(Store store, int cardInfoId, DateTime nowUtc, INotificationDispatcher dispatcher, CancellationToken ct = default)
        {
            var sentToday = await _db.StoreStockAlerts.Where(a => a.StoreId == store.Id && a.SentAt >= nowUtc.AddDays(-1))
                .Select(a => a.CardInfoId).Distinct().CountAsync(ct);
            if (sentToday >= MaxAlertsPerStorePerDay)
                return new(0, 0, $"Puoi inviare al massimo {MaxAlertsPerStorePerDay} avvisi al giorno");

            var copies = await _db.Cards.AsNoTracking().Include(c => c.CardInfo)
                .Where(c => c.UserId == store.OwnerId && c.IsAvailableForTrade && c.CardInfoId == cardInfoId).ToListAsync(ct);
            if (copies.Count == 0) return new(0, 0, "Questa carta non è tra quelle disponibili nel negozio");

            var (_, wishes, users) = await AreaAsync(store, DefaultRadiusKm, nowUtc, ct);
            var today = DateOnly.FromDateTime(nowUtc);
            var targets = wishes
                .Where(w => w.Item.CardInfoId == cardInfoId && copies.Any(c => WishlistMatchRules.Satisfies(c, w.Item)))
                .Select(w => w.UserId).Distinct()
                // Età non indicata: per prudenza come minorenne
                .Where(id => users[id].BirthDate is { } bd && !AgeRules.IsMinor(bd, today))
                .ToList();
            if (targets.Count < MinUsers) return new(0, 0, "Servono almeno 3 persone interessate");

            var weekAgo = nowUtc - UserAlertInterval;
            var monthAgo = nowUtc - SameCardInterval;
            var recent = (await _db.StoreStockAlerts.AsNoTracking()
                .Where(a => targets.Contains(a.UserId) && (a.SentAt >= weekAgo || (a.StoreId == store.Id && a.CardInfoId == cardInfoId && a.SentAt >= monthAgo)))
                .Select(a => a.UserId).Distinct().ToListAsync(ct)).ToHashSet();
            var recipients = targets.Where(id => !recent.Contains(id)).ToList();
            if (recipients.Count == 0) return new(0, targets.Count, null);

            var name = copies[0].CardInfo?.Name ?? "una carta";
            var payloads = recipients.Select(id => new NotificationPayload
            {
                UserId = id,
                Type = NotificationType.StoreOffer,
                Title = $"Disponibile da {store.Name}: {name}",
                Body = $"È nella tua wishlist ed è disponibile da {store.Name}, {store.City}. Passa in negozio: prezzo e scambio si concordano di persona.",
                ReferenceId = store.Id,
                ReferenceType = "Store"
            }).ToList();
            var delivered = await dispatcher.DeliverManyAsync(payloads);
            _db.StoreStockAlerts.AddRange(recipients.Select(id => new StoreStockAlert { StoreId = store.Id, CardInfoId = cardInfoId, UserId = id, SentAt = nowUtc }));
            await _db.SaveChangesAsync(ct);
            return new(delivered, targets.Count - recipients.Count, null);
        }
    }
}
