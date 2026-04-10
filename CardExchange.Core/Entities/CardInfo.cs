using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardExchange.Core.Entities
{
    public class CardInfo : BaseEntity
    {
        [Required]
        public int CardSetId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? CardNumber { get; set; }

        [MaxLength(50)]
        public string? Rarity { get; set; }

        [MaxLength(100)]
        public string? Type { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public string? ImageUrl { get; set; }

        // === Campi Scryfall ===

        [MaxLength(36)]
        public string? ScryfallId { get; set; }

        [MaxLength(36)]
        public string? OracleId { get; set; }

        [MaxLength(100)]
        public string? ManaCost { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? Cmc { get; set; }

        [MaxLength(200)]
        public string? TypeLine { get; set; }

        public string? OracleText { get; set; }

        [MaxLength(50)]
        public string? Colors { get; set; }

        [MaxLength(50)]
        public string? ColorIdentity { get; set; }

        [MaxLength(10)]
        public string? Power { get; set; }

        [MaxLength(10)]
        public string? Toughness { get; set; }

        [MaxLength(10)]
        public string? Loyalty { get; set; }

        [MaxLength(500)]
        public string? Keywords { get; set; }

        [MaxLength(200)]
        public string? Artist { get; set; }

        // Immagini (Scryfall CDN *.scryfall.io - no rate limit)
        public string? ImageSmall { get; set; }
        public string? ImageNormal { get; set; }
        public string? ImageLarge { get; set; }
        public string? ImagePng { get; set; }
        public string? ImageArtCrop { get; set; }
        public string? ImageBorderCrop { get; set; }

        // Prezzi di mercato
        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceUsd { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceUsdFoil { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceEur { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceEurFoil { get; set; }

        public string? Legalities { get; set; }
        public string? ScryfallUri { get; set; }
        public DateTime? ScryfallUpdatedAt { get; set; }

        // === Campi Pokémon TCG ===

        [MaxLength(50)]
        public string? PokemonTcgId { get; set; }

        [MaxLength(20)]
        public string? Supertype { get; set; } // Pokémon, Trainer, Energy

        [MaxLength(200)]
        public string? Subtypes { get; set; } // CSV: "Stage 1,VMAX"

        [MaxLength(10)]
        public string? Hp { get; set; }

        [MaxLength(200)]
        public string? PokemonTypes { get; set; } // CSV: "Fire,Water"

        [MaxLength(200)]
        public string? EvolvesFrom { get; set; }

        // Dati complessi compressi in JSON (attacchi, debolezze, resistenze, regole)
        public string? PokemonTcgData { get; set; }

        // Immagini Pokémon TCG (CDN, no rate limit)
        public string? PokemonImageSmall { get; set; }
        public string? PokemonImageLarge { get; set; }

        // Prezzi Pokémon TCG (TCGPlayer market)
        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceTcgNormal { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceTcgHolofoil { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceTcgReverseHolofoil { get; set; }

        // Prezzi Pokémon TCG (Cardmarket)
        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceCardmarketAvg { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceCardmarketTrend { get; set; }

        public DateTime? PokemonTcgUpdatedAt { get; set; }

        // === Campi Yu-Gi-Oh! (YGOProDeck) ===

        public int? YuGiOhId { get; set; } // Passcode a 8 cifre

        [MaxLength(50)]
        public string? YuGiOhType { get; set; } // Effect Monster, Spell Card, Trap Card, etc.

        [MaxLength(20)]
        public string? YuGiOhAttribute { get; set; } // DARK, LIGHT, FIRE, WATER, EARTH, WIND, DIVINE

        [MaxLength(50)]
        public string? YuGiOhRace { get; set; } // Dragon, Spellcaster, Warrior, etc.

        public int? YuGiOhLevel { get; set; } // Livello/Rank/Link Rating

        public int? YuGiOhAtk { get; set; }
        public int? YuGiOhDef { get; set; }

        [MaxLength(100)]
        public string? YuGiOhArchetype { get; set; }

        [MaxLength(50)]
        public string? YuGiOhFrameType { get; set; } // normal, effect, ritual, fusion, synchro, xyz, link, spell, trap

        public string? YuGiOhImageUrl { get; set; }
        public string? YuGiOhImageSmall { get; set; }

        // Dati estesi compressi in JSON (card_sets, banlist_info)
        public string? YuGiOhData { get; set; }

        // Prezzi Yu-Gi-Oh! (da card_prices)
        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceYuGiOhTcgPlayer { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceYuGiOhCardmarket { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceYuGiOhEbay { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceYuGiOhAmazon { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? PriceYuGiOhCoolstuffinc { get; set; }

        public DateTime? YuGiOhUpdatedAt { get; set; }

        // === Campi One Piece TCG (ApiTCG) ===

        [MaxLength(20)]
        public string? OnePieceTcgId { get; set; } // Codice carta, es. "OP06-014"

        [MaxLength(30)]
        public string? OnePieceColor { get; set; } // Red, Blue, Green, etc.

        public int? OnePieceCost { get; set; }
        public int? OnePiecePower { get; set; }
        public int? OnePieceCounter { get; set; }

        [MaxLength(200)]
        public string? OnePieceFamily { get; set; } // Straw Hat Crew, etc.

        public string? OnePieceAbility { get; set; } // Effetto della carta
        public string? OnePieceTrigger { get; set; } // Trigger text

        public string? OnePieceImageSmall { get; set; }
        public string? OnePieceImageLarge { get; set; }

        public DateTime? OnePieceTcgUpdatedAt { get; set; }

        // Relazioni
        [ForeignKey("CardSetId")]
        public virtual CardSet CardSet { get; set; } = null!;
        public virtual ICollection<Card> Cards { get; set; } = new List<Card>();
        public virtual ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
    }
}