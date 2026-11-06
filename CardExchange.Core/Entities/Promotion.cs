namespace CardExchange.Core.Entities
{
    public enum PromotionType
    {
        /// <summary>Evento fissato in cima agli eventi vicini, più una notifica a chi è nel raggio.</summary>
        FeaturedEvent = 1,
        /// <summary>"Sono qui" che raggiunge anche chi non segue l'autore, se cerca una sua carta.</summary>
        SponsoredBroadcast = 2
    }

    public enum PromotionStatus { Draft = 0, PendingPayment = 1, Active = 2, Completed = 3, Rejected = 4, Cancelled = 5 }

    public enum PromotionPayment { None = 0, Credits = 1, Purchase = 2 }

    /// <summary>
    /// Promozione locale a pagamento (crediti del piano o acquisto). Sempre etichettata "Sponsorizzato".
    /// </summary>
    public class Promotion : BaseEntity
    {
        public int OwnerUserId { get; set; }
        public int? StoreId { get; set; }
        public PromotionType Type { get; set; }
        /// <summary>Evento o avviso "Sono qui" promosso.</summary>
        public int TargetId { get; set; }
        public int RadiusKm { get; set; }
        public int? GameId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public PromotionStatus Status { get; set; } = PromotionStatus.PendingPayment;
        public PromotionPayment PaidWith { get; set; }
        public int PriceCredits { get; set; }
        public int? PurchaseId { get; set; }
        /// <summary>Notifiche consegnate (persone raggiunte).</summary>
        public int Reach { get; set; }
        public int Impressions { get; set; }
        public int Clicks { get; set; }
        public int Reports { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public string? RejectionReason { get; set; }

        public virtual User OwnerUser { get; set; } = null!;
    }

    /// <summary>Una notifica sponsorizzata consegnata: serve ai limiti (2 a settimana, mai due volte la stessa).</summary>
    public class PromotionDelivery
    {
        public long Id { get; set; }
        public int PromotionId { get; set; }
        public int UserId { get; set; }
        public DateTime SentAt { get; set; }
    }

    /// <summary>Segnalazione di una promozione da parte di un utente (una per utente).</summary>
    public class PromotionReport
    {
        public long Id { get; set; }
        public int PromotionId { get; set; }
        public int UserId { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
