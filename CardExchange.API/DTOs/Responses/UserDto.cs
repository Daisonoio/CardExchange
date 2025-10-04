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
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public UserLocationDto? Location { get; set; }
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