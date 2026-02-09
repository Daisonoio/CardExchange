namespace CardExchange.Core.Entities
{
    public enum TradeOfferItemSide
    {
        Offered = 1,
        Requested = 2
    }

    public class TradeOfferItem : BaseEntity
    {
        public int TradeOfferId { get; set; }
        public int CardId { get; set; }

        public TradeOfferItemSide Side { get; set; }

        public int Quantity { get; set; } = 1;

        // Relazioni
        public virtual TradeOffer TradeOffer { get; set; } = null!;
        public virtual Card Card { get; set; } = null!;
    }
}
