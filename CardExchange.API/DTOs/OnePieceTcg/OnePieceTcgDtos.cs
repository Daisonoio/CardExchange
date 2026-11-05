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

        /// <summary>Prezzo di mercato in dollari (TCGplayer, via OPTCG API).</summary>
        [JsonPropertyName("priceUsd")]
        public decimal? PriceUsd { get; set; }

        /// <summary>Prezzo convertito in euro al cambio BCE del giorno.</summary>
        [JsonPropertyName("priceEur")]
        public decimal? PriceEur { get; set; }
    }

    // ================================================================
    // OPTCG API (optcgapi.com): formato originale delle carte
    // ================================================================

    public class OptcgApiCard
    {
        [JsonPropertyName("card_name")] public string? CardName { get; set; }
        [JsonPropertyName("set_name")] public string? SetName { get; set; }
        [JsonPropertyName("set_id")] public string? SetId { get; set; }
        [JsonPropertyName("card_text")] public string? CardText { get; set; }
        [JsonPropertyName("rarity")] public string? Rarity { get; set; }
        [JsonPropertyName("card_set_id")] public string? CardSetId { get; set; }
        [JsonPropertyName("card_color")] public string? CardColor { get; set; }
        [JsonPropertyName("card_type")] public string? CardType { get; set; }
        [JsonPropertyName("card_cost")] public System.Text.Json.JsonElement? CardCost { get; set; }
        [JsonPropertyName("card_power")] public System.Text.Json.JsonElement? CardPower { get; set; }
        [JsonPropertyName("counter_amount")] public System.Text.Json.JsonElement? CounterAmount { get; set; }
        [JsonPropertyName("sub_types")] public string? SubTypes { get; set; }
        [JsonPropertyName("attribute")] public string? Attribute { get; set; }
        /// <summary>Identifica la singola stampa: "OP01-001", "OP01-001_p1" (parallel), "P-001_r1"…</summary>
        [JsonPropertyName("card_image_id")] public string? CardImageId { get; set; }
        [JsonPropertyName("card_image")] public string? CardImage { get; set; }
        [JsonPropertyName("market_price")] public decimal? MarketPrice { get; set; }
        [JsonPropertyName("inventory_price")] public decimal? InventoryPrice { get; set; }

        /// <summary>Numero intero da un campo che l'API manda a volte come numero, a volte come testo ("5000", "-").</summary>
        public static int? ToInt(System.Text.Json.JsonElement? e)
        {
            if (e is not { } v) return null;
            if (v.ValueKind == System.Text.Json.JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
            if (v.ValueKind == System.Text.Json.JsonValueKind.String && int.TryParse(v.GetString()?.Trim(), out var m)) return m;
            return null;
        }
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
