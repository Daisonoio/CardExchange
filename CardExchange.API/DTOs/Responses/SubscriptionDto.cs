using CardExchange.Core.Entities;

namespace CardExchange.API.DTOs.Responses
{
    public class SubscriptionPlanDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Tier { get; set; } = string.Empty;
        public string Cycle { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Currency { get; set; } = "EUR";

        // Limiti
        public int MaxCards { get; set; }
        public int MaxWishlistItems { get; set; }
        public int MaxActiveTradeOffers { get; set; }
        public int MaxDailyMessages { get; set; }
        public bool CanSearchAdvanced { get; set; }
        public bool CanSearchGeographic { get; set; }
        public bool CanExportCollection { get; set; }
        public bool CanViewStatistics { get; set; }
        public bool CanSaveSearches { get; set; }
        public bool HasPriorityListing { get; set; }
        public bool HasVerifiedBadge { get; set; }
        public bool HasWishlistAlerts { get; set; }
    }

    public class UserSubscriptionDto
    {
        public int Id { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool AutoRenew { get; set; }
        public decimal AmountPaid { get; set; }
        public string Currency { get; set; } = "EUR";
        public SubscriptionPlanDto? Plan { get; set; }
    }

    public class UserLimitsDto
    {
        public string Tier { get; set; } = "Free";
        public int CardsUsed { get; set; }
        public int CardsLimit { get; set; }
        public int WishlistUsed { get; set; }
        public int WishlistLimit { get; set; }
        public int ActiveTradesUsed { get; set; }
        public int ActiveTradesLimit { get; set; }
        public int DailyMessagesUsed { get; set; }
        public int DailyMessagesLimit { get; set; }
        public bool CanSearchAdvanced { get; set; }
        public bool CanSearchGeographic { get; set; }
        public bool CanExportCollection { get; set; }
        public bool CanViewStatistics { get; set; }
        public DateTime? SubscriptionExpiresAt { get; set; }
    }
}
