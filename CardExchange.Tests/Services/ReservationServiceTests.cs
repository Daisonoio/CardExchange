using CardExchange.API.Services;
using CardExchange.API.Services.Moderation;
using CardExchange.API.Services.Stores;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardExchange.Tests.Services;

/// <summary>Su SQLite vero (file temporaneo): il blocco della quantità usa un aggiornamento condizionale.</summary>
public class ReservationServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"res-{Guid.NewGuid():N}.db");
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<IUserBlockService> _blocks = new();
    private readonly DateTime _now = DateTime.UtcNow;
    private int _storeId, _ownerId, _cardId;
    private readonly List<int> _users = new();

    public ReservationServiceTests()
    {
        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_path); } catch (IOException) { }
    }

    private ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite($"Data Source={_path};Default Timeout=30").Options);

    private ReservationService Service(ApplicationDbContext db) => new(db, _notifications.Object, _blocks.Object, new Mock<ILogger<ReservationService>>().Object);
    private ReservationService PaidService(ApplicationDbContext db) => new(db, _notifications.Object, _blocks.Object, new Mock<ILogger<ReservationService>>().Object, TestEntitlements.Paid(db));

    private async Task SeedAsync(int copies = 1, bool storePlan = true, int users = 3)
    {
        await using var db = NewContext();
        var owner = new User { Email = "o@t.it", Username = "owner", PasswordHash = "x" };
        db.Users.Add(owner);
        for (var i = 0; i < users; i++) db.Users.Add(new User { Email = $"u{i}@t.it", Username = $"u{i}", PasswordHash = "x" });
        var game = new Game { Name = "Pokémon", Publisher = "P" };
        var set = new CardSet { Name = "Base", Code = "BS", Game = game };
        var info = new CardInfo { Name = "Charizard", CardSet = set, PriceEur = 300 };
        db.CardInfos.Add(info);
        await db.SaveChangesAsync();
        var store = new Store { Name = "Games Corner", City = "Milano", Address = "Via Roma 1", OwnerId = owner.Id, Status = StoreStatus.Verified };
        db.Stores.Add(store);
        var card = new Card { UserId = owner.Id, CardInfoId = info.Id, Quantity = copies, IsAvailableForTrade = true, AskingPrice = 280, Condition = CardCondition.NearMint };
        db.Cards.Add(card);
        if (storePlan)
        {
            // Le funzioni del negozio vengono dal tipo di profilo
            owner.ProfileType = ProfileType.Store;
        }
        await db.SaveChangesAsync();
        // Carta caricata da più di 2 ore: fuori dall'accesso anticipato Premium
        await db.Database.ExecuteSqlRawAsync("UPDATE Cards SET CreatedAt = {0} WHERE Id = {1}", _now.AddHours(-3), card.Id);
        (_storeId, _ownerId, _cardId) = (store.Id, owner.Id, card.Id);
        _users.AddRange(await db.Users.Where(u => u.Id != owner.Id).Select(u => u.Id).ToListAsync());
    }

    private async Task<Card> CardAsync()
    {
        await using var db = NewContext();
        return await db.Cards.IgnoreQueryFilters().AsNoTracking().SingleAsync(c => c.Id == _cardId);
    }

    [Fact]
    public async Task LastCopy_TwoSimultaneousReservations_OnlyOneSucceeds()
    {
        await SeedAsync(copies: 1);
        async Task<bool> Try(int userId)
        {
            await using var db = NewContext();
            try { await Service(db).CreateAsync(_storeId, userId, _cardId, 1, _now); return true; }
            catch (ReservationException ex) { ex.Message.Should().Be("Questa carta non è più disponibile"); return false; }
        }
        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(i => Task.Run(() => Try(_users[i]))));

        results.Count(r => r).Should().Be(1);
        (await CardAsync()).ReservedQuantity.Should().Be(1);
        await using var check = NewContext();
        (await check.Reservations.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Pickup_WithCode_RemovesCopy_CancelReleases()
    {
        await SeedAsync(copies: 2);
        await using var db = NewContext();
        var svc = Service(db);
        var a = await svc.CreateAsync(_storeId, _users[0], _cardId, 1, _now);
        var b = await svc.CreateAsync(_storeId, _users[1], _cardId, 1, _now);
        a.PickupCode.Should().HaveLength(6);
        a.PriceEur.Should().Be(280);
        (await CardAsync()).ReservedQuantity.Should().Be(2);

        var wrong = () => svc.PickUpAsync(a.Id, _ownerId, "XXXXXX", _now);
        await wrong.Should().ThrowAsync<ReservationException>().WithMessage("*codice*");
        var notOwner = () => svc.PickUpAsync(a.Id, _users[0], a.PickupCode, _now);
        await notOwner.Should().ThrowAsync<UnauthorizedAccessException>();

        await svc.PickUpAsync(a.Id, _ownerId, a.PickupCode.ToLowerInvariant(), _now);
        var card = await CardAsync();
        (card.Quantity, card.ReservedQuantity).Should().Be((1, 1));

        await svc.CancelAsync(b.Id, _users[1], _now);
        card = await CardAsync();
        (card.Quantity, card.ReservedQuantity).Should().Be((1, 0));
        _notifications.Verify(n => n.SendAsync(_ownerId, It.IsAny<NotificationType>(), "Prenotazione annullata", It.IsAny<string>(), _storeId, "StoreReservations"), Times.Once);
    }

    [Fact]
    public async Task LastCopyPickedUp_CardLeavesTheStore()
    {
        await SeedAsync(copies: 1);
        await using var db = NewContext();
        var svc = Service(db);
        var r = await svc.CreateAsync(_storeId, _users[0], _cardId, 1, _now);
        await svc.ConfirmAsync(r.Id, _ownerId, _now);
        await svc.PickUpAsync(r.Id, _ownerId, r.PickupCode, _now);
        var card = await CardAsync();
        (card.Quantity, card.IsAvailableForTrade, card.IsDeleted).Should().Be((0, false, true));
    }

    [Fact]
    public async Task Limits_StorePlan_ActiveCount_AndNoShowPause()
    {
        await SeedAsync(copies: 10, storePlan: false);
        await using (var db = NewContext())
        {
            var noPlan = () => Service(db).CreateAsync(_storeId, _users[0], _cardId, 1, _now);
            await noPlan.Should().ThrowAsync<ReservationException>().WithMessage("*non accetta prenotazioni*");
            (await db.Users.SingleAsync(u => u.Id == _ownerId)).ProfileType = ProfileType.Store;
            await db.SaveChangesAsync();
        }
        await using (var db = NewContext())
        {
            var svc = Service(db);
            for (var i = 0; i < ReservationService.MaxActive; i++) await svc.CreateAsync(_storeId, _users[0], _cardId, 1, _now);
            var fourth = () => svc.CreateAsync(_storeId, _users[0], _cardId, 1, _now);
            await fourth.Should().ThrowAsync<ReservationException>().WithMessage("Hai già 3 prenotazioni attive*");

            var own = () => svc.CreateAsync(_storeId, _ownerId, _cardId, 1, _now);
            await own.Should().ThrowAsync<ReservationException>();

            // Due mancate presentazioni: pausa di 30 giorni
            var r1 = await svc.CreateAsync(_storeId, _users[1], _cardId, 1, _now);
            var r2 = await svc.CreateAsync(_storeId, _users[1], _cardId, 1, _now);
            await svc.NoShowAsync(r1.Id, _ownerId, _now);
            await svc.NoShowAsync(r2.Id, _ownerId, _now);
            var paused = () => svc.CreateAsync(_storeId, _users[1], _cardId, 1, _now.AddDays(10));
            await paused.Should().ThrowAsync<ReservationException>().WithMessage("*non ritirate*");
            (await svc.CreateAsync(_storeId, _users[1], _cardId, 1, _now.AddDays(31))).Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Expiry_RemindsThenExpires_AndReleases()
    {
        await SeedAsync(copies: 1);
        await using var db = NewContext();
        var svc = Service(db);
        var r = await svc.CreateAsync(_storeId, _users[0], _cardId, 1, _now);

        (await svc.RunAsync(r.ExpiresAt.AddHours(-5))).Should().Be((1, 0));
        (await svc.RunAsync(r.ExpiresAt.AddHours(-4))).Should().Be((0, 0));
        (await svc.RunAsync(r.ExpiresAt.AddMinutes(1))).Should().Be((0, 1));
        (await CardAsync()).ReservedQuantity.Should().Be(0);
        await using var check = NewContext();
        (await check.Reservations.SingleAsync()).Status.Should().Be(ReservationStatus.Expired);
    }

    [Theory]
    [InlineData("2026-11-06T09:00:00Z", "2026-11-09T09:00:00Z")]  // venerdì 10:00 → lunedì 10:00 (salta la domenica)
    [InlineData("2026-11-03T09:00:00Z", "2026-11-05T09:00:00Z")]  // martedì → giovedì
    public void Expiry_SkipsSundays(string now, string expected) =>
        ReservationService.ExpiryFrom(DateTime.Parse(now).ToUniversalTime()).Should().Be(DateTime.Parse(expected).ToUniversalTime());

    [Fact]
    public async Task NewStoreCard_IsReservedForPremium_ForTwoHours()
    {
        await SeedAsync(copies: 2);
        await using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync("UPDATE Cards SET CreatedAt = {0} WHERE Id = {1}", _now.AddMinutes(-30), _cardId);
        // Piani spenti: nessun accesso anticipato, la carta nuova è subito prenotabile da tutti
        (await Service(db).CreateAsync(_storeId, _users[2], _cardId, 1, _now)).Should().NotBeNull();
        await db.Database.ExecuteSqlRawAsync("UPDATE Cards SET ReservedQuantity = 0 WHERE Id = {0}", _cardId);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Reservations");

        // Piani accesi: per 2 ore solo i Premium
        var free = () => PaidService(db).CreateAsync(_storeId, _users[0], _cardId, 1, _now);
        await free.Should().ThrowAsync<ReservationException>().WithMessage("*90 minuti*solo i Premium*");

        db.UserSubscriptions.Add(new UserSubscription { UserId = _users[1], Plan = CardExchange.Core.Rules.PlanCatalog.All().First(p => p.Name == "Premium Mensile"),
            Status = SubscriptionStatus.Active, StartDate = _now.AddDays(-1), EndDate = _now.AddDays(30) });
        await db.SaveChangesAsync();
        (await PaidService(db).CreateAsync(_storeId, _users[1], _cardId, 1, _now)).Should().NotBeNull();
        (await PaidService(db).CreateAsync(_storeId, _users[0], _cardId, 1, _now.AddMinutes(91))).Should().NotBeNull("dopo 2 ore la vedono tutti");
    }
}
