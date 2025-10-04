using System.ComponentModel.DataAnnotations;

namespace CardExchange.Core.Entities
{
    public class User : BaseEntity
    {
        [Required]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Bio { get; set; }

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
        public bool EmailConfirmed { get; set; } = false;
        public DateTime? LastLoginAt { get; set; }

        // Relazioni
        public virtual UserLocation? Location { get; set; }
        public virtual ICollection<Card> Cards { get; set; } = new List<Card>();
        public virtual ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
        public virtual ICollection<TradeOffer> SentOffers { get; set; } = new List<TradeOffer>();
        public virtual ICollection<TradeOffer> ReceivedOffers { get; set; } = new List<TradeOffer>();
    }
}