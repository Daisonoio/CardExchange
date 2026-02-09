namespace CardExchange.Core.Entities
{
    public class Conversation : BaseEntity
    {
        public int User1Id { get; set; }
        public int User2Id { get; set; }

        public int? TradeOfferId { get; set; }

        public DateTime? LastMessageAt { get; set; }

        // Relazioni
        public virtual User User1 { get; set; } = null!;
        public virtual User User2 { get; set; } = null!;
        public virtual TradeOffer? TradeOffer { get; set; }
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
