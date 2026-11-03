using System.ComponentModel.DataAnnotations;

namespace CardExchange.API.DTOs.Requests
{
    public class UpdateWishlistItemRequest
    {
        /// <summary>1-8; 0 = qualsiasi condizione; null = non modificare.</summary>
        [Range(0, 8, ErrorMessage = "Condizione non valida")]
        public int? PreferredCondition { get; set; }

        /// <summary>Codice lingua; stringa vuota = qualsiasi lingua; null = non modificare.</summary>
        [MaxLength(5)]
        public string? PreferredLanguage { get; set; }

        /// <summary>1-4; 0 = qualsiasi finitura; null = non modificare.</summary>
        public int? PreferredFinish { get; set; }

        [Range(0, 999999.99, ErrorMessage = "Il prezzo deve essere tra 0 e 999999.99")]
        public decimal? MaxPrice { get; set; }

        [MaxLength(500, ErrorMessage = "Le note non possono superare 500 caratteri")]
        public string? Notes { get; set; }

        [Range(1, 3, ErrorMessage = "La priorità deve essere 1 (Alta), 2 (Media) o 3 (Bassa)")]
        public int? Priority { get; set; }
    }
}