using System.ComponentModel.DataAnnotations;

namespace CardExchange.API.DTOs.Requests
{
    public class CreateEventRequest
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        public int Type { get; set; } = 1; // EventType enum

        [Required]
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        [Required]
        [MaxLength(300)]
        public string Address { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Province { get; set; }

        [Required]
        [MaxLength(100)]
        public string Country { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? PostalCode { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public int? MaxParticipants { get; set; }
        public bool IsPublic { get; set; } = true;

        /// <summary>Negozio verificato dell'organizzatore che ospita l'evento: indirizzo e coordinate vengono presi dal negozio.</summary>
        public int? StoreId { get; set; }

        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        // JSON array di GameId, es. "[1,3,5]"
        [MaxLength(500)]
        public string? SupportedGameIds { get; set; }
        /// <summary>Quota indicativa, pagata di persona all'evento.</summary>
        [Range(0, 1000)]
        public decimal? EntryFeeEur { get; set; }
        public DateTime? RegistrationDeadline { get; set; }
        [MaxLength(60)]
        public string? Format { get; set; }
        public int? GameId { get; set; }
    }

    public class UpdateEventRequest
    {
        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        public int? Type { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        [MaxLength(300)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(100)]
        public string? Province { get; set; }

        [MaxLength(100)]
        public string? Country { get; set; }

        [MaxLength(20)]
        public string? PostalCode { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public int? MaxParticipants { get; set; }
        public bool? IsPublic { get; set; }

        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        [MaxLength(500)]
        public string? SupportedGameIds { get; set; }
        /// <summary>Quota indicativa, pagata di persona all'evento.</summary>
        [Range(0, 1000)]
        public decimal? EntryFeeEur { get; set; }
        public DateTime? RegistrationDeadline { get; set; }
        [MaxLength(60)]
        public string? Format { get; set; }
        public int? GameId { get; set; }
    }
}
