using CardExchange.API.Services.Moderation;
using CardExchange.API.Services.Notifications;
using CardExchange.API.Services.Stores;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CardExchange.Tests.Services;

public class StoreDemandTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Mock<IUserBlockService> _blocks = new();
    private readonly Mock<INotificationDispatcher> _dispatcher = new();
    private readonly DateTime _now = DateTime.UtcNow;
    private Store _store = null!;
    private CardInfo _charizard = null!, _pikachu = null!, _mew = null!, _rare = null!;

    public StoreDemandTests()
    {
        _blocks.Setup(b => b.GetBlockedUserIdsAsync(It.IsAny<int>())).ReturnsAsync(new HashSet<int>());
        _dispatcher.Setup(d => d.DeliverManyAsync(It.IsAny<IReadOnlyList<NotificationPayload>>())).ReturnsAsync((IReadOnlyList<NotificationPayload> p) => p.Count);
    }

    public void Dispose() => _db.Dispose();

    private StoreDemandService Service => new(_db, _blocks.Object);

    private User AddUser(string name, decimal lat = 45.4642m, decimal lon = 9.19m, int? age = 30, bool active = true)
    {
        var u = new User
        {
            Email = $"{name}@t.it", Username = name, PasswordHash = "x", LastLoginAt = active ? _now.AddDays(-3) : _now.AddDays(-200),
            BirthDate = age.HasValue ? DateOnly.FromDateTime(_now).AddYears(-age.Value) : null,
            Location = new UserLocation { City = "Milano", Province = "MI", Country = "IT", Latitude = lat, Longitude = lon }
        };
        _db.Users.Add(u);
        return u;
    }

    private async Task SeedAsync()
    {
        var set = new CardSet { Name = "Base", Code = "BS", Game = new Game { Name = "Pokémon" } };
        _charizard = new CardInfo { Name = "Charizard", CardSet = set, PriceEur = 300 };
        _pikachu = new CardInfo { Name = "Pikachu", CardSet = set, PriceEur = 5 };
        _mew = new CardInfo { Name = "Mew", CardSet = set, PriceEur = 40 };
        _rare = new CardInfo { Name = "Rara", CardSet = set, PriceEur = 1 };
        _db.CardInfos.AddRange(_charizard, _pikachu, _mew, _rare);
        var owner = AddUser("owner");
        await _db.SaveChangesAsync();
        _store = new Store { Name = "Games Corner", City = "Milano", Address = "x", OwnerId = owner.Id, Status = StoreStatus.Verified, Latitude = 45.4642m, Longitude = 9.19m };
        _db.Stores.Add(_store);
        _db.Cards.Add(new Card { UserId = owner.Id, CardInfoId = _charizard.Id, Condition = CardCondition.NearMint, Quantity = 2, IsAvailableForTrade = true });
        _db.Cards.Add(new Card { UserId = owner.Id, CardInfoId = _pikachu.Id, Condition = CardCondition.HeavilyPlayed, IsAvailableForTrade = true });
        await _db.SaveChangesAsync();
    }

    private void Wish(User u, CardInfo c, CardCondition? cond = null, decimal? max = null) =>
        _db.WishlistItems.Add(new WishlistItem { UserId = u.Id, CardInfoId = c.Id, PreferredCondition = cond, MaxPrice = max });

    [Fact]
    public async Task InStockAndMissing_OnlyAboveThreshold_WithinRadius_ActiveOnly()
    {
        await SeedAsync();
        var a = AddUser("a"); var b = AddUser("b"); var c = AddUser("c"); var d = AddUser("d");
        var far = AddUser("far", 41.9m, 12.5m);
        var idle = AddUser("idle", active: false);
        await _db.SaveChangesAsync();
        foreach (var u in new[] { a, b, c }) Wish(u, _charizard, max: 350);
        foreach (var u in new[] { a, b, c, d }) Wish(u, _mew);                       // mancante, 4 persone
        foreach (var u in new[] { a, b, c }) Wish(u, _pikachu, CardCondition.NearMint); // copia troppo rovinata: mancante
        Wish(a, _rare); Wish(b, _rare);                                             // sotto soglia
        Wish(far, _charizard); Wish(idle, _charizard);
        await _db.SaveChangesAsync();

        var demand = await Service.ComputeAsync(_store, _now);

        demand.ActiveUsersInRadius.Should().Be(4);
        demand.InterestedUsers.Should().Be(3);
        demand.InStock.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Name = "Charizard", Users = 3, AvgMaxPriceEur = 350m, CopiesInStock = 2 });
        demand.Missing.Select(m => (m.Name, m.Users)).Should().Equal(("Mew", 4), ("Pikachu", 3));
        demand.Missing.Should().NotContain(m => m.Name == "Rara");
    }

    [Fact]
    public async Task BlockedUsers_AreExcluded()
    {
        await SeedAsync();
        var users = Enumerable.Range(0, 3).Select(i => AddUser($"u{i}")).ToList();
        await _db.SaveChangesAsync();
        users.ForEach(u => Wish(u, _charizard));
        await _db.SaveChangesAsync();
        _blocks.Setup(b => b.GetBlockedUserIdsAsync(_store.OwnerId)).ReturnsAsync(new HashSet<int> { users[0].Id });

        (await Service.ComputeAsync(_store, _now)).InStock.Should().BeEmpty("restano 2 persone, sotto la soglia");
    }

    [Fact]
    public async Task NotifyInterested_SkipsMinorsAndUnknownAge_AndRespectsWeeklyLimit()
    {
        await SeedAsync();
        var adults = Enumerable.Range(0, 3).Select(i => AddUser($"adult{i}")).ToList();
        var minor = AddUser("minor", age: 15);
        var unknown = AddUser("unknown", age: null);
        await _db.SaveChangesAsync();
        adults.Concat(new[] { minor, unknown }).ToList().ForEach(u => Wish(u, _charizard));
        await _db.SaveChangesAsync();

        var first = await Service.NotifyInterestedAsync(_store, _charizard.Id, _now, _dispatcher.Object);
        first.Should().Be(new StockAlertResult(3, 0, null));
        _dispatcher.Verify(d => d.DeliverManyAsync(It.Is<IReadOnlyList<NotificationPayload>>(p =>
            p.Count == 3 && p.All(x => x.Type == NotificationType.StoreOffer && !new[] { minor.Id, unknown.Id }.Contains(x.UserId)))), Times.Once);

        var again = await Service.NotifyInterestedAsync(_store, _charizard.Id, _now.AddHours(1), _dispatcher.Object);
        again.Should().Be(new StockAlertResult(0, 3, null));

        var missing = await Service.NotifyInterestedAsync(_store, _mew.Id, _now, _dispatcher.Object);
        missing.Error.Should().Contain("non è tra quelle disponibili");
    }
}
