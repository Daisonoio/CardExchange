using System.ComponentModel.DataAnnotations;

namespace CardExchange.Core.Entities
{
    public enum ParticipationStatus
    {
        Registered = 1,
        Confirmed = 2,
        Cancelled = 3,
        Attended = 4
    }

    public class EventParticipant : BaseEntity
    {
        [Required]
        public int EventId { get; set; }

        [Required]
        public int UserId { get; set; }

        public ParticipationStatus Status { get; set; } = ParticipationStatus.Registered;

        [MaxLength(500)]
        public string? Notes { get; set; }

        /// <summary>Presenza confermata all'evento (serata di scambio). Null = non presente o uscito.</summary>
        public DateTime? CheckedInAt { get; set; }

        // Relazioni
        public virtual Event Event { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}
