using CardExchange.API.Services;
using CardExchange.API.Services.Prices;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardExchange.Tests.Services;

public class DailyMaintenanceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ICatalogPriceRefresher> _refresher = new();
    private readonly Mock<IPriceTrackingService> _priceTracking = new();
    private readonly DailyMaintenance _job;

    public DailyMaintenanceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _job = new DailyMaintenance(_context, _refresher.Object, _priceTracking.Object, new Mock<ILogger<DailyMaintenance>>().Object);
    }

    public void Dispose() => _context.Dispose();

    private async Task<(User a, User b)> SeedUsersAsync()
    {
        var a = new User { Email = "a@t.it", Username = "a", PasswordHash = "x" };
        var b = new User { Email = "b@t.it", Username = "b", PasswordHash = "x" };
        _context.Users.AddRange(a, b);
        await _context.SaveChangesAsync();
        return (a, b);
    }

    private async Task<CardInfo> SeedCardInfoAsync(string name, decimal? priceEur, string? scryfallId = null)
    {
        var game = await _context.Games.FirstOrDefaultAsync() ?? _context.Games.Add(new Game { Name = "Magic" }).Entity;
        var set = await _context.CardSets.FirstOrDefaultAsync() ?? _context.CardSets.Add(new CardSet { Name = "Set", Code = "SET", Game = game }).Entity;
        var info = new CardInfo { Name = name, CardSet = set, PriceEur = priceEur, ScryfallId = scryfallId };
        _context.CardInfos.Add(info);
        await _context.SaveChangesAsync();
        return info;
    }

    [Fact]
    public async Task ExpireOffers_ClosesOnlyPendingOffersPastExpiry()
    {
        var (a, b) = await SeedUsersAsync();
        var expired = new TradeOffer { SenderId = a.Id, ReceiverId = b.Id, Status = TradeOfferStatus.Pending, ExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
        var stillValid = new TradeOffer { SenderId = a.Id, ReceiverId = b.Id, Status = TradeOfferStatus.Pending, ExpiresAt = DateTime.UtcNow.AddDays(1) };
        var acceptedOld = new TradeOffer { SenderId = a.Id, ReceiverId = b.Id, Status = TradeOfferStatus.Accepted, ExpiresAt = DateTime.UtcNow.AddDays(-3) };
        _context.TradeOffers.AddRange(expired, stillValid, acceptedOld);
        await _context.SaveChangesAsync();

        var count = await _job.ExpireOffersAsync();

        count.Should().Be(1);
        expired.Status.Should().Be(TradeOfferStatus.Expired);
        stillValid.Status.Should().Be(TradeOfferStatus.Pending);
        acceptedOld.Status.Should().Be(TradeOfferStatus.Accepted);
    }

    [Fact]
    public async Task Snapshot_RecordsOnlyCardsInUse_AndIsIdempotent()
    {
        var (a, _) = await SeedUsersAsync();
        var owned = await SeedCardInfoAsync("Owned", 2m);
        var wished = await SeedCardInfoAsync("Wished", 5m);
        await SeedCardInfoAsync("Unused", 9m);
        await SeedCardInfoAsync("NoPrice", null);
        _context.Cards.Add(new Card { UserId = a.Id, CardInfoId = owned.Id, Condition = CardCondition.NearMint });
        _context.WishlistItems.Add(new WishlistItem { UserId = a.Id, CardInfoId = wished.Id });
        await _context.SaveChangesAsync();

        (await _job.SnapshotPricesAsync()).Should().Be(2);
        owned.PriceEur = 3m;
        await _context.SaveChangesAsync();
        await _job.SnapshotPricesAsync();

        var history = await _context.PriceHistories.ToListAsync();
        history.Should().HaveCount(2, "una sola riga per carta e per giorno");
        history.Single(h => h.CardInfoId == owned.Id).PriceEur.Should().Be(3m, "la seconda esecuzione aggiorna la riga del giorno");
        history.Should().OnlyContain(h => h.SnapshotDate == DateTime.UtcNow.Date);
    }

    [Fact]
    public async Task RefreshPrices_PassesOnlyCardsInUseToRefresher()
    {
        var (a, _) = await SeedUsersAsync();
        var owned = await SeedCardInfoAsync("Owned", 1m, scryfallId: "11111111-1111-1111-1111-111111111111");
        await SeedCardInfoAsync("Unused", 1m, scryfallId: "22222222-2222-2222-2222-222222222222");
        _context.Cards.Add(new Card { UserId = a.Id, CardInfoId = owned.Id, Condition = CardCondition.NearMint });
        await _context.SaveChangesAsync();

        IReadOnlyList<CardInfo>? passed = null;
        _refresher.Setup(r => r.RefreshMagicAsync(It.IsAny<IReadOnlyList<CardInfo>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<CardInfo>, CancellationToken>((list, _) => passed = list)
            .ReturnsAsync(1);

        var (magic, _, _) = await _job.RefreshPricesAsync(new DailyMaintenanceOptions());

        magic.Should().Be(1);
        passed!.Select(ci => ci.Name).Should().Equal("Owned");
    }

    [Fact]
    public async Task RefreshPrices_FailureInOneGame_DoesNotStopTheOthers()
    {
        _refresher.Setup(r => r.RefreshMagicAsync(It.IsAny<IReadOnlyList<CardInfo>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));
        _refresher.Setup(r => r.RefreshYuGiOhAsync(It.IsAny<IReadOnlyList<CardInfo>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var (magic, _, yugioh) = await _job.RefreshPricesAsync(new DailyMaintenanceOptions());

        magic.Should().Be(0);
        yugioh.Should().Be(3);
    }

    [Theory]
    [InlineData("2026-11-03T01:00:00", "02:30", 90)]          // stessa notte
    [InlineData("2026-11-03T02:30:00", "02:30", 24 * 60)]     // appena eseguito: domani
    [InlineData("2026-11-03T23:00:00", "02:30", 210)]         // dopo mezzanotte
    [InlineData("2026-11-03T01:00:00", "non valido", 90)]     // formato errato: default 02:30
    public void DelayUntilNextRun_ComputesNextOccurrence(string now, string runAt, int expectedMinutes)
    {
        DailyMaintenanceHostedService.DelayUntilNextRun(runAt, DateTime.Parse(now))
            .Should().Be(TimeSpan.FromMinutes(expectedMinutes));
    }
}
