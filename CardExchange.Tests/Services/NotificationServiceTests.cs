using CardExchange.API.Services;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardExchange.Tests.Services;

public class NotificationServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _service = new NotificationService(_context, new Mock<ILogger<NotificationService>>().Object);
    }

    [Fact]
    public async Task SendAsync_CreatesNotification()
    {
        // Arrange
        var user = new User
        {
            Email = "test@test.com",
            Username = "testuser",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "hash"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        await _service.SendAsync(user.Id, NotificationType.SystemAnnouncement, "Test Title", "Test Body");

        // Assert
        var notifications = await _context.Notifications.Where(n => n.UserId == user.Id).ToListAsync();
        notifications.Should().HaveCount(1);
        notifications[0].Title.Should().Be("Test Title");
        notifications[0].Body.Should().Be("Test Body");
        notifications[0].Type.Should().Be(NotificationType.SystemAnnouncement);
        notifications[0].IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task SendTradeOfferNotificationAsync_TradeOfferReceived_CorrectMessage()
    {
        var user = new User
        {
            Email = "test@test.com",
            Username = "testuser",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "hash"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await _service.SendTradeOfferNotificationAsync(user.Id, NotificationType.TradeOfferReceived, 1, "sender123");

        var notification = await _context.Notifications.FirstAsync(n => n.UserId == user.Id);
        notification.Title.Should().Be("Nuova offerta di scambio");
        notification.Body.Should().Contain("sender123");
        notification.ReferenceId.Should().Be(1);
        notification.ReferenceType.Should().Be("TradeOffer");
    }

    [Fact]
    public async Task SendWishlistMatchNotificationAsync_CorrectContent()
    {
        var user = new User
        {
            Email = "test@test.com",
            Username = "testuser",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "hash"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await _service.SendWishlistMatchNotificationAsync(user.Id, "Charizard", 42);

        var notification = await _context.Notifications.FirstAsync(n => n.UserId == user.Id);
        notification.Type.Should().Be(NotificationType.WishlistMatch);
        notification.Body.Should().Contain("Charizard");
        notification.ReferenceId.Should().Be(42);
        notification.ReferenceType.Should().Be("Card");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
