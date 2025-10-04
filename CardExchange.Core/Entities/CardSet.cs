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

        // Relazioni
        [ForeignKey("GameId")]
        public virtual Game Game { get; set; } = null!;
        public virtual ICollection<CardInfo> CardInfos { get; set; } = new List<CardInfo>();
    }
}