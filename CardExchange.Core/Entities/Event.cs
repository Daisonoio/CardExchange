using System.ComponentModel.DataAnnotations;

namespace CardExchange.Core.Entities
{
    public enum EventType
    {
        Trademeetup = 1,
        Tournament = 2,
        CardShow = 3,
        DraftEvent = 4,
        SealedEvent = 5,
        CasualPlay = 6,
        Other = 99
    }

    public enum EventStatus
    {
        Draft = 0,
        Published = 1,
        Cancelled = 2,
        Completed = 3
    }

    public class Event : BaseEntity
    {
        [Required]
        public int OrganizerId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        public EventType Type { get; set; } = EventType.Trademeetup;
        public EventStatus Status { get; set; } = EventStatus.Draft;

        // Quando
        [Required]
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // Dove
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

        // Dettagli
        public int? MaxParticipants { get; set; }
        public bool IsPublic { get; set; } = true;

        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        // Giochi supportati (JSON array di GameId, es. "[1,3,5]")
        [MaxLength(500)]
        public string? SupportedGameIds { get; set; }

        // Contatori denormalizzati
        public int ParticipantCount { get; set; } = 0;

        // Relazioni
        public virtual User Organizer { get; set; } = null!;
        public virtual ICollection<EventParticipant> Participants { get; set; } = new List<EventParticipant>();
    }
}
