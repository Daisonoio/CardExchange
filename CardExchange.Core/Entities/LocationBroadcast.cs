using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardExchange.Core.Entities
{
    /// <summary>
    /// Avviso "Sono qui": un collezionista o un negozio fa sapere a chi lo segue dove si trova
    /// (mercatino, fiera, stand) fino a un certo orario. La posizione è quella del luogo scelto
    /// dall'autore, non la sua posizione privata.
    /// </summary>
    public class LocationBroadcast : BaseEntity
    {
        public int AuthorId { get; set; }

        /// <summary>Se valorizzato l'avviso è pubblicato a nome del negozio (dell'autore).</summary>
        public int? StoreId { get; set; }

        /// <summary>Evento collegato (facoltativo).</summary>
        public int? EventId { get; set; }

        [Required]
        [MaxLength(100)]
        public string PlaceName { get; set; } = string.Empty;

        /// <summary>Dove trovarlo sul posto, es. "Padiglione 3, stand B12".</summary>
        [MaxLength(120)]
        public string? SpotDetail { get; set; }

        [MaxLength(280)]
        public string? Message { get; set; }

        [Column(TypeName = "decimal(9,6)")]
        public decimal Latitude { get; set; }

        [Column(TypeName = "decimal(9,6)")]
        public decimal Longitude { get; set; }

        public DateTime StartsAt { get; set; } = DateTime.UtcNow;

        public DateTime EndsAt { get; set; }

        /// <summary>Chiuso prima della scadenza dall'autore.</summary>
        public DateTime? EndedAt { get; set; }

        /// <summary>Quanti follower hanno ricevuto l'avviso.</summary>
        public int RecipientCount { get; set; }

        [ForeignKey(nameof(AuthorId))]
        public virtual User Author { get; set; } = null!;

        [ForeignKey(nameof(StoreId))]
        public virtual Store? Store { get; set; }

        [ForeignKey(nameof(EventId))]
        public virtual Event? Event { get; set; }

        [NotMapped]
        public bool IsLive => EndedAt == null && !IsDeleted && EndsAt > DateTime.UtcNow;
    }
}
