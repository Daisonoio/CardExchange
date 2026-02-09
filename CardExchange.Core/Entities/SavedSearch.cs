using System.ComponentModel.DataAnnotations;

namespace CardExchange.Core.Entities
{
    public class SavedSearch : BaseEntity
    {
        public int UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        // Criteri di ricerca serializzati in JSON
        [Required]
        [MaxLength(4000)]
        public string CriteriaJson { get; set; } = string.Empty;

        public bool AlertEnabled { get; set; } = false;
        public DateTime? LastAlertSentAt { get; set; }

        // Relazioni
        public virtual User User { get; set; } = null!;
    }
}
