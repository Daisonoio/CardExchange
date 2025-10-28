namespace CardExchange.API.DTOs.Responses
{
    public class AuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime AccessTokenExpiration { get; set; }
        public DateTime RefreshTokenExpiration { get; set; }
        public UserDto User { get; set; } = null!;
    }

    public class LoginResponse : AuthResponse
    {
        public string Message { get; set; } = "Login effettuato con successo";
    }

    public class RegisterResponse : AuthResponse
    {
        public string Message { get; set; } = "Registrazione completata con successo";
    }
}