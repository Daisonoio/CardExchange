using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Dynamic;

namespace CardExchange.Core.Entities
{
    public class CardSet : BaseEntity
    {
        [Required]
        public int GameId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string Code { get; set; } = string.Empty;

        public DateTime? ReleaseDate { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        // === Campi Scryfall ===

        [MaxLength(36)]
        public string? ScryfallId { get; set; }

        [MaxLength(50)]
        public string? SetType { get; set; }

        public int? CardCount { get; set; }

        public string? IconSvgUri { get; set; }

        public bool IsDigital { get; set; } = false;

        public DateTime? ScryfallUpdatedAt { get; set; }

        // Relazioni
        [ForeignKey("GameId")]
        public virtual Game Game { get; set; } = null!;
        public virtual ICollection<CardInfo> CardInfos { get; set; } = new List<CardInfo>();
    }
}