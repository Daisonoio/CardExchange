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

        // === Campi Scryfall ===

        [MaxLength(36)]
        public string? ScryfallId { get; set; }

        [MaxLength(36)]
        public string? OracleId { get; set; }

        [MaxLength(100)]
        public string? ManaCost { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? Cmc { get; set; }

        [MaxLength(200)]
        public string? TypeLine { get; set; }

        public string? OracleText { get; set; }

        [MaxLength(50)]
        public string? Colors { get; set; }

        [MaxLength(50)]
        public string? ColorIdentity { get; set; }

        [MaxLength(10)]
        public string? Power { get; set; }

        [MaxLength(10)]
        public string? Toughness { get; set; }

        [MaxLength(10)]
        public string? Loyalty { get; set; }

        [MaxLength(500)]
        public string? Keywords { get; set; }

        [MaxLength(200)]
        public string? Artist { get; set; }

        // Immagini (Scryfall CDN *.scryfall.io - no rate limit)
        public string? ImageSmall { get; set; }
        public string? ImageNormal { get; set; }
        public string? ImageLarge { get; set; }
        public string? ImagePng { get; set; }
        public string? ImageArtCrop { get; set; }
        public string? ImageBorderCrop { get; set; }

        // Prezzi di mercato
        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceUsd { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceUsdFoil { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceEur { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceEurFoil { get; set; }

        public string? Legalities { get; set; }
        public string? ScryfallUri { get; set; }
        public DateTime? ScryfallUpdatedAt { get; set; }

        // Relazioni
        [ForeignKey("CardSetId")]
        public virtual CardSet CardSet { get; set; } = null!;
        public virtual ICollection<Card> Cards { get; set; } = new List<Card>();
        public virtual ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
    }
}