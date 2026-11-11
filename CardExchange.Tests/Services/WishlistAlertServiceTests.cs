using CardExchange.API.Services;
using CardExchange.API.Services.Moderation;
using CardExchange.API.Services.Notifications;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardExchange.Tests.Services;

public class WishlistAlertServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<INotificationDispatcher> _dispatcher = new();
    private readonly WishlistAlertService _service;
    private readonly List<Dictionary<string, object>> _sent = new();

    // Milano centro e due punti a ~5 km e ~200 km
    private const decimal MilanLat = 45.4642m, MilanLon = 9.1900m;

    public WishlistAlertServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new ApplicationDbContext(options);
        _dispatcher
            .Setup(d => d.DispatchAsync(NotificationType.WishlistMatch, It.IsAny<Dictionary<string, object>>()))
            .Callback<NotificationType, Dictionary<string, object>>((_, p) =>
            {
                _sent.Add(p);
                // Simula la scrittura della notifica, come fa il dispatcher reale
                _context.Notifications.Add(new Notification
                {
                    UserId = (int)p["UserId"], Type = NotificationType.WishlistMatch, Title = "x",
                    ReferenceId = (int)p["CardId"], ReferenceType = "Card"
                });
                _context.SaveChanges();
            })
            .Returns(Task.CompletedTask);
        _service = new WishlistAlertService(_context, _dispatcher.Object, new UserBlockService(_context), new Mock<ILogger<WishlistAlertService>>().Object,
            TestEntitlements.Paid(_context));
    }

    public void Dispose() => _context.Dispose();

    private User NewUser(string name, decimal? lat, decimal? lon, int radius = 50)
    {
        var u = new User { Email = $"{name}@t.it", Username = name, PasswordHash = "x" };
        if (lat.HasValue) u.Location = new UserLocation { City = "Milano", Country = "IT", Latitude = lat, Longitude = lon, MaxDistanceKm = radius };
        _context.Users.Add(u);
        return u;
    }

    private async Task<(User owner, User wisher, Card card)> SeedAsync(decimal? wisherLat = MilanLat + 0.045m, int radius = 50,
        CardCondition wantedCondition = CardCondition.NearMint, bool premium = true)
    {
        var owner = NewUser("owner", MilanLat, MilanLon);
        var wisher = NewUser("wisher", wisherLat, wisherLat.HasValue ? MilanLon : null, radius);
        var set = new CardSet { Name = "Set", Code = "SET", Game = new Game { Name = "Magic" } };
        var info = new CardInfo { Name = "Lightning Bolt", CardSet = set };
        var card = new Card { User = owner, CardInfo = info, Condition = CardCondition.NearMint, IsAvailableForTrade = true };
        _context.Cards.Add(card);
        _context.WishlistItems.Add(new WishlistItem { User = wisher, CardInfo = info, PreferredCondition = wantedCondition });
        await _context.SaveChangesAsync();
        // Avvisi immediati: solo con il Premium (gli altri ricevono il riepilogo delle 18)
        if (premium)
        {
            _context.UserSubscriptions.Add(new UserSubscription { UserId = wisher.Id, Plan = CardExchange.Core.Rules.PlanCatalog.All().First(p => p.Name == "Premium Mensile"),
                Status = SubscriptionStatus.Active, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(30) });
            await _context.SaveChangesAsync();
        }
        return (owner, wisher, card);
    }

    [Fact]
    public async Task NotifiesNearbyWisher_WithDistanceInMessage()
    {
        var (_, wisher, card) = await SeedAsync();

        await _service.NotifyAvailableAsync(new[] { card.Id });

        _sent.Should().ContainSingle();
        _sent[0]["UserId"].Should().Be(wisher.Id);
        ((string)_sent[0]["Body"]).Should().Contain("a 5 km");
    }

    [Fact]
    public async Task SameCardIsNotifiedOnlyOnce()
    {
        var (_, _, card) = await SeedAsync();
        await _service.NotifyAvailableAsync(new[] { card.Id });
        await _service.NotifyAvailableAsync(new[] { card.Id });
        _sent.Should().HaveCount(1);
    }

    [Fact]
    public async Task SkipsWisherOutsideRadius()
    {
        var (_, _, card) = await SeedAsync(wisherLat: MilanLat + 1.8m); // ~200 km
        await _service.NotifyAvailableAsync(new[] { card.Id });
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SkipsWisherWithoutLocation()
    {
        var (_, _, card) = await SeedAsync(wisherLat: null);
        await _service.NotifyAvailableAsync(new[] { card.Id });
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SkipsWhenCardDoesNotSatisfyWishlist()
    {
        var (_, _, card) = await SeedAsync(wantedCondition: CardCondition.Mint);
        await _service.NotifyAvailableAsync(new[] { card.Id });
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SkipsBlockedUsers()
    {
        var (owner, wisher, card) = await SeedAsync();
        _context.UserBlocks.Add(new UserBlock { BlockerId = wisher.Id, BlockedUserId = owner.Id });
        await _context.SaveChangesAsync();

        await _service.NotifyAvailableAsync(new[] { card.Id });

        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task RespectsDailyLimitPerUser()
    {
        var (owner, wisher, card) = await SeedAsync();
        var extraCards = new List<Card>();
        for (int i = 0; i < WishlistAlertService.DailyLimitPerUser + 5; i++)
            extraCards.Add(new Card { UserId = owner.Id, CardInfoId = card.CardInfoId, Condition = CardCondition.Mint, IsAvailableForTrade = true });
        _context.Cards.AddRange(extraCards);
        await _context.SaveChangesAsync();

        await _service.NotifyAvailableAsync(extraCards.Select(c => c.Id).Append(card.Id));

        _sent.Should().HaveCount(WishlistAlertService.DailyLimitPerUser);
        _sent.Should().OnlyContain(p => (int)p["UserId"] == wisher.Id);
    }

    [Fact]
    public async Task FreeUser_GetsNoInstantAlert_ButAnEveningDigest()
    {
        var (_, wisher, card) = await SeedAsync(premium: false);
        wisher.TimeZone = "Europe/Rome";
        await _context.SaveChangesAsync();

        await _service.NotifyAvailableAsync(new[] { card.Id });
        await _service.NotifyAvailableAsync(new[] { card.Id });

        _sent.Should().BeEmpty();
        (await _context.PendingWishlistAlerts.CountAsync()).Should().Be(1, "lo stesso avviso non si accoda due volte");

        var payloads = new List<NotificationPayload>();
        _dispatcher.Setup(d => d.DeliverManyAsync(It.IsAny<IReadOnlyList<NotificationPayload>>()))
            .Callback<IReadOnlyList<NotificationPayload>>(p => payloads.AddRange(p)).ReturnsAsync(1);
        var digest = new WishlistDigest(_context, _dispatcher.Object, new Mock<ILogger<WishlistDigest>>().Object);
        var created = (await _context.PendingWishlistAlerts.SingleAsync()).CreatedAt;
        var rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
        var local = TimeZoneInfo.ConvertTimeFromUtc(created, rome);
        var sixPm = TimeZoneInfo.ConvertTimeToUtc(local.Date.AddHours(local.Hour >= 18 ? 42 : 18), rome);

        (await digest.RunAsync(sixPm.AddMinutes(-30))).Should().Be(0, "prima delle 18 non parte");
        card.IsAvailableForTrade = false;   // nel frattempo l'ha presa qualcun altro
        await _context.SaveChangesAsync();
        (await digest.RunAsync(sixPm.AddMinutes(5))).Should().Be(1);
        (await digest.RunAsync(sixPm.AddMinutes(20))).Should().Be(0, "un solo riepilogo per gli stessi avvisi");

        payloads.Should().ContainSingle();
        payloads[0].Body.Should().Contain("Lightning Bolt").And.Contain("già stata presa").And.NotContain("Premium", "con i piani spenti non si cita il Premium");
    }
}
