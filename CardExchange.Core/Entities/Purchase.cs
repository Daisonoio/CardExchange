namespace CardExchange.Core.Entities
{
    public enum PurchaseProductType
    {
        /// <summary>Una promozione specifica (evento in evidenza, "Sono qui" sponsorizzato…).</summary>
        Promotion = 1,
        /// <summary>Pacchetto di crediti promozione.</summary>
        PromotionCredits = 2,
        /// <summary>Pacchetto per organizzatori di fiere e mercatini.</summary>
        OrganizerPackage = 3
    }

    public enum PurchaseStatus { Pending = 1, Paid = 2, Refunded = 3, Failed = 4, Expired = 5 }

    /// <summary>
    /// Pagamento una tantum a CardExchange (mai tra utenti): si attiva solo dal webhook di Stripe.
    /// </summary>
    public class Purchase : BaseEntity
    {
        public int UserId { get; set; }
        public PurchaseProductType ProductType { get; set; }
        /// <summary>Codice del prodotto nel listino (es. "credits-5").</summary>
        public string ProductCode { get; set; } = string.Empty;
        /// <summary>Quantità acquistata (es. numero di crediti).</summary>
        public int Quantity { get; set; } = 1;
        /// <summary>Promozione o evento a cui si riferisce, se c'è.</summary>
        public int? RefId { get; set; }
        public decimal AmountEur { get; set; }
        public PurchaseStatus Status { get; set; } = PurchaseStatus.Pending;
        public string? StripeSessionId { get; set; }
        public string? StripePaymentIntentId { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime? RefundedAt { get; set; }

        public virtual User User { get; set; } = null!;
    }

    /// <summary>Movimento dei crediti promozione: + acquisto o accredito mensile del piano, − utilizzo o rimborso.</summary>
    public class PromotionCreditEntry
    {
        public long Id { get; set; }
        public int UserId { get; set; }
        public int Delta { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int? PurchaseId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
