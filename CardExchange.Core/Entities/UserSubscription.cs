using System.ComponentModel.DataAnnotations;

namespace CardExchange.Core.Entities
{
    public enum SubscriptionStatus
    {
        Active = 1,
        Expired = 2,
        Cancelled = 3,
        PendingPayment = 4
    }

    public class UserSubscription : BaseEntity
    {
        public int UserId { get; set; }
        public int PlanId { get; set; }

        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public DateTime? CancelledAt { get; set; }
        public bool AutoRenew { get; set; } = true;

        [MaxLength(100)]
        public string? PaymentReference { get; set; }

        public decimal AmountPaid { get; set; }

        [MaxLength(3)]
        public string Currency { get; set; } = "EUR";

        // Relazioni
        public virtual User User { get; set; } = null!;
        public virtual SubscriptionPlan Plan { get; set; } = null!;
    }
}
