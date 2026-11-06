using CardExchange.API.Services.Notifications;
using CardExchange.API.Services.Notifications.Handlers;
using CardExchange.Core.Entities;
using FluentAssertions;

namespace CardExchange.Tests.Services;

/// <summary>Ogni tipo di notifica deve avere un handler: senza, il dispatcher la scarta in silenzio.</summary>
public class NotificationHandlerCoverageTests
{
    [Fact]
    public void EveryNotificationType_HasAHandler()
    {
        var handlers = new INotificationHandler[]
        {
            new TradeNotificationHandler(), new WishlistNotificationHandler(), new FavoriteNotificationHandler(),
            new MessageNotificationHandler(), new SystemNotificationHandler()
        };
        var handled = handlers.SelectMany(h => h.SupportedTypes).ToHashSet();
        Enum.GetValues<NotificationType>().Where(t => !handled.Contains(t)).Should().BeEmpty();
    }
}
