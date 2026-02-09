using System.ComponentModel.DataAnnotations;

namespace CardExchange.Core.Entities
{
    public class TradeReview : BaseEntity
    {
        public int TradeOfferId { get; set; }
        public int ReviewerId { get; set; }
        public int ReviewedUserId { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }

        public bool CardAsDescribed { get; set; } = true;
        public bool TimelyShipping { get; set; } = true;
        public bool GoodCommunication { get; set; } = true;

        // Relazioni
        public virtual TradeOffer TradeOffer { get; set; } = null!;
        public virtual User Reviewer { get; set; } = null!;
        public virtual User ReviewedUser { get; set; } = null!;
    }
}
