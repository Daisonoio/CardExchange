using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardExchange.Core.Entities
{
    public enum MeetupStatus
    {
        Proposed = 1,
        Accepted = 2,
        Declined = 3,
        Cancelled = 4,
        /// <summary>Sostituito da una proposta più recente.</summary>
        Superseded = 5
    }

    /// <summary>
    /// Appuntamento per uno scambio di persona. Per ogni scambio è "attivo" al massimo un appuntamento
    /// (Proposed o Accepted): una nuova proposta sostituisce la precedente.
    /// </summary>
    public class TradeMeetup : BaseEntity
    {
        [Required]
        public int TradeOfferId { get; set; }

        [Required]
        public int ProposedById { get; set; }

        /// <summary>Data e ora in UTC.</summary>
        [Required]
        public DateTime ScheduledAt { get; set; }

        [Required]
        [MaxLength(200)]
        public string LocationName { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Address { get; set; }

        [Column(TypeName = "decimal(9,6)")]
        public decimal? Latitude { get; set; }

        [Column(TypeName = "decimal(9,6)")]
        public decimal? Longitude { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public MeetupStatus Status { get; set; } = MeetupStatus.Proposed;
        public DateTime? RespondedAt { get; set; }

        /// <summary>Promemoria del giorno prima già inviato.</summary>
        public DateTime? ReminderSentAt { get; set; }

        /// <summary>Invito a confermare lo scambio dopo l'incontro già inviato.</summary>
        public DateTime? FollowUpSentAt { get; set; }

        /// <summary>Negozio scelto come punto di scambio (luogo e coordinate vengono dalla sua scheda).</summary>
        public int? StoreId { get; set; }

        /// <summary>Quando il negozio è stato avvisato dell'incontro.</summary>
        public DateTime? StoreNotifiedAt { get; set; }

        public virtual Store? Store { get; set; }

        [ForeignKey(nameof(TradeOfferId))]
        public virtual TradeOffer TradeOffer { get; set; } = null!;

        [ForeignKey(nameof(ProposedById))]
        public virtual User ProposedBy { get; set; } = null!;
    }
}
