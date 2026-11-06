using CardExchange.API.Services;
using CardExchange.API.Services.Moderation;
using CardExchange.API.Services.Notifications;
using CardExchange.API.Services.Stores;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardExchange.Tests.Services;

public class BuyListServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"buy-{Guid.NewGuid():N}.db");
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<IUserBlockService> _blocks = new();
    private readonly Mock<INotificationDispatcher> _dispatcher = new();
    private readonly DateTime _now = DateTime.UtcNow;
    private Store _store = null!;
    private int _infoId;
    private readonly Dictionary<string, (int UserId, int CardId)> _u = new();

    public BuyListServiceTests()
    {
        using var db = NewContext();
        db.Database.EnsureCreated();
        _blocks.Setup(b => b.GetBlockedUserIdsAsync(It.IsAny<int>())).ReturnsAsync(new HashSet<int>());
        _dispatcher.Setup(d => d.DeliverManyAsync(It.IsAny<IReadOnlyList<NotificationPayload>>())).ReturnsAsync((IReadOnlyList<NotificationPayload> p) => p.Count);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_path); } catch (IOException) { }
    }

    private ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite($"Data Source={_path};Default Timeout=30").Options);

    private BuyListService Service(ApplicationDbContext db) => new(db, _notifications.Object, _blocks.Object, new Mock<ILogger<BuyListService>>().Object);

    private async Task SeedAsync()
    {
        await using var db = NewContext();
        var owner = new User { Email = "o@t.it", Username = "owner", PasswordHash = "x" };
        var info = new CardInfo { Name = "Charizard", CardSet = new CardSet { Name = "Base", Code = "BS", Game = new Game { Name = "Pokémon", Publisher = "P" } } };
        db.Users.Add(owner);
        db.CardInfos.Add(info);
        await db.SaveChangesAsync();
        _store = new Store { Name = "Games Corner", City = "Milano", Address = "Via Roma 1", OwnerId = owner.Id, Status = StoreStatus.Verified, Latitude = 45.46m, Longitude = 9.19m };
        db.Stores.Add(_store);
        db.UserSubscriptions.Add(new UserSubscription { UserId = owner.Id, Plan = CardExchange.Core.Rules.PlanCatalog.All().Single(p => p.Name == "Negozio Pro"),
            Status = SubscriptionStatus.Active, StartDate = _now.AddDays(-1), EndDate = _now.AddDays(60) });
        foreach (var (name, cond, age) in new[] { ("good", CardCondition.NearMint, 30), ("good2", CardCondition.Excellent, 25), ("worn", CardCondition.HeavilyPlayed, 30), ("minor", CardCondition.Mint, 15) })
        {
            var u = new User { Email = $"{name}@t.it", Username = name, PasswordHash = "x", BirthDate = DateOnly.FromDateTime(_now).AddYears(-age),
                Location = new UserLocation { City = "Milano", Province = "MI", Country = "IT", Latitude = 45.47m, Longitude = 9.2m, MaxDistanceKm = 20 } };
            db.Users.Add(u);
            await db.SaveChangesAsync();
            var c = new Card { UserId = u.Id, CardInfoId = info.Id, Condition = cond, Quantity = 2, IsAvailableForTrade = false };
            db.Cards.Add(c);
            await db.SaveChangesAsync();
            _u[name] = (u.Id, c.Id);
        }
        _infoId = info.Id;
    }

    private async Task<int> OrderAsync(int wanted = 2)
    {
        await using var db = NewContext();
        var store = await db.Stores.FirstAsync();
        var o = await Service(db).CreateOrderAsync(store, new BuyOrderInput(_infoId, CardCondition.Excellent, null, null, 32, 40, wanted, null), _now);
        return o.Id;
    }

    [Fact]
    public async Task OnlyMatchingAdults_SeeTheRequest_AndGetOneWeeklyAlert()
    {
        await SeedAsync();
        await OrderAsync();
        await using var db = NewContext();
        var svc = Service(db);

        (await svc.ForMeAsync(_u["good"].UserId, _now)).Should().ContainSingle();
        (await svc.ForMeAsync(_u["worn"].UserId, _now)).Should().BeEmpty("condizione inferiore al minimo");
        (await svc.ForMeAsync(_u["minor"].UserId, _now)).Should().BeEmpty("niente proposte ai minorenni");

        (await svc.SendAlertsAsync(_now, _dispatcher.Object)).Should().Be(2);
        _dispatcher.Verify(d => d.DeliverManyAsync(It.Is<IReadOnlyList<NotificationPayload>>(p =>
            p.Count == 2 && p.All(x => x.Type == NotificationType.StoreOffer && x.Title == "Games Corner compra una tua carta"
                                       && x.Body.StartsWith("Charizard (40 € in credito o 32 € in contanti)")))), Times.Once);
        (await svc.SendAlertsAsync(_now.AddDays(1), _dispatcher.Object)).Should().Be(0, "al massimo un avviso a settimana per negozio");
    }

    [Fact]
    public async Task CannotAcceptMoreThanWanted_EvenConcurrently()
    {
        await SeedAsync();
        var orderId = await OrderAsync(wanted: 2);
        int a, b;
        await using (var db = NewContext())
        {
            var svc = Service(db);
            a = (await svc.ProposeAsync(_u["good"].UserId, orderId, _u["good"].CardId, 2, _now)).Id;
            b = (await svc.ProposeAsync(_u["good2"].UserId, orderId, _u["good2"].CardId, 1, _now)).Id;
            var dup = () => svc.ProposeAsync(_u["good"].UserId, orderId, _u["good"].CardId, 1, _now);
            await dup.Should().ThrowAsync<BuyListException>().WithMessage("*già una proposta aperta*");
            var worn = () => svc.ProposeAsync(_u["worn"].UserId, orderId, _u["worn"].CardId, 1, _now);
            await worn.Should().ThrowAsync<BuyListException>().WithMessage("*non corrisponde*");
        }
        var ownerId = _store.OwnerId;
        async Task<bool> Accept(int offerId)
        {
            await using var db = NewContext();
            try { await Service(db).AcceptAsync(offerId, ownerId, null, _now); return true; }
            catch (BuyListException) { return false; }
        }
        var results = await Task.WhenAll(Task.Run(() => Accept(a)), Task.Run(() => Accept(b)));
        results.Count(r => r).Should().Be(1);
        await using var check = NewContext();
        var order = await check.StoreBuyOrders.SingleAsync();
        (order.QuantityPending <= order.QuantityWanted).Should().BeTrue();
    }

    [Fact]
    public async Task Complete_FillsOrder_RemovesCopy_ClosesOthers_ExpiryReleases()
    {
        await SeedAsync();
        var orderId = await OrderAsync(wanted: 1);
        await using var db = NewContext();
        var svc = Service(db);
        var ownerId = _store.OwnerId;
        var first = await svc.ProposeAsync(_u["good"].UserId, orderId, _u["good"].CardId, 1, _now);
        var other = await svc.ProposeAsync(_u["good2"].UserId, orderId, _u["good2"].CardId, 1, _now);
        await svc.AcceptAsync(first.Id, ownerId, "Passa dopo le 16", _now);
        await svc.CompleteAsync(first.Id, ownerId, _now);

        await using var check = NewContext();
        var order = await check.StoreBuyOrders.SingleAsync();
        (order.QuantityFilled, order.QuantityPending, order.IsActive).Should().Be((1, 0, false));
        (await check.Cards.IgnoreQueryFilters().SingleAsync(c => c.Id == _u["good"].CardId)).Quantity.Should().Be(1);
        (await check.StoreBuyOffers.SingleAsync(o => o.Id == other.Id)).Status.Should().Be(BuyOfferStatus.Rejected);

        // Una consegna non avvenuta in tempo scade e libera la quantità
        var orderId2 = await OrderAsync(wanted: 1);
        var p = await svc.ProposeAsync(_u["good2"].UserId, orderId2, _u["good2"].CardId, 1, _now);
        await svc.AcceptAsync(p.Id, ownerId, null, _now);
        (await svc.RunAsync(_now.AddDays(6.5))).Reminded.Should().Be(1);
        (await svc.RunAsync(_now.AddDays(8))).Expired.Should().Be(1);
        await using var check2 = NewContext();
        (await check2.StoreBuyOrders.SingleAsync(o => o.Id == orderId2)).QuantityPending.Should().Be(0);
    }

    [Theory]
    [InlineData(40.0, 32.0, "40 € in credito o 32 € in contanti")]
    [InlineData(null, 12.5, "12,5 € in contanti")]
    public void PriceText(double? credit, double? cash, string expected) =>
        BuyListService.PriceText(new StoreBuyOrder { PriceCreditEur = (decimal?)credit, PriceCashEur = (decimal?)cash }).Should().Be(expected);
}
