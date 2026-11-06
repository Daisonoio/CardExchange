using CardExchange.Core.Entities;

namespace CardExchange.API.Services.Notifications.Handlers
{
    public class SystemNotificationHandler : INotificationHandler
    {
        public IReadOnlyList<NotificationType> SupportedTypes { get; } = new[]
        {
            NotificationType.SystemAnnouncement,
            NotificationType.NewReview,
            NotificationType.SubscriptionExpiring,
            NotificationType.SubscriptionExpired,
            NotificationType.PriceSpike,
            NotificationType.MeetupUpdate,
            // Notifiche con titolo e testo già pronti: senza questa voce il dispatcher le scartava
            NotificationType.WeeklyDigest,
            NotificationType.FollowedNearby,
            NotificationType.NewFollower,
            NotificationType.StoreActivity,
            NotificationType.EventUpdate,
            NotificationType.StoreOffer,
            NotificationType.Loyalty
        };

        public NotificationPayload BuildPayload(NotificationType type, Dictionary<string, object> parameters)
        {
            var userId = Convert.ToInt32(parameters["UserId"]);
            var title = parameters.TryGetValue("Title", out var t) ? t.ToString()! : "Notifica di sistema";
            var body = parameters.TryGetValue("Body", out var b) ? b.ToString() : null;
            var referenceId = parameters.TryGetValue("ReferenceId", out var rid) ? Convert.ToInt32(rid) : (int?)null;
            var referenceType = parameters.TryGetValue("ReferenceType", out var rt) ? rt.ToString() : null;

            return new NotificationPayload
            {
                UserId = userId,
                Type = type,
                Title = title,
                Body = body,
                ReferenceId = referenceId,
                ReferenceType = referenceType
            };
        }
    }
}
