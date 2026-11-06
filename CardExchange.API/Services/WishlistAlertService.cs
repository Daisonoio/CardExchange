using CardExchange.API.Services.Moderation;
using CardExchange.API.Services.Notifications;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services
{
    public interface IWishlistAlertService
    {
        /// <summary>
        /// Avvisa chi cerca queste carte (wishlist compatibile, entro il proprio raggio) che sono disponibili per lo scambio.
        /// Non lancia eccezioni: un errore negli avvisi non deve far fallire l'operazione che li ha generati.
        /// </summary>
        Task NotifyAvailableAsync(IEnumerable<int> cardIds);
    }

    public class WishlistAlertService : IWishlistAlertService
    {
        /// <summary>Massimo avvisi "carta trovata" per utente nelle 24 ore (evita valanghe dopo un import).</summary>
        public const int DailyLimitPerUser = 10;
        private const int DefaultRadiusKm = 50;

        private readonly ApplicationDbContext _context;
        private readonly INotificationDispatcher _dispatcher;
        private readonly IUserBlockService _blocks;
        private readonly ILogger<WishlistAlertService> _logger;

        private readonly Entitlements.IEntitlementService _entitlements;

        public WishlistAlertService(ApplicationDbContext context, INotificationDispatcher dispatcher, IUserBlockService blocks, ILogger<WishlistAlertService> logger,
            Entitlements.IEntitlementService? entitlements = null)
        {
            _entitlements = Entitlements.EntitlementExtensions.OrDefault(entitlements, context);
            _context = context;
            _dispatcher = dispatcher;
            _blocks = blocks;
            _logger = logger;
        }

        public async Task NotifyAvailableAsync(IEnumerable<int> cardIds)
        {
            try
            {
                await NotifyInternalAsync(cardIds.Distinct().ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore negli avvisi wishlist");
            }
        }

        private async Task NotifyInternalAsync(List<int> ids)
        {
            if (ids.Count == 0) return;

            var cards = await _context.Cards
                .AsNoTracking()
                .Include(c => c.CardInfo)
                .Include(c => c.User).ThenInclude(u => u.Location)
                .Where(c => ids.Contains(c.Id) && c.IsAvailableForTrade)
                .ToListAsync();
            if (cards.Count == 0) return;

            var cardInfoIds = cards.Select(c => c.CardInfoId).Distinct().ToList();
            var wishes = await _context.WishlistItems
                .AsNoTracking()
                .Include(w => w.User).ThenInclude(u => u.Location)
                .Where(w => cardInfoIds.Contains(w.CardInfoId))
                .ToListAsync();
            if (wishes.Count == 0) return;

            var since = DateTime.UtcNow.AddHours(-24);
            var wisherIds = wishes.Select(w => w.UserId).Distinct().ToList();
            var sentToday = await _context.Notifications
                .Where(n => wisherIds.Contains(n.UserId) && n.Type == NotificationType.WishlistMatch && n.CreatedAt >= since)
                .GroupBy(n => n.UserId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
            var alreadyNotified = (await _context.Notifications
                .Where(n => wisherIds.Contains(n.UserId) && n.Type == NotificationType.WishlistMatch
                            && n.ReferenceType == "Card" && n.ReferenceId != null && ids.Contains(n.ReferenceId.Value))
                .Select(n => new { n.UserId, CardId = n.ReferenceId!.Value })
                .ToListAsync())
                .Select(x => (x.UserId, x.CardId))
                .ToHashSet();

            // Avvisi immediati solo con il piano che li include; gli altri vanno nel riepilogo delle 18
            var rights = await _entitlements.GetManyAsync(wisherIds);
            var queued = (await _context.PendingWishlistAlerts.Where(a => wisherIds.Contains(a.UserId) && ids.Contains(a.CardId))
                .Select(a => new { a.UserId, a.CardId }).ToListAsync()).Select(a => (a.UserId, a.CardId)).ToHashSet();
            var now = DateTime.UtcNow;

            int sent = 0;
            foreach (var card in cards)
            {
                var ownerLocation = card.User?.Location;
                if (ownerLocation?.Latitude == null || ownerLocation.Longitude == null) continue;
                var blocked = await _blocks.GetBlockedUserIdsAsync(card.UserId);

                foreach (var wish in wishes.Where(w => w.CardInfoId == card.CardInfoId && w.UserId != card.UserId))
                {
                    if (blocked.Contains(wish.UserId) || alreadyNotified.Contains((wish.UserId, card.Id))) continue;
                    if (!WishlistMatchRules.Satisfies(card, wish)) continue;

                    var wisherLocation = wish.User?.Location;
                    if (wisherLocation?.Latitude == null || wisherLocation.Longitude == null) continue;
                    var distance = Geo.DistanceKm((double)wisherLocation.Latitude.Value, (double)wisherLocation.Longitude.Value,
                        (double)ownerLocation.Latitude.Value, (double)ownerLocation.Longitude.Value);
                    var radius = wisherLocation.MaxDistanceKm > 0 ? wisherLocation.MaxDistanceKm : DefaultRadiusKm;
                    var wisherRights = rights.GetValueOrDefault(wish.UserId);
                    if (wisherRights != null) radius = Math.Min(radius, wisherRights.Limit(PlanLimit.RadiusKm));
                    if (distance > radius) continue;

                    if (wisherRights == null || !wisherRights.Has(Capability.InstantAlerts))
                    {
                        if (queued.Add((wish.UserId, card.Id)))
                            _context.PendingWishlistAlerts.Add(new PendingWishlistAlert { UserId = wish.UserId, CardId = card.Id, CreatedAt = now });
                        continue;
                    }

                    if (sentToday.GetValueOrDefault(wish.UserId) >= DailyLimitPerUser) continue;

                    var name = card.CardInfo?.Name ?? "Una carta";
                    var where = distance < 1 ? "a meno di 1 km" : $"a {Math.Round(distance)} km";
                    await _dispatcher.DispatchAsync(NotificationType.WishlistMatch, new Dictionary<string, object>
                    {
                        ["UserId"] = wish.UserId,
                        ["CardId"] = card.Id,
                        ["CardName"] = name,
                        ["Title"] = $"{name} disponibile vicino a te",
                        ["Body"] = $"{card.User!.Username} ha messo in scambio {name} {where} da te."
                    });

                    sentToday[wish.UserId] = sentToday.GetValueOrDefault(wish.UserId) + 1;
                    alreadyNotified.Add((wish.UserId, card.Id));
                    sent++;
                }
            }

            await _context.SaveChangesAsync();
            if (sent > 0)
                _logger.LogInformation("Avvisi wishlist inviati: {Count}", sent);
        }
    }
}
