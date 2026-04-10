using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Dynamic;

namespace CardExchange.Core.Entities
{
    public class CardSet : BaseEntity
    {
        [Required]
        public int GameId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        public DateTime? ReleaseDate { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        // === Campi Scryfall ===

        [MaxLength(36)]
        public string? ScryfallId { get; set; }

        [MaxLength(50)]
        public string? SetType { get; set; }

        public int? CardCount { get; set; }

        public string? IconSvgUri { get; set; }

        public bool IsDigital { get; set; } = false;

        public DateTime? ScryfallUpdatedAt { get; set; }

        // === Campi Pokémon TCG ===

        [MaxLength(50)]
        public string? PokemonTcgId { get; set; }

        [MaxLength(100)]
        public string? Series { get; set; }

        public int? PrintedTotal { get; set; }

        public string? PokemonLogoUrl { get; set; }
        public string? PokemonSymbolUrl { get; set; }

        public DateTime? PokemonTcgUpdatedAt { get; set; }

        // === Campi Yu-Gi-Oh! (YGOProDeck) ===

        [MaxLength(50)]
        public string? YuGiOhSetCode { get; set; }

        public int? YuGiOhNumCards { get; set; }

        [MaxLength(20)]
        public string? YuGiOhTcgDate { get; set; } // Data formato "YYYY-MM-DD"

        public DateTime? YuGiOhUpdatedAt { get; set; }

        // === Campi One Piece TCG (ApiTCG) ===

        [MaxLength(100)]
        public string? OnePieceTcgSetName { get; set; }

        public DateTime? OnePieceTcgUpdatedAt { get; set; }

        // Relazioni
        [ForeignKey("GameId")]
        public virtual Game Game { get; set; } = null!;
        public virtual ICollection<CardInfo> CardInfos { get; set; } = new List<CardInfo>();
    }
}