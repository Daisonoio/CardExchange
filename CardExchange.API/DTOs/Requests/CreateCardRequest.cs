using System.ComponentModel.DataAnnotations;

namespace CardExchange.API.DTOs.Requests
{
    public class CreateCardRequest
    {
        [Required(ErrorMessage = "L'ID della carta è obbligatorio")]
        public int CardInfoId { get; set; }

        [Required(ErrorMessage = "La condizione è obbligatoria")]
        [Range(1, 8, ErrorMessage = "Condizione non valida")]
        public int Condition { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "La quantità deve essere almeno 1")]
        public int Quantity { get; set; } = 1;

        [MaxLength(500)]
        public string? Notes { get; set; }

        public bool IsAvailableForTrade { get; set; } = true;

        [Range(0, 999999.99, ErrorMessage = "Il valore deve essere tra 0 e 999999.99")]
        public decimal? EstimatedValue { get; set; }
    }

    public class UpdateCardRequest
    {
        [Range(1, 8, ErrorMessage = "Condizione non valida")]
        public int? Condition { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "La quantità deve essere almeno 1")]
        public int? Quantity { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public bool? IsAvailableForTrade { get; set; }

        [Range(0, 999999.99, ErrorMessage = "Il valore deve essere tra 0 e 999999.99")]
        public decimal? EstimatedValue { get; set; }
    }
}