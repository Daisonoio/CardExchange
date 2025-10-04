using System.ComponentModel.DataAnnotations;

namespace CardExchange.API.DTOs.Requests
{
    public class CreateGameRequest
    {
        [Required(ErrorMessage = "Il nome è obbligatorio")]
        [MaxLength(100, ErrorMessage = "Il nome non può superare 100 caratteri")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "La descrizione non può superare 500 caratteri")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Il publisher è obbligatorio")]
        [MaxLength(50, ErrorMessage = "Il publisher non può superare 50 caratteri")]
        public string Publisher { get; set; } = string.Empty;
    }
}