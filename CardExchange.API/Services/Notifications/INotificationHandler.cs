using CardExchange.Core.Entities;

namespace CardExchange.API.Services.Notifications
{
    public class NotificationPayload
    {
        public int UserId { get; set; }
        public NotificationType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Body { get; set; }
        public int? ReferenceId { get; set; }
        public string? ReferenceType { get; set; }
    }

    public interface INotificationHandler
    {
        IReadOnlyList<NotificationType> SupportedTypes { get; }

        NotificationPayload BuildPayload(NotificationType type, Dictionary<string, object> parameters);
    }
}
