using System.Text.Json.Serialization;

namespace CardExchange.API.DTOs.YuGiOh
{
    // ================================================================
    // Card
    // ================================================================

    public class YuGiOhCard
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("frameType")]
        public string? FrameType { get; set; }

        [JsonPropertyName("desc")]
        public string? Desc { get; set; }

        [JsonPropertyName("atk")]
        public int? Atk { get; set; }

        [JsonPropertyName("def")]
        public int? Def { get; set; }

        [JsonPropertyName("level")]
        public int? Level { get; set; }

        [JsonPropertyName("race")]
        public string? Race { get; set; }

        [JsonPropertyName("attribute")]
        public string? Attribute { get; set; }

        [JsonPropertyName("archetype")]
        public string? Archetype { get; set; }

        [JsonPropertyName("ygoprodeck_url")]
        public string? YgoProDeckUrl { get; set; }

        [JsonPropertyName("card_sets")]
        public List<YuGiOhCardSetEntry>? CardSets { get; set; }

        [JsonPropertyName("card_images")]
        public List<YuGiOhCardImage>? CardImages { get; set; }

        [JsonPropertyName("card_prices")]
        public List<YuGiOhCardPrice>? CardPrices { get; set; }

        [JsonPropertyName("banlist_info")]
        public YuGiOhBanlistInfo? BanlistInfo { get; set; }

        // Campi aggiuntivi per scale Pendulum
        [JsonPropertyName("scale")]
        public int? Scale { get; set; }

        [JsonPropertyName("linkval")]
        public int? LinkVal { get; set; }

        [JsonPropertyName("linkmarkers")]
        public List<string>? LinkMarkers { get; set; }

        [JsonPropertyName("pend_desc")]
        public string? PendDesc { get; set; }

        [JsonPropertyName("monster_desc")]
        public string? MonsterDesc { get; set; }
    }

    public class YuGiOhCardSetEntry
    {
        [JsonPropertyName("set_name")]
        public string? SetName { get; set; }

        [JsonPropertyName("set_code")]
        public string? SetCode { get; set; }

        [JsonPropertyName("set_rarity")]
        public string? SetRarity { get; set; }

        [JsonPropertyName("set_rarity_code")]
        public string? SetRarityCode { get; set; }

        [JsonPropertyName("set_price")]
        public string? SetPrice { get; set; }
    }

    public class YuGiOhCardImage
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("image_url")]
        public string? ImageUrl { get; set; }

        [JsonPropertyName("image_url_small")]
        public string? ImageUrlSmall { get; set; }

        [JsonPropertyName("image_url_cropped")]
        public string? ImageUrlCropped { get; set; }
    }

    public class YuGiOhCardPrice
    {
        [JsonPropertyName("cardmarket_price")]
        public string? CardmarketPrice { get; set; }

        [JsonPropertyName("tcgplayer_price")]
        public string? TcgPlayerPrice { get; set; }

        [JsonPropertyName("ebay_price")]
        public string? EbayPrice { get; set; }

        [JsonPropertyName("amazon_price")]
        public string? AmazonPrice { get; set; }

        [JsonPropertyName("coolstuffinc_price")]
        public string? CoolStuffIncPrice { get; set; }
    }

    public class YuGiOhBanlistInfo
    {
        [JsonPropertyName("ban_tcg")]
        public string? BanTcg { get; set; }

        [JsonPropertyName("ban_ocg")]
        public string? BanOcg { get; set; }

        [JsonPropertyName("ban_goat")]
        public string? BanGoat { get; set; }
    }

    // ================================================================
    // Set
    // ================================================================

    public class YuGiOhCardSet
    {
        [JsonPropertyName("set_name")]
        public string SetName { get; set; } = string.Empty;

        [JsonPropertyName("set_code")]
        public string SetCode { get; set; } = string.Empty;

        [JsonPropertyName("num_of_cards")]
        public int NumOfCards { get; set; }

        [JsonPropertyName("tcg_date")]
        public string? TcgDate { get; set; }
    }

    // ================================================================
    // API Response wrapper
    // ================================================================

    public class YuGiOhResponse
    {
        [JsonPropertyName("data")]
        public List<YuGiOhCard> Data { get; set; } = new();
    }
}
