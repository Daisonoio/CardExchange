using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardExchange.Core.Entities
{
    public enum TradeOfferStatus
    {
        Pending = 1,
        Accepted = 2,
        Rejected = 3,
        Cancelled = 4,
        Completed = 5
    }

    public class TradeOffer : BaseEntity
    {
        [Required]
        public int SenderId { get; set; }

        [Required]
        public int ReceiverId { get; set; }

        [Required]
        public TradeOfferStatus Status { get; set; } = TradeOfferStatus.Pending;

        [MaxLength(1000)]
        public string? Message { get; set; }

        public DateTime? ResponseDate { get; set; }
        public DateTime? CompletedDate { get; set; }

        // Relazioni
        [ForeignKey("SenderId")]
        public virtual User Sender { get; set; } = null!;

        [ForeignKey("ReceiverId")]
        public virtual User Receiver { get; set; } = null!;
    }
}