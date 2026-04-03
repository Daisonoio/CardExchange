using CardExchange.Core.Entities;

namespace CardExchange.API.Services.Notifications.Handlers
{
    public class MessageNotificationHandler : INotificationHandler
    {
        public IReadOnlyList<NotificationType> SupportedTypes { get; } = new[]
        {
            NotificationType.NewMessage
        };

        public NotificationPayload BuildPayload(NotificationType type, Dictionary<string, object> parameters)
        {
            var userId = Convert.ToInt32(parameters["UserId"]);
            var senderUsername = parameters["SenderUsername"].ToString()!;

            return new NotificationPayload
            {
                UserId = userId,
                Type = NotificationType.NewMessage,
                Title = "Nuovo messaggio",
                Body = $"{senderUsername} ti ha inviato un messaggio",
                ReferenceId = parameters.TryGetValue("ConversationId", out var cid) ? Convert.ToInt32(cid) : null,
                ReferenceType = "Conversation"
            };
        }
    }
}
