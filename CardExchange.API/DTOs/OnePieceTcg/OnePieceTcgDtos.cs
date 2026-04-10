using System.Text.Json.Serialization;

namespace CardExchange.API.DTOs.OnePieceTcg
{
    // ================================================================
    // Card
    // ================================================================

    public class OnePieceCard
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string? Type { get; set; } // CHARACTER, EVENT, STAGE, LEADER

        [JsonPropertyName("rarity")]
        public string? Rarity { get; set; } // C, UC, R, SR, SEC, L, etc.

        [JsonPropertyName("cost")]
        public int? Cost { get; set; }

        [JsonPropertyName("power")]
        public int? Power { get; set; }

        [JsonPropertyName("counter")]
        public int? Counter { get; set; }

        [JsonPropertyName("color")]
        public string? Color { get; set; } // Red, Blue, Green, Purple, Black, Yellow

        [JsonPropertyName("family")]
        public string? Family { get; set; } // Straw Hat Crew, Donquixote Pirates, etc.

        [JsonPropertyName("ability")]
        public string? Ability { get; set; } // Testo dell'effetto

        [JsonPropertyName("trigger")]
        public string? Trigger { get; set; }

        [JsonPropertyName("attribute")]
        public OnePieceAttribute? Attribute { get; set; }

        [JsonPropertyName("images")]
        public OnePieceImages? Images { get; set; }

        [JsonPropertyName("set")]
        public OnePieceSetInfo? Set { get; set; }

        [JsonPropertyName("notes")]
        public List<string>? Notes { get; set; }
    }

    public class OnePieceAttribute
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("image")]
        public string? Image { get; set; }
    }

    public class OnePieceImages
    {
        [JsonPropertyName("small")]
        public string? Small { get; set; }

        [JsonPropertyName("large")]
        public string? Large { get; set; }
    }

    public class OnePieceSetInfo
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    // ================================================================
    // API Response wrapper (paginato)
    // ================================================================

    public class OnePieceResponse
    {
        [JsonPropertyName("data")]
        public List<OnePieceCard> Data { get; set; } = new();

        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; }

        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("totalPages")]
        public int TotalPages { get; set; }
    }
}
