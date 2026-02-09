using CardExchange.Core.Entities;

namespace CardExchange.API.Services
{
    public interface INotificationService
    {
        Task SendAsync(int userId, NotificationType type, string title, string? body = null, int? referenceId = null, string? referenceType = null);
        Task SendTradeOfferNotificationAsync(int userId, NotificationType type, int tradeOfferId, string senderUsername);
        Task SendWishlistMatchNotificationAsync(int userId, string cardName, int cardId);
    }
}
