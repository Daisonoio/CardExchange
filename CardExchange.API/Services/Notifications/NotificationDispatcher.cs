using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
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
        private readonly Dictionary<NotificationType, INotificationHandler> _handlerMap;

        public NotificationDispatcher(
            ApplicationDbContext context,
            ILogger<NotificationDispatcher> logger,
            IEnumerable<INotificationHandler> handlers)
        {
            _context = context;
            _logger = logger;
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

            await PersistNotificationAsync(payload);
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
                await PersistNotificationAsync(payload);
            }
        }

        private async Task<bool> IsNotificationEnabledAsync(int userId, NotificationType type)
        {
            var preference = await _context.NotificationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId && p.Type == type);

            // Default: abilitata se non esiste una preferenza esplicita
            return preference?.IsEnabled ?? true;
        }

        private async Task PersistNotificationAsync(NotificationPayload payload)
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
        }
    }
}
