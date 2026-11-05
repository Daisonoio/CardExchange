using CardExchange.API.Services;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardExchange.Tests.Services;

public class MeetupSpotsTests
{
    [Fact]
    public void Partners_ComeFirst_ThenDistanceFromMidpoint_Within30Km()
    {
        // Punto medio tra Duomo e Navigli
        var (lat, lon) = MeetupSpots.Midpoint(45.4642, 9.1900, 45.4520, 9.1750);
        var ranked = MeetupSpots.Rank(new[]
        {
            new MeetupSpots.Candidate(1, 45.4580, 9.1825, IsPartner: false),  // sul punto medio
            new MeetupSpots.Candidate(2, 45.4800, 9.2000, IsPartner: true),   // ~3 km
            new MeetupSpots.Candidate(3, 45.4700, 9.1900, IsPartner: false),  // ~1.5 km
            new MeetupSpots.Candidate(4, 45.0703, 7.6869, IsPartner: true),   // Torino: fuori raggio
        }, lat, lon);

        ranked.Select(r => r.StoreId).Should().Equal(2, 1, 3);
    }

    [Fact]
    public void AtMostFiveSuggestions() =>
        MeetupSpots.Rank(Enumerable.Range(1, 9).Select(i => new MeetupSpots.Candidate(i, 45.46, 9.19, false)), 45.46, 9.19)
            .Should().HaveCount(MeetupSpots.MaxSuggestions);
}

public class StoreMeetupNotificationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<INotificationService> _notifications = new();
    private readonly MeetupReminders _reminders;
    private readonly Store _store;
    // 4 novembre 2026, 9:00 in Italia (8:00 UTC)
    private readonly DateTime _morning = new(2026, 11, 4, 8, 0, 0, DateTimeKind.Utc);

    public StoreMeetupNotificationTests()
    {
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _reminders = new MeetupReminders(_context, _notifications.Object, new Mock<ILogger<MeetupReminders>>().Object);
        var owner = new User { Email = "o@t.it", Username = "owner", PasswordHash = "x" };
        _store = new Store { Owner = owner, Name = "Games Corner", Address = "Via Torino 12", City = "Milano", Status = StoreStatus.Verified };
        _context.Stores.Add(_store);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private void Meetup(DateTime atUtc, MeetupStatus status = MeetupStatus.Accepted, bool atStore = true)
    {
        var a = new User { Email = $"{Guid.NewGuid():N}@t.it", Username = Guid.NewGuid().ToString("N")[..8], PasswordHash = "x" };
        var b = new User { Email = $"{Guid.NewGuid():N}@t.it", Username = Guid.NewGuid().ToString("N")[..8], PasswordHash = "x" };
        _context.TradeMeetups.Add(new TradeMeetup
        {
            TradeOffer = new TradeOffer { Sender = a, Receiver = b, Status = TradeOfferStatus.Accepted },
            ProposedBy = a, ScheduledAt = atUtc, LocationName = "Games Corner", Status = status, Store = atStore ? _store : null
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task OneDigestPerStore_ForTodaysAcceptedMeetups_Once()
    {
        Meetup(_morning.AddHours(7));                 // oggi 16:00
        Meetup(_morning.AddHours(9));                 // oggi 18:00
        Meetup(_morning.AddDays(1));                  // domani: escluso
        Meetup(_morning.AddHours(5), MeetupStatus.Proposed); // non confermato: escluso
        Meetup(_morning.AddHours(6), atStore: false); // non in negozio: escluso

        (await _reminders.NotifyStoresAsync(_morning)).Should().Be(2);
        await _context.SaveChangesAsync();
        _notifications.Verify(n => n.SendAsync(_store.OwnerId, NotificationType.StoreActivity, "Oggi da te ci sono 2 scambi",
            It.Is<string>(b => b.Contains("4 persone") && b.Contains("16:00")), _store.Id, "Store"), Times.Once);

        (await _reminders.NotifyStoresAsync(_morning.AddMinutes(15))).Should().Be(0, "ogni incontro si segnala una volta sola");
    }

    [Fact]
    public async Task NothingBeforeEightInTheMorning()
    {
        Meetup(_morning.AddHours(7));
        (await _reminders.NotifyStoresAsync(_morning.AddHours(-2))).Should().Be(0); // 7:00 in Italia
        _notifications.VerifyNoOtherCalls();
    }
}
