namespace CardExchange.Core.Entities
{
    /// <summary>
    /// Domanda locale di un negozio: cosa cercano i collezionisti attivi nella zona, tra le carte che ha e
    /// tra quelle che non ha. Solo conteggi e medie, mai nomi; ogni carta compare solo se la cercano almeno 3 persone.
    /// </summary>
    public class StoreDemandSnapshot : BaseEntity
    {
        public int StoreId { get; set; }
        public DateTime ComputedAt { get; set; }
        public int RadiusKm { get; set; }
        public int ActiveUsersInRadius { get; set; }
        /// <summary>Persone che cercano almeno una carta disponibile nel negozio (0 se sotto soglia).</summary>
        public int InterestedUsers { get; set; }
        public string InStockJson { get; set; } = "[]";
        public string MissingJson { get; set; } = "[]";

        public virtual Store Store { get; set; } = null!;
    }

    /// <summary>Avviso "il negozio ha una carta che cerchi" inviato a un utente (limiti e niente doppioni).</summary>
    public class StoreStockAlert
    {
        public long Id { get; set; }
        public int StoreId { get; set; }
        public int CardInfoId { get; set; }
        public int UserId { get; set; }
        public DateTime SentAt { get; set; }
    }
}
