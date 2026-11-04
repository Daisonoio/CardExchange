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

        /// <summary>Codice lingua (en, it, ja, ...). Null = non specificata.</summary>
        [MaxLength(5)]
        public string? Language { get; set; }

        /// <summary>1=Normale, 2=Foil, 3=Reverse Holo, 4=Etched.</summary>
        public int Finish { get; set; } = 1;

        [MaxLength(500)]
        public string? Notes { get; set; }

        public bool IsAvailableForTrade { get; set; } = true;

        [Range(0, 999999.99, ErrorMessage = "Il valore deve essere tra 0 e 999999.99")]
        public decimal? EstimatedValue { get; set; }

        /// <summary>Prezzo a cui il proprietario cederebbe la carta (indicativo). Null = usa il valore di mercato.</summary>
        [Range(0, 999999.99, ErrorMessage = "Il prezzo deve essere tra 0 e 999999.99")]
        public decimal? AskingPrice { get; set; }
    }

    public class UpdateCardRequest
    {
        [Range(1, 8, ErrorMessage = "Condizione non valida")]
        public int? Condition { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "La quantità deve essere almeno 1")]
        public int? Quantity { get; set; }

        /// <summary>Codice lingua. Stringa vuota = rimuovi la lingua; null = non modificare.</summary>
        [MaxLength(5)]
        public string? Language { get; set; }

        public int? Finish { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public bool? IsAvailableForTrade { get; set; }

        [Range(0, 999999.99, ErrorMessage = "Il valore deve essere tra 0 e 999999.99")]
        public decimal? EstimatedValue { get; set; }

        /// <summary>Nuovo prezzo indicato (indicativo); null = non modificare.</summary>
        [Range(0, 999999.99, ErrorMessage = "Il prezzo deve essere tra 0 e 999999.99")]
        public decimal? AskingPrice { get; set; }

        /// <summary>True = rimuove il prezzo indicato e torna al valore di mercato.</summary>
        public bool ClearAskingPrice { get; set; }
    }

    public class BulkCardActionRequest
    {
        public List<int> CardIds { get; set; } = new();
        /// <summary>Se valorizzato imposta la disponibilità allo scambio.</summary>
        public bool? IsAvailableForTrade { get; set; }
        /// <summary>Se true elimina le carte (ha la precedenza sulla disponibilità).</summary>
        public bool? Delete { get; set; }
    }
}
