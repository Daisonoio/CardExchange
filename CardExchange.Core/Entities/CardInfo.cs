using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardExchange.Core.Entities
{
    public class CardInfo : BaseEntity
    {
        [Required]
        public int CardSetId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? CardNumber { get; set; }

        [MaxLength(50)]
        public string? Rarity { get; set; }

        [MaxLength(100)]
        public string? Type { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public string? ImageUrl { get; set; }

        // Relazioni
        [ForeignKey("CardSetId")]
        public virtual CardSet CardSet { get; set; } = null!;
        public virtual ICollection<Card> Cards { get; set; } = new List<Card>();
        public virtual ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
    }
}