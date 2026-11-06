using CardExchange.API.Configuration;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CardExchange.API.Services.Entitlements
{
    /// <summary>Unico punto da cui l'app decide cosa può fare un utente in base ai suoi piani.</summary>
    public interface IEntitlementService
    {
        Task<UserEntitlements> GetAsync(int userId);
        Task<bool> HasAsync(int userId, Capability capability);
        Task<int> LimitAsync(int userId, PlanLimit limit);
        /// <summary>Diritti di più utenti in una sola lettura (badge, priorità nei match, negozi in evidenza).</summary>
        Task<Dictionary<int, UserEntitlements>> GetManyAsync(IEnumerable<int> userIds);
        void Invalidate(int userId);
    }

    public class EntitlementService : IEntitlementService
    {
        public static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);
        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly SubscriptionPlan _free;

        public EntitlementService(ApplicationDbContext db, IMemoryCache cache, IOptions<PlanLimitsOptions>? limits = null)
        {
            _db = db;
            _cache = cache;
            _free = PlanCatalog.Free((limits?.Value ?? new PlanLimitsOptions()).EffectiveFreeMaxCards);
        }

        public static string Key(int userId) => $"entitlements:{userId}";

        public async Task<UserEntitlements> GetAsync(int userId)
        {
            if (_cache.TryGetValue(Key(userId), out UserEntitlements? cached) && cached != null) return cached;
            var result = (await LoadAsync(new[] { userId })).GetValueOrDefault(userId) ?? UserEntitlements.Combine(_free, Array.Empty<SubscriptionPlan>());
            _cache.Set(Key(userId), result, CacheFor);
            return result;
        }

        public async Task<bool> HasAsync(int userId, Capability capability) => (await GetAsync(userId)).Has(capability);

        public async Task<int> LimitAsync(int userId, PlanLimit limit) => (await GetAsync(userId)).Limit(limit);

        public async Task<Dictionary<int, UserEntitlements>> GetManyAsync(IEnumerable<int> userIds)
        {
            var ids = userIds.Distinct().ToList();
            var result = new Dictionary<int, UserEntitlements>();
            var missing = new List<int>();
            foreach (var id in ids)
            {
                if (_cache.TryGetValue(Key(id), out UserEntitlements? e) && e != null) result[id] = e;
                else missing.Add(id);
            }
            if (missing.Count == 0) return result;
            var loaded = await LoadAsync(missing);
            foreach (var id in missing)
            {
                var e = loaded.GetValueOrDefault(id) ?? UserEntitlements.Combine(_free, Array.Empty<SubscriptionPlan>());
                _cache.Set(Key(id), e, CacheFor);
                result[id] = e;
            }
            return result;
        }

        public void Invalidate(int userId) => _cache.Remove(Key(userId));

        private async Task<Dictionary<int, UserEntitlements>> LoadAsync(IReadOnlyCollection<int> userIds)
        {
            var now = DateTime.UtcNow;
            var subs = await _db.UserSubscriptions.AsNoTracking().Include(s => s.Plan)
                .Where(s => userIds.Contains(s.UserId) && s.Status == SubscriptionStatus.Active && s.StartDate <= now && s.EndDate > now)
                .ToListAsync();
            return subs.GroupBy(s => s.UserId)
                .ToDictionary(g => g.Key, g => UserEntitlements.Combine(_free, g.Select(s => s.Plan)));
        }
    }

    /// <summary>Quando cambia un abbonamento (attivazione, rinnovo, scadenza, rimborso…) i diritti in cache si ricalcolano.</summary>
    public class EntitlementCacheInterceptor : SaveChangesInterceptor
    {
        private readonly IMemoryCache _cache;
        public EntitlementCacheInterceptor(IMemoryCache cache) => _cache = cache;

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Invalidate(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            Invalidate(eventData.Context);
            return base.SavingChangesAsync(eventData, result, ct);
        }

        private void Invalidate(DbContext? context)
        {
            if (context == null) return;
            foreach (var entry in context.ChangeTracker.Entries<UserSubscription>())
                if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                    _cache.Remove(EntitlementService.Key(entry.Entity.UserId));
        }
    }
}

namespace CardExchange.API.Services.Entitlements
{
    public static class EntitlementExtensions
    {
        /// <summary>Il servizio dei diritti della richiesta (o uno nuovo, senza cache condivisa, fuori da una richiesta HTTP).</summary>
        public static IEntitlementService Entitlements(this Microsoft.AspNetCore.Mvc.ControllerBase controller, ApplicationDbContext db) =>
            controller.HttpContext?.RequestServices?.GetService(typeof(IEntitlementService)) as IEntitlementService
            ?? new EntitlementService(db, new MemoryCache(new MemoryCacheOptions()));

        public static IEntitlementService OrDefault(this IEntitlementService? service, ApplicationDbContext db) =>
            service ?? new EntitlementService(db, new MemoryCache(new MemoryCacheOptions()));

        /// <summary>Utenti (tra quelli dati) che hanno un certo diritto.</summary>
        public static async Task<HashSet<int>> HoldersAsync(this IEntitlementService service, IEnumerable<int> userIds, CardExchange.Core.Rules.Capability capability) =>
            (await service.GetManyAsync(userIds)).Where(kv => kv.Value.Has(capability)).Select(kv => kv.Key).ToHashSet();
    }
}

namespace CardExchange.API.Services.Entitlements
{
    /// <summary>
    /// Accesso anticipato Premium: le carte caricate da negozi e banchi nelle ultime 2 ore sono visibili e
    /// prenotabili solo da chi ha il diritto EarlyAccess (e dal proprietario).
    /// </summary>
    public static class EarlyAccess
    {
        public static readonly TimeSpan Window = TimeSpan.FromHours(2);

        /// <summary>Id delle carte che chi guarda non può ancora vedere (vuoto per chi ha l'accesso anticipato).</summary>
        public static async Task<HashSet<int>> HiddenAsync(IEntitlementService entitlements, int? viewerId,
            IEnumerable<(int CardId, int OwnerId, DateTime CreatedAt)> cards, DateTime nowUtc)
        {
            var recent = cards.Where(c => c.CreatedAt > nowUtc - Window && c.OwnerId != viewerId).ToList();
            if (recent.Count == 0) return new HashSet<int>();
            if (viewerId is > 0 && await entitlements.HasAsync(viewerId.Value, CardExchange.Core.Rules.Capability.EarlyAccess)) return new HashSet<int>();
            var sellers = (await entitlements.GetManyAsync(recent.Select(c => c.OwnerId)))
                .Where(kv => kv.Value.Has(CardExchange.Core.Rules.Capability.FeaturedStore) || kv.Value.Has(CardExchange.Core.Rules.Capability.SellerShowcase))
                .Select(kv => kv.Key).ToHashSet();
            return recent.Where(c => sellers.Contains(c.OwnerId)).Select(c => c.CardId).ToHashSet();
        }

        /// <summary>Minuti che mancano alla fine dell'accesso anticipato di una carta.</summary>
        public static int MinutesLeft(DateTime createdAt, DateTime nowUtc) =>
            Math.Max(1, (int)Math.Ceiling((createdAt + Window - nowUtc).TotalMinutes));
    }
}
