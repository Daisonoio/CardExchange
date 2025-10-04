namespace CardExchange.API.DTOs.Requests
{
    public class UpdateCardSetRequest
    {
        [System.ComponentModel.DataAnnotations.MaxLength(100)]
        public string? Name { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(10)]
        public string? Code { get; set; }

        public DateTime? ReleaseDate { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(500)]
        public string? Description { get; set; }

        public bool? IsActive { get; set; }
    }
}
