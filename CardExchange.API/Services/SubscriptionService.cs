using CardExchange.API.Authorization;
using CardExchange.API.Configuration;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CardExchange.API.Services
{
    /// <summary>
    /// Facciata sopra <see cref="Entitlements.IEntitlementService"/>: limiti e "piano effettivo" (unione dei piani attivi)
    /// arrivano dal servizio dei diritti, così ogni controllo di piano passa da un solo punto.
    /// </summary>
    public class SubscriptionService : ISubscriptionService
    {
        private readonly Entitlements.IEntitlementService _entitlements;

        public SubscriptionService(ApplicationDbContext context, IOptions<PlanLimitsOptions>? planLimits = null, Entitlements.IEntitlementService? entitlements = null)
        {
            _entitlements = entitlements ?? new Entitlements.EntitlementService(context,
                new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()), planLimits);
        }

        public async Task<bool> HasActiveSubscriptionAsync(int userId) => (await _entitlements.GetAsync(userId)).IsPaid;

        public async Task<Dictionary<int, SubscriptionPlan>> GetActivePaidPlansAsync(IEnumerable<int> userIds)
        {
            var all = await _entitlements.GetManyAsync(userIds);
            return all.Where(kv => kv.Value.IsPaid).ToDictionary(kv => kv.Key, kv => ToPlan(kv.Value));
        }

        public async Task<SubscriptionPlan?> GetUserActivePlanAsync(int userId) => ToPlan(await _entitlements.GetAsync(userId));

        public async Task<bool> CheckLimitAsync(int userId, string limitType, int currentCount)
        {
            var e = await _entitlements.GetAsync(userId);
            return limitType switch
            {
                "cards" => currentCount < e.Limit(PlanLimit.Cards),
                "wishlist" => currentCount < e.Limit(PlanLimit.WishlistItems),
                "trades" => currentCount < e.Limit(PlanLimit.ActiveTradeOffers),
                "messages" => currentCount < e.Limit(PlanLimit.DailyMessages),
                _ => false
            };
        }

        /// <summary>Piano "effettivo" (per i chiamanti che leggono ancora i campi del piano).</summary>
        public static SubscriptionPlan ToPlan(UserEntitlements e) => new()
        {
            Name = e.PlanName,
            Tier = e.Tier,
            MaxCards = e.Limit(PlanLimit.Cards),
            MaxWishlistItems = e.Limit(PlanLimit.WishlistItems),
            MaxActiveTradeOffers = e.Limit(PlanLimit.ActiveTradeOffers),
            MaxDailyMessages = e.Limit(PlanLimit.DailyMessages),
            MaxBroadcastsPerDay = e.Limit(PlanLimit.BroadcastsPerDay),
            MaxRadiusKm = e.Limit(PlanLimit.RadiusKm),
            MaxActiveReservations = e.Limit(PlanLimit.ActiveReservations),
            MonthlyPromotionCredits = e.Limit(PlanLimit.MonthlyPromotionCredits),
            CanSearchAdvanced = e.Has(Capability.AdvancedSearch),
            CanSearchGeographic = e.Has(Capability.GeographicSearch),
            CanExportCollection = e.Has(Capability.ExportCollection),
            CanViewStatistics = e.Has(Capability.Statistics),
            CanSaveSearches = e.Has(Capability.SavedSearches),
            HasPriorityListing = e.Has(Capability.PriorityListing),
            HasVerifiedBadge = e.Has(Capability.VerifiedBadge),
            HasWishlistAlerts = e.Has(Capability.WishlistAlerts),
            HasInstantAlerts = e.Has(Capability.InstantAlerts),
            HasEarlyAccess = e.Has(Capability.EarlyAccess),
            HasPriceAlerts = e.Has(Capability.PriceAlerts),
            HasFeaturedStore = e.Has(Capability.FeaturedStore),
            HasLocalDemand = e.Has(Capability.LocalDemand),
            HasBuylist = e.Has(Capability.Buylist),
            HasReservations = e.Has(Capability.Reservations),
            HasSellerShowcase = e.Has(Capability.SellerShowcase),
            HidesBrandSponsors = e.Has(Capability.HidesBrandSponsors)
        };
    }
}
