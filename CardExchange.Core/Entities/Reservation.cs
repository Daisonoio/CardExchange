namespace CardExchange.Core.Entities
{
    public enum ReservationStatus
    {
        Pending = 1,
        Confirmed = 2,
        PickedUp = 3,
        Cancelled = 4,
        NoShow = 5,
        Expired = 6
    }

    /// <summary>
    /// "Prenota e ritira": l'utente blocca una carta di un negozio e la ritira (e paga) in cassa.
    /// Nessun pagamento online: il prezzo è quello indicato al momento della prenotazione, come riferimento.
    /// </summary>
    public class Reservation : BaseEntity
    {
        public int StoreId { get; set; }
        public int CardId { get; set; }
        public int UserId { get; set; }
        public int Quantity { get; set; } = 1;
        /// <summary>Prezzo indicato dal negozio al momento della prenotazione (riferimento, si paga in negozio).</summary>
        public decimal? PriceEur { get; set; }
        public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
        public DateTime ExpiresAt { get; set; }
        /// <summary>Codice da mostrare in cassa (6 caratteri).</summary>
        public string PickupCode { get; set; } = string.Empty;
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime? ReminderSentAt { get; set; }

        public virtual Store Store { get; set; } = null!;
        public virtual Card Card { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}
