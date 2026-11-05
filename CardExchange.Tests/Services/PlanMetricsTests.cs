using CardExchange.API.Services;
using CardExchange.API.Services.Metrics;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CardExchange.Tests.Services;

public class PlanMetricsTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly DateTime _now = new(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);

    public void Dispose() => _db.Dispose();

    private readonly Dictionary<User, DateTime> _created = new();

    /// <summary>Il contesto imposta CreatedAt al salvataggio: la data voluta si riapplica dopo.</summary>
    private async Task SaveUsersAsync()
    {
        await _db.SaveChangesAsync();
        foreach (var (u, at) in _created) u.CreatedAt = at;
        await _db.SaveChangesAsync();
    }

    private User AddUser(string name, string city, int createdDaysAgo, bool? match = null, DateTime? lastLogin = null)
    {
        var u = new User
        {
            Email = $"{name}@t.it", Username = name, PasswordHash = "x", CreatedAt = _now.AddDays(-createdDaysAgo),
            MatchWithin48h = match, LastLoginAt = lastLogin,
            Location = new UserLocation { City = city, Province = "X", Country = "IT" }
        };
        _db.Users.Add(u);
        _created[u] = u.CreatedAt;
        return u;
    }

    [Fact]
    public async Task ComputesPlanMetrics_ByCity()
    {
        var a = AddUser("a", "Milano", 5, match: true);
        var b = AddUser("b", "milano", 10, match: false);
        var c = AddUser("c", "Milano", 40);                       // nella coorte del ritorno: tornato
        var d = AddUser("d", "Milano", 45);                       // nella coorte: non tornato
        var r = AddUser("r", "Roma", 3, match: true);
        await SaveUsersAsync();
        _db.UserActiveDays.AddRange(
            new UserActiveDay { UserId = a.Id, Day = PlanMetrics.ItalianDay(_now.AddDays(-1)) },
            new UserActiveDay { UserId = a.Id, Day = PlanMetrics.ItalianDay(_now.AddDays(-2)) },
            new UserActiveDay { UserId = c.Id, Day = PlanMetrics.ItalianDay(_now.AddDays(-3)) },
            new UserActiveDay { UserId = d.Id, Day = PlanMetrics.ItalianDay(_now.AddDays(-44)) },
            new UserActiveDay { UserId = r.Id, Day = PlanMetrics.ItalianDay(_now.AddDays(-1)) });
        _db.TradeOffers.Add(new TradeOffer { SenderId = a.Id, ReceiverId = b.Id, Status = TradeOfferStatus.Completed, CompletedDate = _now.AddDays(-2) });
        _db.TradeOffers.Add(new TradeOffer { SenderId = r.Id, ReceiverId = r.Id, Status = TradeOfferStatus.Completed, CompletedDate = _now.AddDays(-2) });
        var broadcast = new LocationBroadcast { AuthorId = a.Id, PlaceName = "Fiera" };
        _db.LocationBroadcasts.Add(broadcast);
        _db.Stores.Add(new Store { Name = "Attivo", City = "Milano", Address = "x", OwnerId = c.Id, Status = StoreStatus.Verified });
        _db.Stores.Add(new Store { Name = "Fermo", City = "Milano", Address = "x", OwnerId = d.Id, Status = StoreStatus.Verified });
        _db.Cards.Add(new Card { UserId = c.Id, CardInfoId = 1, Quantity = 60, IsAvailableForTrade = true });
        await _db.SaveChangesAsync();
        broadcast.CreatedAt = _now.AddDays(-1);
        await _db.SaveChangesAsync();

        var m = await new PlanMetrics(_db).ComputeAsync("Milano", _now);

        m.TotalUsers.Should().Be(4, "la città non distingue maiuscole");
        m.MatchWithin48h.Should().Be(new RateMetric(2, 1, 0.5));
        m.WeeklyActiveUsers.Should().Be(2);
        m.TradesCompletedLastWeek.Should().Be(1);
        m.BroadcastsLastWeek.Should().Be(1);
        m.RetentionAfter4Weeks.Should().Be(new RateMetric(2, 1, 0.5));
        m.ActiveStores.Should().Be(1);
        m.Weeks.Should().HaveCount(8);
        m.Cities.First().Should().Be(new CityUsers("Milano", 4));

        var all = await new PlanMetrics(_db).ComputeAsync(null, _now);
        all.TotalUsers.Should().Be(5);
        all.WeeklyActiveUsers.Should().Be(3);
        all.TradesCompletedLastWeek.Should().Be(2);
    }

    [Fact]
    public async Task ActivationCheck_SavesMatchAfter48Hours_Once()
    {
        var fresh = AddUser("fresh", "Milano", 1);
        var due = AddUser("due", "Milano", 2);
        _created[due] = _now.AddHours(-49);
        var empty = AddUser("empty", "Milano", 2);
        _created[empty] = _now.AddHours(-50);
        var old = AddUser("old", "Milano", 30);
        await SaveUsersAsync();
        _db.WishlistItems.Add(new WishlistItem { UserId = due.Id, CardInfoId = 1 });
        await _db.SaveChangesAsync();
        var matchmaking = new Mock<IMatchmakingService>();
        matchmaking.Setup(m => m.FindMatchesAsync(due.Id, null, null, null, null, null)).ReturnsAsync(new[] { new MatchResult { UserId = 9 } });

        (await new ActivationCheck(_db, matchmaking.Object).RunAsync(_now)).Should().Be(2);
        (await new ActivationCheck(_db, matchmaking.Object).RunAsync(_now.AddMinutes(15))).Should().Be(0);

        due.MatchWithin48h.Should().BeTrue();
        empty.MatchWithin48h.Should().BeFalse("senza carte né wishlist non può avere match");
        fresh.MatchWithin48h.Should().BeNull();
        old.MatchWithin48h.Should().BeNull("troppo vecchio: escluso dalla metrica");
        matchmaking.Verify(m => m.FindMatchesAsync(empty.Id, null, null, null, null, null), Times.Never);
    }

    [Fact]
    public async Task ActiveDayWriter_IgnoresDuplicates()
    {
        var u = AddUser("u", "Milano", 1);
        await _db.SaveChangesAsync();
        var day = PlanMetrics.ItalianDay(_now);
        await ActiveDayWriter.SaveAsync(_db, new[] { new UserActiveDay { UserId = u.Id, Day = day }, new UserActiveDay { UserId = u.Id, Day = day } }, default);
        await ActiveDayWriter.SaveAsync(_db, new[] { new UserActiveDay { UserId = u.Id, Day = day }, new UserActiveDay { UserId = 999, Day = day } }, default);
        (await _db.UserActiveDays.CountAsync()).Should().Be(1);
    }
}
