using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardExchange.Core.Entities
{
    public class WishlistItem : BaseEntity
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public int CardInfoId { get; set; }

        public CardCondition? PreferredCondition { get; set; }

        public decimal? MaxPrice { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public int Priority { get; set; } = 1; // 1=alta, 2=media, 3=bassa

        // Relazioni
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [ForeignKey("CardInfoId")]
        public virtual CardInfo CardInfo { get; set; } = null!;
    }
}