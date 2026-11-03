using CardExchange.Core.Entities;

namespace CardExchange.API.Services.Notifications.Handlers
{
    public class TradeNotificationHandler : INotificationHandler
    {
        public IReadOnlyList<NotificationType> SupportedTypes { get; } = new[]
        {
            NotificationType.TradeOfferReceived,
            NotificationType.TradeOfferAccepted,
            NotificationType.TradeOfferRejected,
            NotificationType.TradeCompleted,
            NotificationType.CounterOfferReceived
        };

        public NotificationPayload BuildPayload(NotificationType type, Dictionary<string, object> parameters)
        {
            var userId = Convert.ToInt32(parameters["UserId"]);

            // Chiamate generiche (NotificationService.SendAsync) passano Title/Body/ReferenceId già pronti
            var tradeOfferId = Convert.ToInt32(
                parameters.TryGetValue("TradeOfferId", out var offerId) ? offerId
                : parameters.TryGetValue("ReferenceId", out var refId) ? refId : 0);

            if (parameters.TryGetValue("Title", out var customTitle) && !string.IsNullOrWhiteSpace(customTitle?.ToString()))
            {
                return new NotificationPayload
                {
                    UserId = userId,
                    Type = type,
                    Title = customTitle.ToString()!,
                    Body = parameters.TryGetValue("Body", out var customBody) ? customBody?.ToString() ?? string.Empty : string.Empty,
                    ReferenceId = tradeOfferId,
                    ReferenceType = "TradeOffer"
                };
            }

            var senderUsername = parameters.TryGetValue("SenderUsername", out var sender) ? sender?.ToString() ?? string.Empty : string.Empty;

            var (title, body) = type switch
            {
                NotificationType.TradeOfferReceived => ("Nuova offerta di scambio", $"{senderUsername} ti ha inviato un'offerta di scambio"),
                NotificationType.TradeOfferAccepted => ("Offerta accettata", $"{senderUsername} ha accettato la tua offerta di scambio"),
                NotificationType.TradeOfferRejected => ("Offerta rifiutata", $"{senderUsername} ha rifiutato la tua offerta di scambio"),
                NotificationType.TradeCompleted => ("Scambio completato", $"Lo scambio con {senderUsername} è stato completato"),
                NotificationType.CounterOfferReceived => ("Controproposta ricevuta", $"{senderUsername} ha inviato una controproposta"),
                _ => ("Aggiornamento scambio", $"Aggiornamento sullo scambio con {senderUsername}")
            };

            return new NotificationPayload
            {
                UserId = userId,
                Type = type,
                Title = title,
                Body = body,
                ReferenceId = tradeOfferId,
                ReferenceType = "TradeOffer"
            };
        }
    }
}
