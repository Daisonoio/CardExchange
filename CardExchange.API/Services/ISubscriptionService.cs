using CardExchange.Core.Entities;

namespace CardExchange.API.Authorization
{
    public interface ISubscriptionService
    {
        Task<bool> HasActiveSubscriptionAsync(int userId);
        Task<SubscriptionPlan?> GetUserActivePlanAsync(int userId);
        Task<bool> CheckLimitAsync(int userId, string limitType, int currentCount);
    }
}
