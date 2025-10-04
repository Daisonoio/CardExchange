using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardExchange.Core.Entities
{
    public class UserLocation : BaseEntity
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Province { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Country { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? PostalCode { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        [Range(1, 1000)]
        public int MaxDistanceKm { get; set; } = 50;

        // Relazioni
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}