using System.ComponentModel.DataAnnotations;

namespace CardExchange.Core.Entities
{
    public enum NotificationType
    {
        WishlistMatch = 1,
        TradeOfferReceived = 2,
        TradeOfferAccepted = 3,
        TradeOfferRejected = 4,
        TradeCompleted = 5,
        NewMessage = 6,
        NewReview = 7,
        SubscriptionExpiring = 8,
        SubscriptionExpired = 9,
        SystemAnnouncement = 10,
        FavoritePriceChanged = 11,
        FavoriteCardTraded = 12,
        CounterOfferReceived = 13
    }

    public class Notification : BaseEntity
    {
        public int UserId { get; set; }

        public NotificationType Type { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Body { get; set; }

        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }

        // Link opzionale alla risorsa correlata
        public int? ReferenceId { get; set; }

        [MaxLength(50)]
        public string? ReferenceType { get; set; }

        // Relazioni
        public virtual User User { get; set; } = null!;
    }
}
