using System.ComponentModel.DataAnnotations;

namespace CardExchange.API.DTOs.Requests
{
    public class AdvancedSearchRequest
    {
        [MaxLength(200)]
        public string? SearchTerm { get; set; }

        public int? GameId { get; set; }
        public int? CardSetId { get; set; }

        [MaxLength(50)]
        public string? Rarity { get; set; }

        [MaxLength(50)]
        public string? Type { get; set; }

        // Premium filters
        public int? MinCondition { get; set; }
        public int? MaxCondition { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }

        // Geo filters (premium)
        [Range(-90, 90)]
        public decimal? Latitude { get; set; }

        [Range(-180, 180)]
        public decimal? Longitude { get; set; }

        [Range(1, 1000)]
        public int? RadiusKm { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(100)]
        public string? Country { get; set; }

        public bool OnlyAvailableForTrade { get; set; } = true;

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class SaveSearchRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public AdvancedSearchRequest Criteria { get; set; } = new();

        public bool AlertEnabled { get; set; } = false;
    }
}
