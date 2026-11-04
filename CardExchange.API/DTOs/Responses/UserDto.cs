namespace CardExchange.API.DTOs.Responses
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? PaypalUsername { get; set; }
        public string? SatispayUsername { get; set; }
        public string? PaymentQrCodeUrl { get; set; }
        public bool IsActive { get; set; }
        public decimal ReputationScore { get; set; }
        public int TotalTradesCompleted { get; set; }
        public int TotalReviewsReceived { get; set; }
        /// <summary>Valorizzato solo per l'utente corrente (endpoint di autenticazione).</summary>
        public bool? EmailConfirmed { get; set; }
        /// <summary>Solo per l'utente corrente: manca la data di nascita (account creati prima del controllo dell'età).</summary>
        public bool? BirthDateRequired { get; set; }
        /// <summary>Solo per l'utente corrente: ha meno di 18 anni (per i consigli di sicurezza negli incontri).</summary>
        public bool? IsMinor { get; set; }
        public DateTime CreatedAt { get; set; }
        public UserLocationDto? Location { get; set; }
        /// <summary>"Premium" o "Store" se l'utente ha un piano con badge sul profilo.</summary>
        public string? PlanBadge { get; set; }
    }

    public class UserLocationDto
    {
        public string City { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string? PostalCode { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public int MaxDistanceKm { get; set; }
    }
}