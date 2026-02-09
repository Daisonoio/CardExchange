using System.ComponentModel.DataAnnotations;

namespace CardExchange.API.DTOs.Requests
{
    public class CreateTradeOfferRequest
    {
        [Required(ErrorMessage = "Il destinatario è obbligatorio")]
        public int ReceiverId { get; set; }

        [MaxLength(1000)]
        public string? Message { get; set; }

        [Required(ErrorMessage = "Le carte offerte sono obbligatorie")]
        [MinLength(1, ErrorMessage = "Devi offrire almeno una carta")]
        public List<TradeOfferItemRequest> OfferedCards { get; set; } = new();

        [Required(ErrorMessage = "Le carte richieste sono obbligatorie")]
        [MinLength(1, ErrorMessage = "Devi richiedere almeno una carta")]
        public List<TradeOfferItemRequest> RequestedCards { get; set; } = new();
    }

    public class TradeOfferItemRequest
    {
        [Required]
        public int CardId { get; set; }

        [Range(1, 100, ErrorMessage = "La quantità deve essere tra 1 e 100")]
        public int Quantity { get; set; } = 1;
    }

    public class CounterOfferRequest
    {
        [MaxLength(1000)]
        public string? Message { get; set; }

        [Required(ErrorMessage = "Le carte offerte sono obbligatorie")]
        [MinLength(1)]
        public List<TradeOfferItemRequest> OfferedCards { get; set; } = new();

        [Required(ErrorMessage = "Le carte richieste sono obbligatorie")]
        [MinLength(1)]
        public List<TradeOfferItemRequest> RequestedCards { get; set; } = new();
    }

    public class CreateTradeReviewRequest
    {
        [Required]
        [Range(1, 5, ErrorMessage = "Il rating deve essere tra 1 e 5")]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }

        public bool CardAsDescribed { get; set; } = true;
        public bool TimelyShipping { get; set; } = true;
        public bool GoodCommunication { get; set; } = true;
    }
}
