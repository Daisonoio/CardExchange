using System.ComponentModel.DataAnnotations;

namespace CardExchange.API.DTOs.Requests
{
    public class UpdateWishlistItemRequest
    {
        [Range(1, 8, ErrorMessage = "Condizione non valida")]
        public int? PreferredCondition { get; set; }

        [Range(0, 999999.99, ErrorMessage = "Il prezzo deve essere tra 0 e 999999.99")]
        public decimal? MaxPrice { get; set; }

        [MaxLength(500, ErrorMessage = "Le note non possono superare 500 caratteri")]
        public string? Notes { get; set; }

        [Range(1, 3, ErrorMessage = "La priorità deve essere 1 (Alta), 2 (Media) o 3 (Bassa)")]
        public int? Priority { get; set; }
    }
}