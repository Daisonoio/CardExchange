using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;

namespace CardExchange.API.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(ApplicationDbContext context, ILogger<NotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SendAsync(int userId, NotificationType type, string title, string? body = null, int? referenceId = null, string? referenceType = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Body = body,
                ReferenceId = referenceId,
                ReferenceType = referenceType
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Notifica inviata a utente {UserId}: {Type} - {Title}", userId, type, title);
        }

        public async Task SendTradeOfferNotificationAsync(int userId, NotificationType type, int tradeOfferId, string senderUsername)
        {
            var (title, body) = type switch
            {
                NotificationType.TradeOfferReceived => ("Nuova offerta di scambio", $"{senderUsername} ti ha inviato un'offerta di scambio"),
                NotificationType.TradeOfferAccepted => ("Offerta accettata", $"{senderUsername} ha accettato la tua offerta di scambio"),
                NotificationType.TradeOfferRejected => ("Offerta rifiutata", $"{senderUsername} ha rifiutato la tua offerta di scambio"),
                NotificationType.TradeCompleted => ("Scambio completato", $"Lo scambio con {senderUsername} è stato completato"),
                _ => ("Aggiornamento scambio", $"Aggiornamento sullo scambio con {senderUsername}")
            };

            await SendAsync(userId, type, title, body, tradeOfferId, "TradeOffer");
        }

        public async Task SendWishlistMatchNotificationAsync(int userId, string cardName, int cardId)
        {
            await SendAsync(
                userId,
                NotificationType.WishlistMatch,
                "Carta trovata nella tua wishlist!",
                $"La carta \"{cardName}\" che cercavi è ora disponibile per lo scambio",
                cardId,
                "Card"
            );
        }
    }
}
