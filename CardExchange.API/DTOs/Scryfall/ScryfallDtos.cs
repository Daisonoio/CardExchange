using System.Text.Json.Serialization;

namespace CardExchange.API.DTOs.Scryfall
{
    /// <summary>
    /// Card object restituito da Scryfall API
    /// </summary>
    public class ScryfallCard
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("oracle_id")]
        public string? OracleId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("mana_cost")]
        public string? ManaCost { get; set; }

        [JsonPropertyName("cmc")]
        public decimal? Cmc { get; set; }

        [JsonPropertyName("type_line")]
        public string? TypeLine { get; set; }

        [JsonPropertyName("oracle_text")]
        public string? OracleText { get; set; }

        [JsonPropertyName("colors")]
        public List<string>? Colors { get; set; }

        [JsonPropertyName("color_identity")]
        public List<string>? ColorIdentity { get; set; }

        [JsonPropertyName("power")]
        public string? Power { get; set; }

        [JsonPropertyName("toughness")]
        public string? Toughness { get; set; }

        [JsonPropertyName("loyalty")]
        public string? Loyalty { get; set; }

        [JsonPropertyName("keywords")]
        public List<string>? Keywords { get; set; }

        [JsonPropertyName("set")]
        public string SetCode { get; set; } = string.Empty;

        [JsonPropertyName("set_name")]
        public string SetName { get; set; } = string.Empty;

        [JsonPropertyName("set_id")]
        public string? SetId { get; set; }

        [JsonPropertyName("collector_number")]
        public string? CollectorNumber { get; set; }

        [JsonPropertyName("rarity")]
        public string? Rarity { get; set; }

        [JsonPropertyName("artist")]
        public string? Artist { get; set; }

        [JsonPropertyName("image_uris")]
        public ScryfallImageUris? ImageUris { get; set; }

        [JsonPropertyName("prices")]
        public ScryfallPrices? Prices { get; set; }

        [JsonPropertyName("legalities")]
        public Dictionary<string, string>? Legalities { get; set; }

        [JsonPropertyName("scryfall_uri")]
        public string? ScryfallUri { get; set; }

        [JsonPropertyName("card_faces")]
        public List<ScryfallCardFace>? CardFaces { get; set; }

        [JsonPropertyName("finishes")]
        public List<string>? Finishes { get; set; }

        [JsonPropertyName("lang")]
        public string? Lang { get; set; }
    }

    public class ScryfallImageUris
    {
        [JsonPropertyName("small")]
        public string? Small { get; set; }

        [JsonPropertyName("normal")]
        public string? Normal { get; set; }

        [JsonPropertyName("large")]
        public string? Large { get; set; }

        [JsonPropertyName("png")]
        public string? Png { get; set; }

        [JsonPropertyName("art_crop")]
        public string? ArtCrop { get; set; }

        [JsonPropertyName("border_crop")]
        public string? BorderCrop { get; set; }
    }

    public class ScryfallPrices
    {
        [JsonPropertyName("usd")]
        public string? Usd { get; set; }

        [JsonPropertyName("usd_foil")]
        public string? UsdFoil { get; set; }

        [JsonPropertyName("eur")]
        public string? Eur { get; set; }

        [JsonPropertyName("eur_foil")]
        public string? EurFoil { get; set; }
    }

    public class ScryfallCardFace
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("mana_cost")]
        public string? ManaCost { get; set; }

        [JsonPropertyName("type_line")]
        public string? TypeLine { get; set; }

        [JsonPropertyName("oracle_text")]
        public string? OracleText { get; set; }

        [JsonPropertyName("image_uris")]
        public ScryfallImageUris? ImageUris { get; set; }

        [JsonPropertyName("power")]
        public string? Power { get; set; }

        [JsonPropertyName("toughness")]
        public string? Toughness { get; set; }
    }

    /// <summary>
    /// Set object restituito da Scryfall API
    /// </summary>
    public class ScryfallSet
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("set_type")]
        public string? SetType { get; set; }

        [JsonPropertyName("released_at")]
        public string? ReleasedAt { get; set; }

        [JsonPropertyName("card_count")]
        public int CardCount { get; set; }

        [JsonPropertyName("digital")]
        public bool Digital { get; set; }

        [JsonPropertyName("icon_svg_uri")]
        public string? IconSvgUri { get; set; }

        [JsonPropertyName("scryfall_uri")]
        public string? ScryfallUri { get; set; }
    }

    /// <summary>
    /// List wrapper Scryfall (paginazione)
    /// </summary>
    public class ScryfallList<T>
    {
        [JsonPropertyName("object")]
        public string Object { get; set; } = string.Empty;

        [JsonPropertyName("total_cards")]
        public int? TotalCards { get; set; }

        [JsonPropertyName("has_more")]
        public bool HasMore { get; set; }

        [JsonPropertyName("next_page")]
        public string? NextPage { get; set; }

        [JsonPropertyName("data")]
        public List<T> Data { get; set; } = new();
    }

    /// <summary>
    /// Risultato dell'autocomplete Scryfall
    /// </summary>
    public class ScryfallCatalog
    {
        [JsonPropertyName("object")]
        public string Object { get; set; } = string.Empty;

        [JsonPropertyName("total_values")]
        public int TotalValues { get; set; }

        [JsonPropertyName("data")]
        public List<string> Data { get; set; } = new();
    }

    /// <summary>
    /// Request body per POST /cards/collection
    /// </summary>
    public class ScryfallCollectionRequest
    {
        [JsonPropertyName("identifiers")]
        public List<ScryfallIdentifier> Identifiers { get; set; } = new();
    }

    public class ScryfallIdentifier
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("set")]
        public string? Set { get; set; }

        [JsonPropertyName("collector_number")]
        public string? CollectorNumber { get; set; }
    }

    /// <summary>
    /// Risposta da POST /cards/collection
    /// </summary>
    public class ScryfallCollectionResponse
    {
        [JsonPropertyName("object")]
        public string Object { get; set; } = string.Empty;

        [JsonPropertyName("not_found")]
        public List<ScryfallIdentifier> NotFound { get; set; } = new();

        [JsonPropertyName("data")]
        public List<ScryfallCard> Data { get; set; } = new();
    }

    /// <summary>
    /// Errore restituito da Scryfall API
    /// </summary>
    public class ScryfallError
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("details")]
        public string? Details { get; set; }
    }
}
