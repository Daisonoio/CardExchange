namespace CardExchange.API.DTOs.Requests
{
    public class UpdateCardInfoRequest
    {
        [System.ComponentModel.DataAnnotations.MaxLength(200)]
        public string? Name { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(50)]
        public string? CardNumber { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(50)]
        public string? Rarity { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(100)]
        public string? Type { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(1000)]
        public string? Description { get; set; }

        public string? ImageUrl { get; set; }
    }
}
