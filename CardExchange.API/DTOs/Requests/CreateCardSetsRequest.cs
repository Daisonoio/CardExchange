using System.ComponentModel.DataAnnotations;

namespace CardExchange.API.DTOs.Requests
{
    public class CreateCardSetRequest
    {
        [Required(ErrorMessage = "L'ID del gioco è obbligatorio")]
        public int GameId { get; set; }

        [Required(ErrorMessage = "Il nome è obbligatorio")]
        [MaxLength(100, ErrorMessage = "Il nome non può superare 100 caratteri")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Il codice è obbligatorio")]
        [MaxLength(10, ErrorMessage = "Il codice non può superare 10 caratteri")]
        public string Code { get; set; } = string.Empty;

        public DateTime? ReleaseDate { get; set; }

        [MaxLength(500, ErrorMessage = "La descrizione non può superare 500 caratteri")]
        public string? Description { get; set; }
    }
}