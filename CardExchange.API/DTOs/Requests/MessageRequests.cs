using System.ComponentModel.DataAnnotations;

namespace CardExchange.API.DTOs.Requests
{
    public class SendMessageRequest
    {
        [Required]
        public int RecipientId { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Content { get; set; } = string.Empty;

        public int? TradeOfferId { get; set; }
    }
}
