using System.ComponentModel.DataAnnotations;

namespace CardExchange.Core.Entities
{
    public enum SubscriptionTier
    {
        Free = 0,
        Premium = 1
    }

    public enum BillingCycle
    {
        Monthly = 1,
        Annual = 2
    }

    public class SubscriptionPlan : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;

        public BillingCycle Cycle { get; set; } = BillingCycle.Monthly;

        public decimal Price { get; set; }

        [MaxLength(3)]
        public string Currency { get; set; } = "EUR";

        public bool IsActive { get; set; } = true;

        // Limiti del piano
        public int MaxCards { get; set; } = 50;
        public int MaxWishlistItems { get; set; } = 20;
        public int MaxActiveTradeOffers { get; set; } = 5;
        public int MaxDailyMessages { get; set; } = 10;
        public bool CanSearchAdvanced { get; set; } = false;
        public bool CanSearchGeographic { get; set; } = false;
        public bool CanExportCollection { get; set; } = false;
        public bool CanViewStatistics { get; set; } = false;
        public bool CanSaveSearches { get; set; } = false;
        public bool HasPriorityListing { get; set; } = false;
        public bool HasVerifiedBadge { get; set; } = false;
        public bool HasWishlistAlerts { get; set; } = false;

        // Relazioni
        public virtual ICollection<UserSubscription> Subscriptions { get; set; } = new List<UserSubscription>();
    }
}
