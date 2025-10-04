using System.ComponentModel.DataAnnotations;

namespace CardExchange.API.DTOs.Requests
{
    public class CreateCardInfoRequest
    {
        [Required(ErrorMessage = "L'ID del set è obbligatorio")]
        public int CardSetId { get; set; }

        [Required(ErrorMessage = "Il nome della carta è obbligatorio")]
        [MaxLength(200, ErrorMessage = "Il nome non può superare 200 caratteri")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50, ErrorMessage = "Il numero carta non può superare 50 caratteri")]
        public string? CardNumber { get; set; }

        [MaxLength(50, ErrorMessage = "La rarità non può superare 50 caratteri")]
        public string? Rarity { get; set; }

        [MaxLength(100, ErrorMessage = "Il tipo non può superare 100 caratteri")]
        public string? Type { get; set; }

        [MaxLength(1000, ErrorMessage = "La descrizione non può superare 1000 caratteri")]
        public string? Description { get; set; }

        [Url(ErrorMessage = "L'URL dell'immagine non è valido")]
        public string? ImageUrl { get; set; }
    }
}