using CardExchange.API.Services.Moderation;
using CardExchange.API.Services.Notifications;
using CardExchange.API.Services.Social;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardExchange.Tests.Services;

public class BroadcastServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly HashSet<int> _blocked = new();
    private readonly List<NotificationPayload> _sent = new();
    private readonly BroadcastService _service;
    private readonly User _seller;

    // Milano Duomo e dintorni
    private const decimal Lat = 45.4642m, Lon = 9.1900m;

    public BroadcastServiceTests()
    {
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _seller = AddUser("seller", Lat, Lon);
        _context.SaveChanges();

        var blocks = new Mock<IUserBlockService>();
        blocks.Setup(b => b.GetBlockedUserIdsAsync(It.IsAny<int>())).ReturnsAsync(() => _blocked);
        var dispatcher = new Mock<INotificationDispatcher>();
        dispatcher.Setup(d => d.DeliverManyAsync(It.IsAny<IReadOnlyList<NotificationPayload>>()))
            .ReturnsAsync((IReadOnlyList<NotificationPayload> p) => { _sent.AddRange(p); return p.Count; });
        _service = new BroadcastService(_context, dispatcher.Object, blocks.Object, new Mock<ILogger<BroadcastService>>().Object);
    }

    public void Dispose() => _context.Dispose();

    private User AddUser(string name, decimal? lat, decimal? lon, bool active = true)
    {
        var u = new User { Username = name, Email = $"{name}@t.it", FirstName = name, PasswordHash = "x", IsActive = active };
        if (lat.HasValue) u.Location = new UserLocation { City = "X", Province = "X", Country = "IT", Latitude = lat, Longitude = lon, MaxDistanceKm = 50 };
        _context.Users.Add(u);
        return u;
    }

    private void Follow(User follower, User? user = null, Store? store = null) =>
        _context.Follows.Add(new Follow { Follower = follower, FollowedUser = user, FollowedStore = store });

    private Store AddStore(User owner, StoreStatus status = StoreStatus.Verified)
    {
        var s = new Store { Owner = owner, Name = "Carte & Co", Address = "Via Roma 1", City = "Milano", Latitude = 45.4700m, Longitude = 9.2000m, Status = status };
        _context.Stores.Add(s);
        return s;
    }

    private static CreateBroadcastRequest Here(int minutes = 240) => new()
    {
        PlaceName = "Mercatino di Porta Genova",
        SpotDetail = "Banco 12",
        Latitude = Lat,
        Longitude = Lon,
        DurationMinutes = minutes
    };

    [Fact]
    public async Task Followers_GetTheirOwnDistance_BlockedAndInactiveAreSkipped()
    {
        var near = AddUser("near", 45.4642m, 9.19m);       // stesso punto -> 1 km (minimo)
        var far = AddUser("far", 45.0703m, 7.6869m);       // Torino, ~126 km
        var noLocation = AddUser("noloc", null, null);
        var blocked = AddUser("blocked", Lat, Lon);
        var inactive = AddUser("inactive", Lat, Lon, active: false);
        foreach (var u in new[] { near, far, noLocation, blocked, inactive }) Follow(u, user: _seller);
        _context.SaveChanges();
        _blocked.Add(blocked.Id);

        var result = await _service.CreateAsync(_seller.Id, Here());

        result.Error.Should().BeNull();
        result.Broadcast!.RecipientCount.Should().Be(3);
        _sent.Select(p => p.UserId).Should().BeEquivalentTo(new[] { near.Id, far.Id, noLocation.Id });
        _sent.Should().OnlyContain(p => p.Type == NotificationType.FollowedNearby && p.ReferenceType == "Broadcast" && p.ReferenceId == result.Broadcast.Id);
        _sent.Single(p => p.UserId == near.Id).Title.Should().Be("@seller è a 1 km da te");
        _sent.Single(p => p.UserId == far.Id).Title.Should().MatchRegex(@"^@seller è a 12\d km da te$");
        _sent.Single(p => p.UserId == noLocation.Id).Title.Should().Be("@seller ti dice dov'è");
        _sent[0].Body.Should().StartWith("Mercatino di Porta Genova · Banco 12 · fino alle ");
    }

    [Fact]
    public async Task AsStore_NotifiesStoreAndOwnerFollowers_OnceEach_UsingStorePosition()
    {
        var store = AddStore(_seller);
        var both = AddUser("both", Lat, Lon);
        var storeOnly = AddUser("storeonly", Lat, Lon);
        Follow(both, user: _seller);
        Follow(both, store: store);
        Follow(storeOnly, store: store);
        _context.SaveChanges();

        var request = Here();
        request.StoreId = store.Id;
        request.Latitude = request.Longitude = null;
        var result = await _service.CreateAsync(_seller.Id, request);

        result.Broadcast!.Latitude.Should().Be(45.47m);
        _sent.Select(p => p.UserId).Should().BeEquivalentTo(new[] { both.Id, storeOnly.Id });
        _sent.Should().OnlyContain(p => p.Title.StartsWith("Carte & Co è a"));
    }

    [Fact]
    public async Task OnlyOwnVerifiedStores()
    {
        var other = AddUser("other", Lat, Lon);
        var notMine = AddStore(other);
        var pending = AddStore(_seller, StoreStatus.Pending);
        _context.SaveChanges();

        var r1 = Here(); r1.StoreId = notMine.Id;
        var r2 = Here(); r2.StoreId = pending.Id;

        (await _service.CreateAsync(_seller.Id, r1)).StatusCode.Should().Be(403);
        (await _service.CreateAsync(_seller.Id, r2)).StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task NeedsAPlace_AndValidCoordinates()
    {
        var noCoords = Here(); noCoords.Latitude = noCoords.Longitude = null;
        var zero = Here(); zero.Latitude = 0; zero.Longitude = 0;
        var noName = Here(); noName.PlaceName = " ";

        (await _service.CreateAsync(_seller.Id, noCoords)).Error.Should().Contain("Indica dove ti trovi");
        (await _service.CreateAsync(_seller.Id, zero)).Error.Should().Contain("Indica dove ti trovi");
        (await _service.CreateAsync(_seller.Id, noName)).Error.Should().Contain("nome del posto");
        _context.LocationBroadcasts.Should().BeEmpty();
    }

    [Fact]
    public async Task NewBroadcast_EndsThePreviousOne_AndDailyLimitApplies()
    {
        var first = (await _service.CreateAsync(_seller.Id, Here())).Broadcast!;
        var second = (await _service.CreateAsync(_seller.Id, Here())).Broadcast!;

        (await _context.LocationBroadcasts.FindAsync(first.Id))!.EndedAt.Should().NotBeNull();
        second.EndedAt.Should().BeNull();

        (await _service.CreateAsync(_seller.Id, Here())).Error.Should().BeNull();
        var fourth = await _service.CreateAsync(_seller.Id, Here());
        fourth.StatusCode.Should().Be(429);
        fourth.Broadcast.Should().BeNull();
    }

    [Fact]
    public async Task Duration_IsClamped()
    {
        var shortOne = (await _service.CreateAsync(_seller.Id, Here(minutes: 5))).Broadcast!;
        (shortOne.EndsAt - shortOne.StartsAt).Should().Be(BroadcastRules.MinDuration);

        var longOne = (await _service.CreateAsync(_seller.Id, Here(minutes: 60 * 48))).Broadcast!;
        (longOne.EndsAt - longOne.StartsAt).Should().Be(BroadcastRules.MaxDuration);
    }
}

public class BroadcastRulesTests
{
    [Theory]
    [InlineData(0.2, 1)]
    [InlineData(14.6, 15)]
    [InlineData(null, null)]
    public void DisplayDistance_WholeKm_AtLeastOne(double? km, int? expected) =>
        BroadcastRules.DisplayDistanceKm(km).Should().Be(expected);

    [Fact]
    public void Body_UsesItalianTime_AndTruncatesLongMessages()
    {
        // 16:00 UTC di luglio = 18:00 in Italia (ora legale)
        var body = BroadcastRules.Body("Fiera di Milano", "Pad. 3, stand B12", new DateTime(2026, 7, 5, 16, 0, 0, DateTimeKind.Utc), new string('x', 150));
        body.Should().StartWith("Fiera di Milano · Pad. 3, stand B12 · fino alle 18:00 — ");
        body.Should().EndWith("…");
    }

    [Theory]
    [InlineData(45.0, 9.0, true)]
    [InlineData(0, 0, false)]
    [InlineData(91, 9, false)]
    [InlineData(45, 181, false)]
    public void Coordinates(double lat, double lon, bool ok) =>
        BroadcastRules.IsValidCoordinate((decimal)lat, (decimal)lon).Should().Be(ok);
}
