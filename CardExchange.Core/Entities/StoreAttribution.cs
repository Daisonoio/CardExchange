namespace CardExchange.Core.Entities
{
    public enum AttributionSource
    {
        /// <summary>Registrazione dopo aver scansionato il QR o aperto il link del negozio.</summary>
        Qr = 1,
        Manual = 2
    }

    /// <summary>
    /// Il negozio grazie al quale un utente si è registrato. Al massimo uno per utente (vale il primo):
    /// serve a riepilogo mensile, commissioni e fedeltà. Il negozio vede solo conteggi, mai chi.
    /// </summary>
    public class StoreAttribution : BaseEntity
    {
        public int UserId { get; set; }
        public int StoreId { get; set; }
        public AttributionSource Source { get; set; } = AttributionSource.Qr;

        public virtual User User { get; set; } = null!;
        public virtual Store Store { get; set; } = null!;
    }
}
