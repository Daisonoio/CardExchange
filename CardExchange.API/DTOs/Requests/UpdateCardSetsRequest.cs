using System.ComponentModel.DataAnnotations;

namespace CardExchange.API.DTOs.Requests
{
    public class UpdateCardSetRequest
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(10)]
        public string? Code { get; set; }

        public DateTime? ReleaseDate { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool? IsActive { get; set; }
    }
}