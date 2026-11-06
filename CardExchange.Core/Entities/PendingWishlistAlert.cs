namespace CardExchange.Core.Entities
{
    /// <summary>
    /// Avviso "carta della wishlist disponibile" per chi non ha gli avvisi immediati (piano gratuito):
    /// si raccolgono e partono in un unico riepilogo alle 18.
    /// </summary>
    public class PendingWishlistAlert
    {
        public long Id { get; set; }
        public int UserId { get; set; }
        public int CardId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
    }
}
