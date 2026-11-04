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

        /// <summary>
        /// Recapita notifiche già composte (testo diverso per ogni destinatario) rispettando le preferenze:
        /// un solo salvataggio sul database, poi tempo reale e push. Restituisce quante sono state inviate.
        /// </summary>
        Task<int> DeliverManyAsync(IReadOnlyList<NotificationPayload> payloads);
    }

    public class NotificationDispatcher : INotificationDispatcher
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificationDispatcher> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly Push.IPushQueue _pushQueue;
        private readonly Dictionary<NotificationType, INotificationHandler> _handlerMap;

        public NotificationDispatcher(
            ApplicationDbContext context,
            ILogger<NotificationDispatcher> logger,
            IHubContext<NotificationHub> hubContext,
            IEnumerable<INotificationHandler> handlers,
            Push.IPushQueue pushQueue)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
            _pushQueue = pushQueue;
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

        public async Task<int> DeliverManyAsync(IReadOnlyList<NotificationPayload> payloads)
        {
            if (payloads.Count == 0) return 0;
            var userIds = payloads.Select(p => p.UserId).Distinct().ToList();
            var types = payloads.Select(p => p.Type).Distinct().ToList();
            var disabled = (await _context.NotificationPreferences
                    .AsNoTracking()
                    .Where(p => userIds.Contains(p.UserId) && types.Contains(p.Type) && !p.IsEnabled)
                    .Select(p => new { p.UserId, p.Type })
                    .ToListAsync())
                .Select(p => (p.UserId, p.Type))
                .ToHashSet();

            var notifications = payloads
                .Where(p => !disabled.Contains((p.UserId, p.Type)))
                .Select(p => new Notification
                {
                    UserId = p.UserId,
                    Type = p.Type,
                    Title = p.Title.Length > 200 ? p.Title[..200] : p.Title,
                    Body = p.Body,
                    ReferenceId = p.ReferenceId,
                    ReferenceType = p.ReferenceType
                })
                .ToList();
            if (notifications.Count == 0) return 0;

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
            foreach (var n in notifications) await PushToClientAsync(n);
            return notifications.Count;
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
            // Notifica push sui dispositivi registrati (inviata in background)
            _pushQueue.Enqueue(new Push.PushJob(
                notification.UserId,
                notification.Title,
                notification.Body,
                Push.PushQueue.UrlFor(notification.ReferenceType, notification.ReferenceId),
                Tag: $"{notification.Type}-{notification.ReferenceId}"));

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
