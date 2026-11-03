using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardExchange.Core.Entities
{
    /// <summary>Sottoscrizione Web Push di un browser/dispositivo dell'utente.</summary>
    public class PushSubscription : BaseEntity
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(1000)]
        public string Endpoint { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string P256dh { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Auth { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? UserAgent { get; set; }

        public DateTime? LastSuccessAt { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; } = null!;
    }
}
