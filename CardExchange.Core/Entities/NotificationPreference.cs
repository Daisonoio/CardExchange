using System.ComponentModel.DataAnnotations;

namespace CardExchange.Core.Entities
{
    public class NotificationPreference : BaseEntity
    {
        public int UserId { get; set; }

        public NotificationType Type { get; set; }

        public bool IsEnabled { get; set; } = true;

        // Relazioni
        public virtual User User { get; set; } = null!;
    }
}
