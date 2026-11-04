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

    public enum SubscriptionPaymentMethod
    {
        /// <summary>Attivazione simulata (solo sviluppo).</summary>
        Simulated = 0,
        /// <summary>Carta tramite Stripe Checkout, rinnovo automatico.</summary>
        Stripe = 1,
        /// <summary>Bonifico confermato da un amministratore.</summary>
        BankTransfer = 2,
        /// <summary>Attivazione manuale (fattura, omaggio, premio).</summary>
        Manual = 3
    }

    public class UserSubscription : BaseEntity
    {
        public int UserId { get; set; }
        public int PlanId { get; set; }

        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public DateTime? CancelledAt { get; set; }
        public bool AutoRenew { get; set; }

        [MaxLength(100)]
        public string? PaymentReference { get; set; }

        public decimal AmountPaid { get; set; }

        [MaxLength(3)]
        public string Currency { get; set; } = "EUR";

        // Relazioni
        public SubscriptionPaymentMethod PaymentMethod { get; set; } = SubscriptionPaymentMethod.Simulated;

        /// <summary>Id cliente presso il provider di pagamento (Stripe).</summary>
        [MaxLength(100)]
        public string? ExternalCustomerId { get; set; }

        /// <summary>Id abbonamento presso il provider di pagamento (Stripe).</summary>
        [MaxLength(100)]
        public string? ExternalSubscriptionId { get; set; }

        /// <summary>Nota interna dell'amministratore (es. numero fattura).</summary>
        [MaxLength(500)]
        public string? AdminNote { get; set; }

        /// <summary>Amministratore che ha confermato o attivato l'abbonamento.</summary>
        public int? ActivatedById { get; set; }

        /// <summary>Promemoria di scadenza già inviato.</summary>
        public DateTime? ExpiryReminderSentAt { get; set; }

        public virtual User User { get; set; } = null!;
        public virtual SubscriptionPlan Plan { get; set; } = null!;
    }
}
