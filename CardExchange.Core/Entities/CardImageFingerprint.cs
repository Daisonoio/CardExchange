namespace CardExchange.Core.Entities
{
    /// <summary>Impronta visiva dell'immagine di una carta del catalogo, per lo scanner (vedi Rules/CardFingerprint).</summary>
    public class CardImageFingerprint : BaseEntity
    {
        public int CardInfoId { get; set; }
        /// <summary>932 valori con segno salvati come byte; vuoto se l'immagine non si è potuta leggere.</summary>
        public byte[] Vector { get; set; } = Array.Empty<byte>();
        public int Version { get; set; }
        public DateTime ComputedAt { get; set; }
        public bool Failed { get; set; }

        public virtual CardInfo CardInfo { get; set; } = null!;
    }
}
