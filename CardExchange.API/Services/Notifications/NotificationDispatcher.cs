using CardExchange.API.Hubs;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Notifications
{
    public interface INotificationDispatcher
    {
        Task DispatchAsync(NotificationType type, Dictionary<string, object> parameters);
        Task DispatchToMultipleAsync(NotificationType type, IEnumerable<int> userIds, Dictionary<string, object> sharedParameters);
    }

    public class NotificationDispatcher : INotificationDispatcher
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificationDispatcher> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly Dictionary<NotificationType, INotificationHandler> _handlerMap;

        public NotificationDispatcher(
            ApplicationDbContext context,
            ILogger<NotificationDispatcher> logger,
            IHubContext<NotificationHub> hubContext,
            IEnumerable<INotificationHandler> handlers)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
            _handlerMap = new Dictionary<NotificationType, INotificationHandler>();

            foreach (var handler in handlers)
            {
                foreach (var type in handler.SupportedTypes)
                {
                    _handlerMap[type] = handler;
                }
            }
        }

        public async Task DispatchAsync(NotificationType type, Dictionary<string, object> parameters)
        {
            if (!_handlerMap.TryGetValue(type, out var handler))
            {
                _logger.LogWarning("Nessun handler registrato per il tipo di notifica {Type}", type);
                return;
            }

            var payload = handler.BuildPayload(type, parameters);

            if (!await IsNotificationEnabledAsync(payload.UserId, type))
            {
                _logger.LogDebug("Notifica {Type} disabilitata per l'utente {UserId}", type, payload.UserId);
                return;
            }

            var notification = await PersistNotificationAsync(payload);
            await PushToClientAsync(notification);
        }

        public async Task DispatchToMultipleAsync(NotificationType type, IEnumerable<int> userIds, Dictionary<string, object> sharedParameters)
        {
            if (!_handlerMap.TryGetValue(type, out var handler))
            {
                _logger.LogWarning("Nessun handler registrato per il tipo di notifica {Type}", type);
                return;
            }

            var userIdList = userIds.ToList();

            var disabledUserIds = await _context.NotificationPreferences
                .AsNoTracking()
                .Where(p => userIdList.Contains(p.UserId) && p.Type == type && !p.IsEnabled)
                .Select(p => p.UserId)
                .ToListAsync();

            var enabledUserIds = userIdList.Except(disabledUserIds);

            foreach (var userId in enabledUserIds)
            {
                var parameters = new Dictionary<string, object>(sharedParameters)
                {
                    ["UserId"] = userId
                };

                var payload = handler.BuildPayload(type, parameters);
                var notification = await PersistNotificationAsync(payload);
                await PushToClientAsync(notification);
            }
        }

        private async Task<bool> IsNotificationEnabledAsync(int userId, NotificationType type)
        {
            var preference = await _context.NotificationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId && p.Type == type);

            return preference?.IsEnabled ?? true;
        }

        private async Task<Notification> PersistNotificationAsync(NotificationPayload payload)
        {
            var notification = new Notification
            {
                UserId = payload.UserId,
                Type = payload.Type,
                Title = payload.Title,
                Body = payload.Body,
                ReferenceId = payload.ReferenceId,
                ReferenceType = payload.ReferenceType
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Notifica inviata a utente {UserId}: {Type} - {Title}",
                payload.UserId, payload.Type, payload.Title);

            return notification;
        }

        /// <summary>
        /// Invia la notifica in tempo reale via SignalR al client connesso
        /// </summary>
        private async Task PushToClientAsync(Notification notification)
        {
            try
            {
                await _hubContext.Clients
                    .Group($"user_{notification.UserId}")
                    .SendAsync("ReceiveNotification", new
                    {
                        notification.Id,
                        type = notification.Type.ToString(),
                        notification.Title,
                        notification.Body,
                        notification.IsRead,
                        notification.ReferenceId,
                        notification.ReferenceType,
                        createdAt = notification.CreatedAt
                    });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Errore push SignalR per notifica {NotifId} a utente {UserId}",
                    notification.Id, notification.UserId);
            }
        }
    }
}
