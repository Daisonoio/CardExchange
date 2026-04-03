using CardExchange.Core.Entities;

namespace CardExchange.API.Services.Notifications.Handlers
{
    public class WishlistNotificationHandler : INotificationHandler
    {
        public IReadOnlyList<NotificationType> SupportedTypes { get; } = new[]
        {
            NotificationType.WishlistMatch
        };

        public NotificationPayload BuildPayload(NotificationType type, Dictionary<string, object> parameters)
        {
            var userId = Convert.ToInt32(parameters["UserId"]);
            var cardName = parameters["CardName"].ToString()!;
            var cardId = Convert.ToInt32(parameters["CardId"]);

            return new NotificationPayload
            {
                UserId = userId,
                Type = NotificationType.WishlistMatch,
                Title = "Carta trovata nella tua wishlist!",
                Body = $"La carta \"{cardName}\" che cercavi è ora disponibile per lo scambio",
                ReferenceId = cardId,
                ReferenceType = "Card"
            };
        }
    }
}
