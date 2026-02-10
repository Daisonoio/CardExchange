namespace CardExchange.API.DTOs.Responses
{
    public class EventDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string? Province { get; set; }
        public string Country { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public int? MaxParticipants { get; set; }
        public bool IsPublic { get; set; }
        public string? ImageUrl { get; set; }
        public string? SupportedGameIds { get; set; }
        public int ParticipantCount { get; set; }
        public OrganizerDto Organizer { get; set; } = null!;
        public bool IsParticipating { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class OrganizerDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public decimal ReputationScore { get; set; }
    }

    public class EventDetailDto : EventDto
    {
        public IEnumerable<EventParticipantDto> Participants { get; set; } = new List<EventParticipantDto>();
    }

    public class EventParticipantDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
    }
}
