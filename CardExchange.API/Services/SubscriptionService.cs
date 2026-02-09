using CardExchange.API.Authorization;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ApplicationDbContext _context;

        // Piano Free di default (hardcoded per performance)
        private static readonly SubscriptionPlan FreePlan = new()
        {
            Tier = SubscriptionTier.Free,
            MaxCards = 50,
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

        public SubscriptionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> HasActiveSubscriptionAsync(int userId)
        {
            return await _context.Set<UserSubscription>()
                .AnyAsync(s => s.UserId == userId
                    && s.Status == SubscriptionStatus.Active
                    && s.EndDate > DateTime.UtcNow
                    && !s.IsDeleted);
        }

        public async Task<SubscriptionPlan?> GetUserActivePlanAsync(int userId)
        {
            var subscription = await _context.Set<UserSubscription>()
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId
                    && s.Status == SubscriptionStatus.Active
                    && s.EndDate > DateTime.UtcNow
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
