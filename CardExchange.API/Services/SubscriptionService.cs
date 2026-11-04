using CardExchange.API.Authorization;
using CardExchange.API.Configuration;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CardExchange.API.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ApplicationDbContext _context;

        // Piano Free di default (non salvato a DB). Il limite di carte arriva da PlanLimits:FreeMaxCards
        private readonly SubscriptionPlan FreePlan;

        private static SubscriptionPlan BuildFreePlan(int maxCards) => new()
        {
            Tier = SubscriptionTier.Free,
            MaxCards = maxCards,
            MaxWishlistItems = 20,
            MaxActiveTradeOffers = 5,
            MaxDailyMessages = 10,
            CanSearchAdvanced = false,
            CanSearchGeographic = false,
            CanExportCollection = false,
            CanViewStatistics = false,
            CanSaveSearches = false,
            HasPriorityListing = false,
            HasVerifiedBadge = false,
            HasWishlistAlerts = false
        };

        public SubscriptionService(ApplicationDbContext context, IOptions<PlanLimitsOptions>? planLimits = null)
        {
            _context = context;
            FreePlan = BuildFreePlan((planLimits?.Value ?? new PlanLimitsOptions()).EffectiveFreeMaxCards);
        }

        public async Task<bool> HasActiveSubscriptionAsync(int userId)
        {
            return await _context.Set<UserSubscription>()
                .AnyAsync(s => s.UserId == userId
                    && s.Status == SubscriptionStatus.Active
                    && s.StartDate <= DateTime.UtcNow && s.EndDate > DateTime.UtcNow
                    && !s.IsDeleted);
        }

        public async Task<Dictionary<int, SubscriptionPlan>> GetActivePaidPlansAsync(IEnumerable<int> userIds)
        {
            var ids = userIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, SubscriptionPlan>();
            var now = DateTime.UtcNow;
            var subs = await _context.Set<UserSubscription>()
                .AsNoTracking()
                .Include(s => s.Plan)
                .Where(s => ids.Contains(s.UserId) && s.Status == SubscriptionStatus.Active
                            && s.StartDate <= now && s.EndDate > now && !s.IsDeleted)
                .ToListAsync();
            return subs
                .GroupBy(s => s.UserId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.Plan.Tier).First().Plan);
        }

        public async Task<SubscriptionPlan?> GetUserActivePlanAsync(int userId)
        {
            var subscription = await _context.Set<UserSubscription>()
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId
                    && s.Status == SubscriptionStatus.Active
                    && s.StartDate <= DateTime.UtcNow && s.EndDate > DateTime.UtcNow
                    && !s.IsDeleted)
                .OrderByDescending(s => s.Plan.Tier)
                .FirstOrDefaultAsync();

            return subscription?.Plan ?? FreePlan;
        }

        public async Task<bool> CheckLimitAsync(int userId, string limitType, int currentCount)
        {
            var plan = await GetUserActivePlanAsync(userId);
            if (plan == null) return false;

            return limitType switch
            {
                "cards" => currentCount < plan.MaxCards,
                "wishlist" => currentCount < plan.MaxWishlistItems,
                "trades" => currentCount < plan.MaxActiveTradeOffers,
                "messages" => currentCount < plan.MaxDailyMessages,
                _ => false
            };
        }
    }
}
