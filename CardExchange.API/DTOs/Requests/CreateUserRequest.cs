using System.ComponentModel.DataAnnotations;

namespace CardExchange.API.DTOs.Requests
{
    public class CreateUserRequest
    {
        [Required(ErrorMessage = "L'email è obbligatoria")]
        [EmailAddress(ErrorMessage = "Formato email non valido")]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Lo username è obbligatorio")]
        [MinLength(3, ErrorMessage = "Lo username deve essere di almeno 3 caratteri")]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Il nome è obbligatorio")]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Il cognome è obbligatorio")]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Bio { get; set; }

        [Required(ErrorMessage = "La password è obbligatoria")]
        [MinLength(6, ErrorMessage = "La password deve essere di almeno 6 caratteri")]
        public string Password { get; set; } = string.Empty;
    }

    public class UpdateUserRequest
    {
        [MaxLength(100)]
        public string? FirstName { get; set; }

        [MaxLength(100)]
        public string? LastName { get; set; }

        [MaxLength(500)]
        public string? Bio { get; set; }
    }

    public class CreateUserLocationRequest
    {
        [Required(ErrorMessage = "La città è obbligatoria")]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "La provincia è obbligatoria")]
        [MaxLength(100)]
        public string Province { get; set; } = string.Empty;

        [Required(ErrorMessage = "Il paese è obbligatorio")]
        [MaxLength(100)]
        public string Country { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? PostalCode { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        [Range(1, 1000, ErrorMessage = "La distanza massima deve essere tra 1 e 1000 km")]
        public int MaxDistanceKm { get; set; } = 50;
    }
}