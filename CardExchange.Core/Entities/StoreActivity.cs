namespace CardExchange.Core.Entities
{
    public enum StoreActivityType
    {
        ProfileView = 1,
        QrScan = 2,
        Follow = 3,
        MeetupScheduled = 4,
        MeetupCompleted = 5,
        EventRegistration = 6,
        EventCheckIn = 7
    }

    /// <summary>
    /// Azione che porta valore a un negozio (visita, scansione del QR, scambio in negozio...).
    /// Base del riepilogo mensile e delle statistiche. UserId serve solo a contare le persone diverse
    /// e viene cancellato dopo 13 mesi.
    /// </summary>
    public class StoreActivity
    {
        public long Id { get; set; }
        public int StoreId { get; set; }
        public StoreActivityType Type { get; set; }
        public int? UserId { get; set; }
        public int? RefId { get; set; }
        public DateTime OccurredAt { get; set; }
    }
}
