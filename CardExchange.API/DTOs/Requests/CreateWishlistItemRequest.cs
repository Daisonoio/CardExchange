using System.ComponentModel.DataAnnotations;

namespace CardExchange.API.DTOs.Requests
{
    public class CreateWishlistItemRequest
    {
        [Required(ErrorMessage = "L'ID della carta è obbligatorio")]
        public int CardInfoId { get; set; }

        [Range(1, 8, ErrorMessage = "Condizione non valida")]
        public int? PreferredCondition { get; set; }

        [Range(0, 999999.99, ErrorMessage = "Il prezzo deve essere tra 0 e 999999.99")]
        public decimal? MaxPrice { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [Range(1, 3, ErrorMessage = "La priorità deve essere 1 (alta), 2 (media) o 3 (bassa)")]
        public int Priority { get; set; } = 1;
    }

    public class UpdateWishlistItemRequest
    {
        [Range(1, 8, ErrorMessage = "Condizione non valida")]
        public int? PreferredCondition { get; set; }

        [Range(0, 999999.99, ErrorMessage = "Il prezzo deve essere tra 0 e 999999.99")]
        public decimal? MaxPrice { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [Range(1, 3, ErrorMessage = "La priorità deve essere 1 (alta), 2 (media) o 3 (bassa)")]
        public int? Priority { get; set; }
    }
}