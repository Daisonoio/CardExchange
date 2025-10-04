using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardExchange.Core.Entities
{
    public enum CardCondition
    {
        Mint = 1,
        NearMint = 2,
        Excellent = 3,
        Good = 4,
        LightlyPlayed = 5,
        ModeratelyPlayed = 6,
        HeavilyPlayed = 7,
        Damaged = 8
    }

    public class Card : BaseEntity
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public int CardInfoId { get; set; }

        [Required]
        public CardCondition Condition { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public bool IsAvailableForTrade { get; set; } = true;

        public decimal? EstimatedValue { get; set; }

        // Relazioni
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [ForeignKey("CardInfoId")]
        public virtual CardInfo CardInfo { get; set; } = null!;
    }
}