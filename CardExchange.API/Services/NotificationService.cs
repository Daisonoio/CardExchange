using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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

        public async Task NotifyFavoriteCardChangedAsync(int cardId, string cardName, string changeType)
        {
            var favoriteUserIds = await _context.FavoriteCards
                .AsNoTracking()
                .Where(fc => fc.CardId == cardId)
                .Select(fc => fc.UserId)
                .ToListAsync();

            var notificationType = changeType == "price"
                ? NotificationType.FavoritePriceChanged
                : NotificationType.FavoriteCardTraded;

            var (title, body) = changeType switch
            {
                "price" => ("Prezzo aggiornato", $"Il prezzo della carta \"{cardName}\" che hai tra i preferiti è stato modificato"),
                "traded" => ("Carta scambiata", $"La carta \"{cardName}\" che hai tra i preferiti è stata scambiata"),
                "removed" => ("Carta rimossa", $"La carta \"{cardName}\" che hai tra i preferiti non è più disponibile"),
                _ => ("Aggiornamento preferito", $"La carta \"{cardName}\" nei tuoi preferiti è stata aggiornata")
            };

            foreach (var uid in favoriteUserIds)
            {
                await SendAsync(uid, notificationType, title, body, cardId, "Card");
            }
        }
    }
}
