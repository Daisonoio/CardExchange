using CardExchange.API.Services.Billing;
using CardExchange.API.Services.Moderation;
using CardExchange.API.Services.Notifications;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardExchange.Tests.Services;

/// <summary>Su SQLite vero (aggiornamenti condizionali): limiti di consegna, minorenni, crediti, revisione.</summary>
public class PromotionServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"promo-{Guid.NewGuid():N}.db");
    private readonly Mock<INotificationDispatcher> _dispatcher = new();
    private readonly Mock<IUserBlockService> _blocks = new();
    private readonly List<NotificationPayload> _sent = new();
    private readonly DateTime _now = DateTime.UtcNow;
    private int _ownerId, _adultId, _minorId, _unknownAgeId, _farId;

    public PromotionServiceTests()
    {
        using var db = NewContext();
        db.Database.EnsureCreated();
        _blocks.Setup(b => b.GetBlockedUserIdsAsync(It.IsAny<int>())).ReturnsAsync(new HashSet<int>());
        _dispatcher.Setup(d => d.DeliverManyAsync(It.IsAny<IReadOnlyList<NotificationPayload>>()))
            .Callback<IReadOnlyList<NotificationPayload>>(p => _sent.AddRange(p))
            .ReturnsAsync((IReadOnlyList<NotificationPayload> p) => p.Count);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_path); } catch (IOException) { }
    }

    private ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite($"Data Source={_path};Default Timeout=30").Options);

    private PromotionService Service(ApplicationDbContext db) => new(db, _dispatcher.Object, _blocks.Object, new Mock<ILogger<PromotionService>>().Object);

    private static User Person(string name, DateOnly? birth, decimal lat, decimal lon) => new()
    {
        Email = $"{name}@t.it", Username = name, PasswordHash = "x", BirthDate = birth,
        Location = new UserLocation { City = "Milano", Latitude = lat, Longitude = lon }
    };

    /// <summary>Organizzatore a Milano con un evento tra 3 giorni; un adulto, un quindicenne, uno senza età e uno lontano.</summary>
    private async Task<int> SeedAsync(string? description = "Torneo mensile")
    {
        await using var db = NewContext();
        var today = DateOnly.FromDateTime(_now);
        var owner = Person("negozio", today.AddYears(-40), 45.4642m, 9.19m);
        var adult = Person("giulia", today.AddYears(-25), 45.47m, 9.20m);
        var minor = Person("luca15", today.AddYears(-15), 45.46m, 9.18m);
        var unknown = Person("anonimo", null, 45.465m, 9.19m);
        var far = Person("roma", today.AddYears(-30), 41.90m, 12.49m);
        db.Users.AddRange(owner, adult, minor, unknown, far);
        await db.SaveChangesAsync();
        var ev = new Event
        {
            OrganizerId = owner.Id, Title = "Torneo Pokémon", Description = description, Status = EventStatus.Published, IsPublic = true,
            StartDate = _now.AddDays(3), Address = "Via Roma 1", City = "Milano", Country = "IT", Latitude = 45.4642m, Longitude = 9.19m
        };
        db.Events.Add(ev);
        db.PromotionCreditEntries.Add(new PromotionCreditEntry { UserId = owner.Id, Delta = 10, Reason = "test", CreatedAt = _now });
        await db.SaveChangesAsync();
        (_ownerId, _adultId, _minorId, _unknownAgeId, _farId) = (owner.Id, adult.Id, minor.Id, unknown.Id, far.Id);
        return ev.Id;
    }

    [Fact]
    public async Task FeaturedEvent_PaidWithCredits_ReachesOnlyAdultsInRadius()
    {
        var eventId = await SeedAsync();
        await using var db = NewContext();
        var svc = Service(db);
        var promo = await svc.CreateAsync(_ownerId, PromotionType.FeaturedEvent, eventId, 10, null, _now);
        promo.PriceCredits.Should().Be(3);

        await svc.PayWithCreditsAsync(_ownerId, promo.Id, _now);

        _sent.Select(s => s.UserId).Should().BeEquivalentTo(new[] { _adultId });
        _sent.Should().OnlyContain(s => s.Type == NotificationType.Sponsored);
        (await svc.CreditBalanceAsync(_ownerId)).Should().Be(7);
        await using var check = NewContext();
        (await check.Promotions.SingleAsync()).Reach.Should().Be(1);
    }

    [Fact]
    public async Task ThirdSponsoredNotificationInAWeek_IsNotSent()
    {
        var eventId = await SeedAsync();
        await using (var db = NewContext())
        {
            // Già 2 notifiche sponsorizzate negli ultimi 7 giorni
            db.PromotionDeliveries.AddRange(
                new PromotionDelivery { PromotionId = 900, UserId = _adultId, SentAt = _now.AddDays(-2) },
                new PromotionDelivery { PromotionId = 901, UserId = _adultId, SentAt = _now.AddDays(-5) });
            await db.SaveChangesAsync();
        }
        await using var ctx = NewContext();
        var svc = Service(ctx);
        var promo = await svc.CreateAsync(_ownerId, PromotionType.FeaturedEvent, eventId, 10, null, _now);
        await svc.PayWithCreditsAsync(_ownerId, promo.Id, _now);

        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Delivery_HappensOnce_EvenIfRunTwice()
    {
        var eventId = await SeedAsync();
        await using var db = NewContext();
        var svc = Service(db);
        var promo = await svc.CreateAsync(_ownerId, PromotionType.FeaturedEvent, eventId, 10, null, _now);
        await svc.PayWithCreditsAsync(_ownerId, promo.Id, _now);
        await svc.RunAsync(_now.AddMinutes(15));
        await svc.DeliverAsync(promo, _now.AddMinutes(20));

        _sent.Should().HaveCount(1);
    }

    [Fact]
    public async Task SponsoredBroadcast_ReachesOnlyNonFollowersWhoWantACard_NeverMinors()
    {
        await SeedAsync();
        int broadcastId;
        await using (var db = NewContext())
        {
            var set = new CardSet { Name = "Base", Code = "BS", Game = new Game { Name = "Pokémon", Publisher = "P" } };
            var info = new CardInfo { Name = "Charizard", CardSet = set };
            db.CardInfos.Add(info);
            await db.SaveChangesAsync();
            db.Cards.Add(new Card { UserId = _ownerId, CardInfoId = info.Id, Quantity = 1, IsAvailableForTrade = true });
            db.WishlistItems.AddRange(
                new WishlistItem { UserId = _adultId, CardInfoId = info.Id },
                new WishlistItem { UserId = _minorId, CardInfoId = info.Id });
            var b = new LocationBroadcast { AuthorId = _ownerId, PlaceName = "Fiera di Milano", Latitude = 45.4642m, Longitude = 9.19m, EndsAt = _now.AddHours(4) };
            db.LocationBroadcasts.Add(b);
            await db.SaveChangesAsync();
            broadcastId = b.Id;
        }
        await using var ctx = NewContext();
        var svc = Service(ctx);
        var promo = await svc.CreateAsync(_ownerId, PromotionType.SponsoredBroadcast, broadcastId, 10, null, _now);
        promo.PriceCredits.Should().Be(1);
        await svc.PayWithCreditsAsync(_ownerId, promo.Id, _now);

        _sent.Select(s => s.UserId).Should().BeEquivalentTo(new[] { _adultId });
        _sent.Single().Body.Should().StartWith("Sponsorizzato.");
    }

    [Fact]
    public async Task NotEnoughCredits_IsRefused_AndPromotionStaysPending()
    {
        var eventId = await SeedAsync();
        await using var db = NewContext();
        db.PromotionCreditEntries.Add(new PromotionCreditEntry { UserId = _ownerId, Delta = -9, Reason = "speso", CreatedAt = _now });
        await db.SaveChangesAsync();
        var svc = Service(db);
        var promo = await svc.CreateAsync(_ownerId, PromotionType.FeaturedEvent, eventId, 50, null, _now);
        var pay = () => svc.PayWithCreditsAsync(_ownerId, promo.Id, _now);

        await pay.Should().ThrowAsync<PromotionException>().Where(e => e.Code == "NOT_ENOUGH_CREDITS");
        await using var check = NewContext();
        (await check.Promotions.SingleAsync()).Status.Should().Be(PromotionStatus.PendingPayment);
    }

    [Fact]
    public async Task ExternalLink_IsRejectedByReview()
    {
        var eventId = await SeedAsync("Iscrizioni su www.mio-sito.com");
        await using var db = NewContext();
        var act = () => Service(db).CreateAsync(_ownerId, PromotionType.FeaturedEvent, eventId, 10, null, _now);
        await act.Should().ThrowAsync<PromotionException>().Where(e => e.Code == "REJECTED");
    }

    [Fact]
    public async Task MinorOwner_CannotBuyPromotions()
    {
        var eventId = await SeedAsync();
        await using var db = NewContext();
        var ev = await db.Events.SingleAsync(e => e.Id == eventId);
        ev.OrganizerId = _minorId;
        await db.SaveChangesAsync();
        var act = () => Service(db).CreateAsync(_minorId, PromotionType.FeaturedEvent, eventId, 10, null, _now);
        await act.Should().ThrowAsync<PromotionException>().Where(e => e.Code == "ADULTS_ONLY");
    }

    [Fact]
    public async Task FeaturedEvents_VisibleInRadius_ToMinorsToo_AndCountImpressions()
    {
        var eventId = await SeedAsync();
        await using var db = NewContext();
        var svc = Service(db);
        var promo = await svc.CreateAsync(_ownerId, PromotionType.FeaturedEvent, eventId, 10, null, _now);
        await svc.PayWithCreditsAsync(_ownerId, promo.Id, _now);

        (await svc.FeaturedEventsAsync(_minorId, null, null, _now)).Should().HaveCount(1);
        (await svc.FeaturedEventsAsync(_farId, null, null, _now)).Should().BeEmpty();
        await using var check = NewContext();
        (await check.Promotions.SingleAsync()).Impressions.Should().Be(1);
    }

    [Fact]
    public async Task Cancel_BeforeDelivery_RefundsCredits()
    {
        var eventId = await SeedAsync();
        await using var db = NewContext();
        var svc = Service(db);
        var promo = await svc.CreateAsync(_ownerId, PromotionType.FeaturedEvent, eventId, 10, null, _now);
        await svc.CancelAsync(_ownerId, promo.Id, _now);
        (await svc.CreditBalanceAsync(_ownerId)).Should().Be(10);
    }

    [Fact]
    public async Task ManyReports_PauseThePromotion()
    {
        var eventId = await SeedAsync();
        await using var db = NewContext();
        var svc = Service(db);
        var promo = await svc.CreateAsync(_ownerId, PromotionType.FeaturedEvent, eventId, 10, null, _now);
        await svc.PayWithCreditsAsync(_ownerId, promo.Id, _now);
        foreach (var u in new[] { _adultId, _minorId, _unknownAgeId }) await svc.ReportAsync(u, promo.Id, null, _now);
        (await svc.ReportAsync(_adultId, promo.Id, null, _now)).Should().BeFalse();

        await using var check = NewContext();
        (await check.Promotions.SingleAsync()).Status.Should().Be(PromotionStatus.Rejected);
    }

    [Fact]
    public async Task MonthlyPlanCredits_GrantedOncePerMonth()
    {
        await SeedAsync();
        await using var db = NewContext();
        var banco = PlanCatalog.All().Single(p => p.Name == "Banco");
        db.UserSubscriptions.Add(new UserSubscription { UserId = _adultId, Plan = banco, Status = SubscriptionStatus.Active, StartDate = _now.AddDays(-3), EndDate = _now.AddDays(27) });
        await db.SaveChangesAsync();
        var svc = Service(db);

        (await svc.GrantMonthlyCreditsAsync(_now)).Should().Be(1);
        (await svc.GrantMonthlyCreditsAsync(_now.AddHours(1))).Should().Be(0);
        (await svc.CreditBalanceAsync(_adultId)).Should().Be(2);
    }

    [Fact]
    public async Task RegionPackage_FeaturesTheEventWithin100Km()
    {
        var eventId = await SeedAsync();
        await using var db = NewContext();
        var package = await CardExchange.API.Services.Events.EventPackages.ActivateAsync(db, eventId, EventPackageTier.Region, null, null, _now);
        var promo = await db.Promotions.SingleAsync(p => p.Id == package!.PromotionId);
        promo.RadiusKm.Should().Be(100);
        promo.Status.Should().Be(PromotionStatus.Active);
        // Idempotente: una seconda attivazione non crea un'altra promozione
        await CardExchange.API.Services.Events.EventPackages.ActivateAsync(db, eventId, EventPackageTier.Region, null, null, _now);
        (await db.Promotions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public void Review_And_Prices()
    {
        PromotionRules.Review("Torneo", "Porta il mazzo").Should().BeNull();
        PromotionRules.Review("Scrivimi su whatsapp").Should().NotBeNull();
        PromotionRules.Review("Chiama 333 123 4567").Should().NotBeNull();
        PromotionRules.PriceCredits(PromotionType.FeaturedEvent, 50).Should().Be(5);
        PromotionRules.PriceEur(PromotionType.SponsoredBroadcast, 25).Should().Be(8m);
    }
}
