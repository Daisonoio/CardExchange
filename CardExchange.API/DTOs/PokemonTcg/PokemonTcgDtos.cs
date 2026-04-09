using System.Text.Json.Serialization;

namespace CardExchange.API.DTOs.PokemonTcg
{
    // ================================================================
    // Card
    // ================================================================

    public class PokemonTcgCard
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("supertype")]
        public string? Supertype { get; set; }

        [JsonPropertyName("subtypes")]
        public List<string>? Subtypes { get; set; }

        [JsonPropertyName("hp")]
        public string? Hp { get; set; }

        [JsonPropertyName("types")]
        public List<string>? Types { get; set; }

        [JsonPropertyName("evolvesFrom")]
        public string? EvolvesFrom { get; set; }

        [JsonPropertyName("evolvesTo")]
        public List<string>? EvolvesTo { get; set; }

        [JsonPropertyName("rules")]
        public List<string>? Rules { get; set; }

        [JsonPropertyName("attacks")]
        public List<PokemonTcgAttack>? Attacks { get; set; }

        [JsonPropertyName("weaknesses")]
        public List<PokemonTcgEffect>? Weaknesses { get; set; }

        [JsonPropertyName("resistances")]
        public List<PokemonTcgEffect>? Resistances { get; set; }

        [JsonPropertyName("retreatCost")]
        public List<string>? RetreatCost { get; set; }

        [JsonPropertyName("convertedRetreatCost")]
        public int? ConvertedRetreatCost { get; set; }

        [JsonPropertyName("number")]
        public string? Number { get; set; }

        [JsonPropertyName("artist")]
        public string? Artist { get; set; }

        [JsonPropertyName("rarity")]
        public string? Rarity { get; set; }

        [JsonPropertyName("nationalPokedexNumbers")]
        public List<int>? NationalPokedexNumbers { get; set; }

        [JsonPropertyName("legalities")]
        public PokemonTcgLegalities? Legalities { get; set; }

        [JsonPropertyName("images")]
        public PokemonTcgCardImages? Images { get; set; }

        [JsonPropertyName("tcgplayer")]
        public PokemonTcgPlayerData? TcgPlayer { get; set; }

        [JsonPropertyName("cardmarket")]
        public PokemonTcgCardmarketData? Cardmarket { get; set; }

        [JsonPropertyName("set")]
        public PokemonTcgSet? Set { get; set; }

        [JsonPropertyName("abilities")]
        public List<PokemonTcgAbility>? Abilities { get; set; }

        [JsonPropertyName("flavorText")]
        public string? FlavorText { get; set; }

        [JsonPropertyName("regulationMark")]
        public string? RegulationMark { get; set; }
    }

    public class PokemonTcgAttack
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("cost")]
        public List<string>? Cost { get; set; }

        [JsonPropertyName("convertedEnergyCost")]
        public int ConvertedEnergyCost { get; set; }

        [JsonPropertyName("damage")]
        public string? Damage { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    public class PokemonTcgAbility
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    public class PokemonTcgEffect
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }

    public class PokemonTcgLegalities
    {
        [JsonPropertyName("unlimited")]
        public string? Unlimited { get; set; }

        [JsonPropertyName("standard")]
        public string? Standard { get; set; }

        [JsonPropertyName("expanded")]
        public string? Expanded { get; set; }
    }

    public class PokemonTcgCardImages
    {
        [JsonPropertyName("small")]
        public string? Small { get; set; }

        [JsonPropertyName("large")]
        public string? Large { get; set; }
    }

    // ================================================================
    // Prezzi TCGPlayer
    // ================================================================

    public class PokemonTcgPlayerData
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("updatedAt")]
        public string? UpdatedAt { get; set; }

        [JsonPropertyName("prices")]
        public PokemonTcgPlayerPrices? Prices { get; set; }
    }

    public class PokemonTcgPlayerPrices
    {
        [JsonPropertyName("normal")]
        public PokemonTcgPriceVariant? Normal { get; set; }

        [JsonPropertyName("holofoil")]
        public PokemonTcgPriceVariant? Holofoil { get; set; }

        [JsonPropertyName("reverseHolofoil")]
        public PokemonTcgPriceVariant? ReverseHolofoil { get; set; }

        [JsonPropertyName("1stEditionHolofoil")]
        public PokemonTcgPriceVariant? FirstEditionHolofoil { get; set; }

        [JsonPropertyName("1stEditionNormal")]
        public PokemonTcgPriceVariant? FirstEditionNormal { get; set; }
    }

    public class PokemonTcgPriceVariant
    {
        [JsonPropertyName("low")]
        public decimal? Low { get; set; }

        [JsonPropertyName("mid")]
        public decimal? Mid { get; set; }

        [JsonPropertyName("high")]
        public decimal? High { get; set; }

        [JsonPropertyName("market")]
        public decimal? Market { get; set; }

        [JsonPropertyName("directLow")]
        public decimal? DirectLow { get; set; }
    }

    // ================================================================
    // Prezzi Cardmarket
    // ================================================================

    public class PokemonTcgCardmarketData
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("updatedAt")]
        public string? UpdatedAt { get; set; }

        [JsonPropertyName("prices")]
        public PokemonTcgCardmarketPrices? Prices { get; set; }
    }

    public class PokemonTcgCardmarketPrices
    {
        [JsonPropertyName("averageSellPrice")]
        public decimal? AverageSellPrice { get; set; }

        [JsonPropertyName("lowPrice")]
        public decimal? LowPrice { get; set; }

        [JsonPropertyName("trendPrice")]
        public decimal? TrendPrice { get; set; }

        [JsonPropertyName("avg1")]
        public decimal? Avg1 { get; set; }

        [JsonPropertyName("avg7")]
        public decimal? Avg7 { get; set; }

        [JsonPropertyName("avg30")]
        public decimal? Avg30 { get; set; }

        [JsonPropertyName("reverseHoloAvg1")]
        public decimal? ReverseHoloAvg1 { get; set; }

        [JsonPropertyName("reverseHoloAvg7")]
        public decimal? ReverseHoloAvg7 { get; set; }

        [JsonPropertyName("reverseHoloAvg30")]
        public decimal? ReverseHoloAvg30 { get; set; }
    }

    // ================================================================
    // Set
    // ================================================================

    public class PokemonTcgSet
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("series")]
        public string? Series { get; set; }

        [JsonPropertyName("printedTotal")]
        public int PrintedTotal { get; set; }

        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("legalities")]
        public PokemonTcgLegalities? Legalities { get; set; }

        [JsonPropertyName("ptcgoCode")]
        public string? PtcgoCode { get; set; }

        [JsonPropertyName("releaseDate")]
        public string? ReleaseDate { get; set; }

        [JsonPropertyName("updatedAt")]
        public string? UpdatedAt { get; set; }

        [JsonPropertyName("images")]
        public PokemonTcgSetImages? Images { get; set; }
    }

    public class PokemonTcgSetImages
    {
        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("logo")]
        public string? Logo { get; set; }
    }

    // ================================================================
    // API Response wrapper
    // ================================================================

    public class PokemonTcgResponse<T>
    {
        [JsonPropertyName("data")]
        public T Data { get; set; } = default!;

        [JsonPropertyName("page")]
        public int? Page { get; set; }

        [JsonPropertyName("pageSize")]
        public int? PageSize { get; set; }

        [JsonPropertyName("count")]
        public int? Count { get; set; }

        [JsonPropertyName("totalCount")]
        public int? TotalCount { get; set; }
    }
}
