using System.ComponentModel.DataAnnotations;

namespace CardExchange.API.DTOs.Requests
{
    public class RegisterRequest
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
        [MinLength(8, ErrorMessage = "La password deve essere di almeno 8 caratteri")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
            ErrorMessage = "La password deve contenere almeno una maiuscola, una minuscola, un numero e un carattere speciale")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "La conferma password è obbligatoria")]
        [Compare("Password", ErrorMessage = "Le password non coincidono")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        [Required(ErrorMessage = "Username o email obbligatorio")]
        public string UsernameOrEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "La password è obbligatoria")]
        public string Password { get; set; } = string.Empty;
    }

    public class RefreshTokenRequest
    {
        [Required(ErrorMessage = "L'access token è obbligatorio")]
        public string AccessToken { get; set; } = string.Empty;

        [Required(ErrorMessage = "Il refresh token è obbligatorio")]
        public string RefreshToken { get; set; } = string.Empty;
    }
}