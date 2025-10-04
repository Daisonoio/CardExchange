using System.ComponentModel.DataAnnotations;

namespace CardExchange.Core.Entities
{
    public class Game : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(50)]
        public string Publisher { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        // Relazioni
        public virtual ICollection<CardSet> CardSets { get; set; } = new List<CardSet>();
    }
}