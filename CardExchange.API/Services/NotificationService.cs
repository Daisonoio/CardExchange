using CardExchange.API.Services.Notifications;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificationService> _logger;
        private readonly INotificationDispatcher _dispatcher;

        public NotificationService(
            ApplicationDbContext context,
            ILogger<NotificationService> logger,
            INotificationDispatcher dispatcher)
        {
            _context = context;
            _logger = logger;
            _dispatcher = dispatcher;
        }

        public async Task SendAsync(int userId, NotificationType type, string title, string? body = null, int? referenceId = null, string? referenceType = null)
        {
            await _dispatcher.DispatchAsync(type, new Dictionary<string, object>
            {
                ["UserId"] = userId,
                ["Title"] = title,
                ["Body"] = body ?? "",
                ["ReferenceId"] = referenceId ?? 0,
                ["ReferenceType"] = referenceType ?? ""
            });
        }

        public async Task SendTradeOfferNotificationAsync(int userId, NotificationType type, int tradeOfferId, string senderUsername)
        {
            await _dispatcher.DispatchAsync(type, new Dictionary<string, object>
            {
                ["UserId"] = userId,
                ["TradeOfferId"] = tradeOfferId,
                ["SenderUsername"] = senderUsername
            });
        }

        public async Task SendWishlistMatchNotificationAsync(int userId, string cardName, int cardId)
        {
            await _dispatcher.DispatchAsync(NotificationType.WishlistMatch, new Dictionary<string, object>
            {
                ["UserId"] = userId,
                ["CardName"] = cardName,
                ["CardId"] = cardId
            });
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

            await _dispatcher.DispatchToMultipleAsync(notificationType, favoriteUserIds, new Dictionary<string, object>
            {
                ["CardId"] = cardId,
                ["CardName"] = cardName,
                ["ChangeType"] = changeType
            });
        }
    }
}
