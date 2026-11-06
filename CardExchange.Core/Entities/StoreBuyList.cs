namespace CardExchange.Core.Entities
{
    /// <summary>
    /// Lista acquisti del negozio: una carta che il negozio vuole comprare, a quale prezzo (contanti o credito)
    /// e quante copie. L'acquisto si conclude e si paga in negozio.
    /// </summary>
    public class StoreBuyOrder : BaseEntity
    {
        public int StoreId { get; set; }
        public int CardInfoId { get; set; }
        /// <summary>Condizione minima accettata (1 = Mint … 8 = Damaged).</summary>
        public CardCondition MinCondition { get; set; } = CardCondition.LightlyPlayed;
        public CardFinish? Finish { get; set; }
        public string? Language { get; set; }
        public decimal? PriceCashEur { get; set; }
        /// <summary>Valore in credito da spendere in negozio.</summary>
        public decimal? PriceCreditEur { get; set; }
        public int QuantityWanted { get; set; } = 1;
        public int QuantityFilled { get; set; }
        /// <summary>Copie di proposte accettate e non ancora consegnate.</summary>
        public int QuantityPending { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;

        public virtual Store Store { get; set; } = null!;
        public virtual CardInfo CardInfo { get; set; } = null!;
    }

    public enum BuyOfferStatus
    {
        Proposed = 1,
        Accepted = 2,
        Rejected = 3,
        Completed = 4,
        Expired = 5,
        Withdrawn = 6
    }

    /// <summary>Un utente propone al negozio una copia della sua collezione per una richiesta della lista acquisti.</summary>
    public class StoreBuyOffer : BaseEntity
    {
        public int BuyOrderId { get; set; }
        public int UserId { get; set; }
        public int CardId { get; set; }
        public int Quantity { get; set; } = 1;
        public BuyOfferStatus Status { get; set; } = BuyOfferStatus.Proposed;
        /// <summary>Entro quando portare la carta in negozio (dopo l'accettazione).</summary>
        public DateTime? DropOffBy { get; set; }
        public string? StoreNote { get; set; }
        public DateTime? RespondedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime? ReminderSentAt { get; set; }

        public virtual StoreBuyOrder BuyOrder { get; set; } = null!;
        public virtual User User { get; set; } = null!;
        public virtual Card Card { get; set; } = null!;
    }

    /// <summary>Ultimo avviso "il negozio compra le tue carte" per coppia negozio–utente (al massimo uno a settimana).</summary>
    public class StoreBuyAlert
    {
        public long Id { get; set; }
        public int StoreId { get; set; }
        public int UserId { get; set; }
        public DateTime SentAt { get; set; }
    }
}
