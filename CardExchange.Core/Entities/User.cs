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

        [MaxLength(500)]
        public string? AvatarUrl { get; set; }

        [MaxLength(120)]
        public string? PaypalUsername { get; set; }

        [MaxLength(120)]
        public string? SatispayUsername { get; set; }

        [MaxLength(600)]
        public string? PaymentQrCodeUrl { get; set; }

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
        public bool EmailConfirmed { get; set; } = false;
        public DateTime? LastLoginAt { get; set; }

        // Reputazione
        public decimal ReputationScore { get; set; } = 0;
        public int TotalTradesCompleted { get; set; } = 0;
        public int TotalReviewsReceived { get; set; } = 0;

        // Soglia spike prezzo (percentuale, default 10%)
        public decimal PriceSpikeThreshold { get; set; } = 10;

        // JWT Refresh Token
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }

        // Relazioni
        public virtual UserLocation? Location { get; set; }
        public virtual ICollection<Card> Cards { get; set; } = new List<Card>();
        public virtual ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
        public virtual ICollection<TradeOffer> SentOffers { get; set; } = new List<TradeOffer>();
        public virtual ICollection<TradeOffer> ReceivedOffers { get; set; } = new List<TradeOffer>();
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public virtual ICollection<UserSubscription> Subscriptions { get; set; } = new List<UserSubscription>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public virtual ICollection<NotificationPreference> NotificationPreferences { get; set; } = new List<NotificationPreference>();
        public virtual ICollection<SavedSearch> SavedSearches { get; set; } = new List<SavedSearch>();
        public virtual ICollection<FavoriteCard> FavoriteCards { get; set; } = new List<FavoriteCard>();
    }
}
