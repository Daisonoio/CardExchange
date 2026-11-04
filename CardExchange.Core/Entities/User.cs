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

        /// <summary>Data di nascita, per verificare l'età minima (null per gli account creati prima del controllo).</summary>
        public DateOnly? BirthDate { get; set; }

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
        public string? EmailVerificationToken { get; set; }
        public DateTime? EmailVerificationTokenExpiry { get; set; }

        // Recupero password: si salva solo l'hash SHA-256 del token inviato via email
        [MaxLength(64)]
        public string? PasswordResetTokenHash { get; set; }
        public DateTime? PasswordResetTokenExpiry { get; set; }
        public DateTime? LastLoginAt { get; set; }

        /// <summary>Codice personale per invitare amici (link /register?ref=CODICE). Generato al primo uso.</summary>
        [MaxLength(12)]
        public string? ReferralCode { get; set; }

        /// <summary>Utente che ha invitato questo utente, se registrato con un codice invito.</summary>
        public int? ReferredByUserId { get; set; }

        /// <summary>Ultimo invio del riepilogo settimanale via email.</summary>
        public DateTime? LastWeeklyDigestAt { get; set; }

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
