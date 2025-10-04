using System.ComponentModel.DataAnnotations;

namespace CardExchange.API.DTOs.Requests
{
    public class UpdateCardInfoRequest
    {
        [MaxLength(200)]
        public string? Name { get; set; }

        [MaxLength(50)]
        public string? CardNumber { get; set; }

        [MaxLength(50)]
        public string? Rarity { get; set; }

        [MaxLength(100)]
        public string? Type { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Url(ErrorMessage = "L'URL dell'immagine non è valido")]
        public string? ImageUrl { get; set; }
    }
}