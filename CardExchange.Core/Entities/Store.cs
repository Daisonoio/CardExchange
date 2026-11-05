using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardExchange.Core.Entities
{
    public enum StoreStatus
    {
        /// <summary>Inserito da un utente, in attesa di verifica dei moderatori.</summary>
        Pending = 1,
        Verified = 2,
        Rejected = 3,
        Suspended = 4
    }

    /// <summary>
    /// Negozio di carte (LGS). Visibile a tutti solo dopo la verifica. Se IsSafeMeetupSpot è attivo,
    /// viene proposto come luogo sicuro per gli scambi di persona.
    /// </summary>
    public class Store : BaseEntity
    {
        /// <summary>Utente che gestisce la scheda del negozio.</summary>
        [Required]
        public int OwnerId { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required, MaxLength(250)]
        public string Address { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Province { get; set; }

        [MaxLength(10)]
        public string? PostalCode { get; set; }

        [Required, MaxLength(50)]
        public string Country { get; set; } = "IT";

        [Column(TypeName = "decimal(9,6)")]
        public decimal Latitude { get; set; }

        [Column(TypeName = "decimal(9,6)")]
        public decimal Longitude { get; set; }

        [MaxLength(30)]
        public string? Phone { get; set; }

        [MaxLength(200)]
        public string? Website { get; set; }

        [MaxLength(100)]
        public string? Instagram { get; set; }

        /// <summary>Orari in testo libero, es. "Lun-Sab 10-19, giovedì serata Commander fino alle 23".</summary>
        [MaxLength(500)]
        public string? OpeningHours { get; set; }

        /// <summary>Id dei giochi trattati, separati da virgola (stesso formato degli eventi).</summary>
        [MaxLength(100)]
        public string? SupportedGameIds { get; set; }

        /// <summary>Il negozio accetta che gli utenti si incontrino lì per gli scambi.</summary>
        public bool IsSafeMeetupSpot { get; set; } = true;

        public StoreStatus Status { get; set; } = StoreStatus.Pending;
        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedById { get; set; }

        [MaxLength(500)]
        public string? ReviewNote { get; set; }

        [ForeignKey(nameof(OwnerId))]
        /// <summary>Identificativo leggibile per link e QR (es. "games-corner-milano"), univoco.</summary>
        [MaxLength(80)]
        public string? Slug { get; set; }

        public virtual User Owner { get; set; } = null!;

        public virtual ICollection<Event> Events { get; set; } = new List<Event>();
    }
}
