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
        /// <summary>Negozio che ospita l'evento, se presente.</summary>
        public int? StoreId { get; set; }
        public string? StoreName { get; set; }
        public OrganizerDto Organizer { get; set; } = null!;
        public bool IsParticipating { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Quota indicativa, pagata di persona.</summary>
        public decimal? EntryFeeEur { get; set; }
        public DateTime? RegistrationDeadline { get; set; }
        public string? Format { get; set; }
        public int? GameId { get; set; }
        public bool RegistrationOpen { get; set; }
        /// <summary>Posti rimasti, null se senza limite.</summary>
        public int? SpotsLeft { get; set; }
        public int WaitlistCount { get; set; }
        /// <summary>Stato dell'utente corrente: Registered, Confirmed, Attended, Waitlisted, Offered (null se non iscritto).</summary>
        public string? MyStatus { get; set; }
        public int? MyWaitlistPosition { get; set; }
        public DateTime? MyOfferExpiresAt { get; set; }
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
        /// <summary>"Offerto da …": sponsor di marca che coprono l'evento (pagina evento o serata di scambio).</summary>
        public IEnumerable<object> Sponsors { get; set; } = new List<object>();
    }

    /// <summary>Iscritto visto dall'organizzatore (con presenza e lista d'attesa).</summary>
    public class EventAttendeeDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
        public DateTime? CheckedInAt { get; set; }
        public int? WaitlistPosition { get; set; }
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
