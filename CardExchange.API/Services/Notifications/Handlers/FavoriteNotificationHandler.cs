using CardExchange.Core.Entities;

namespace CardExchange.API.Services.Notifications.Handlers
{
    public class FavoriteNotificationHandler : INotificationHandler
    {
        public IReadOnlyList<NotificationType> SupportedTypes { get; } = new[]
        {
            NotificationType.FavoritePriceChanged,
            NotificationType.FavoriteCardTraded
        };

        public NotificationPayload BuildPayload(NotificationType type, Dictionary<string, object> parameters)
        {
            var userId = Convert.ToInt32(parameters["UserId"]);
            var cardId = Convert.ToInt32(parameters["CardId"]);
            var cardName = parameters["CardName"].ToString()!;
            var changeType = parameters.TryGetValue("ChangeType", out var ct) ? ct.ToString()! : "update";

            var (title, body) = changeType switch
            {
                "price" => ("Prezzo aggiornato", $"Il prezzo della carta \"{cardName}\" che hai tra i preferiti è stato modificato"),
                "traded" => ("Carta scambiata", $"La carta \"{cardName}\" che hai tra i preferiti è stata scambiata"),
                "removed" => ("Carta rimossa", $"La carta \"{cardName}\" che hai tra i preferiti non è più disponibile"),
                _ => ("Aggiornamento preferito", $"La carta \"{cardName}\" nei tuoi preferiti è stata aggiornata")
            };

            return new NotificationPayload
            {
                UserId = userId,
                Type = type,
                Title = title,
                Body = body,
                ReferenceId = cardId,
                ReferenceType = "Card"
            };
        }
    }
}
