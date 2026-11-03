using CardExchange.API.Services;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardExchange.Tests.Services;

public class MeetupRemindersTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<INotificationService> _notifications = new();
    private readonly MeetupReminders _reminders;
    private readonly DateTime _now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
    private TradeOffer _offer = null!;

    public MeetupRemindersTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new ApplicationDbContext(options);
        _reminders = new MeetupReminders(_context, _notifications.Object, new Mock<ILogger<MeetupReminders>>().Object);
    }

    public void Dispose() => _context.Dispose();

    private async Task<TradeMeetup> SeedAsync(MeetupStatus status, DateTime scheduledAt, TradeOfferStatus offerStatus = TradeOfferStatus.Accepted)
    {
        var a = new User { Email = $"a{Guid.NewGuid():N}@t.it", Username = Guid.NewGuid().ToString("N")[..8], PasswordHash = "x" };
        var b = new User { Email = $"b{Guid.NewGuid():N}@t.it", Username = Guid.NewGuid().ToString("N")[..8], PasswordHash = "x" };
        _offer = new TradeOffer { Sender = a, Receiver = b, Status = offerStatus };
        var meetup = new TradeMeetup { TradeOffer = _offer, ProposedBy = a, ScheduledAt = scheduledAt, LocationName = "Negozio", Status = status };
        _context.TradeMeetups.Add(meetup);
        await _context.SaveChangesAsync();
        return meetup;
    }

    [Fact]
    public async Task SendsReminderOnceToBothParticipants_WithinTwentyFourHours()
    {
        var meetup = await SeedAsync(MeetupStatus.Accepted, _now.AddHours(20));

        (await _reminders.RunAsync(_now)).Reminders.Should().Be(1);
        (await _reminders.RunAsync(_now.AddMinutes(15))).Reminders.Should().Be(0, "il promemoria parte una sola volta");

        meetup.ReminderSentAt.Should().Be(_now);
        _notifications.Verify(n => n.SendAsync(It.IsAny<int>(), NotificationType.MeetupUpdate, "Promemoria appuntamento",
            It.IsAny<string?>(), _offer.Id, "TradeOffer"), Times.Exactly(2));
    }

    [Fact]
    public async Task DoesNotRemind_ProposedOrFarAwayMeetups()
    {
        await SeedAsync(MeetupStatus.Proposed, _now.AddHours(5));
        await SeedAsync(MeetupStatus.Accepted, _now.AddDays(3));

        (await _reminders.RunAsync(_now)).Reminders.Should().Be(0);
    }

    [Fact]
    public async Task AsksToConfirmTrade_AfterMeetup_OnlyIfTradeStillOpen()
    {
        var open = await SeedAsync(MeetupStatus.Accepted, _now.AddHours(-2));
        var completed = await SeedAsync(MeetupStatus.Accepted, _now.AddHours(-2), TradeOfferStatus.Completed);
        await SeedAsync(MeetupStatus.Accepted, _now.AddMinutes(-30)); // troppo presto

        var (_, followUps) = await _reminders.RunAsync(_now);

        followUps.Should().Be(1);
        open.FollowUpSentAt.Should().Be(_now);
        completed.FollowUpSentAt.Should().BeNull();
    }
}
