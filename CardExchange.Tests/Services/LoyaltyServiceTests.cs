using CardExchange.API.Configuration;
using CardExchange.API.Services;
using CardExchange.API.Services.Stores;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace CardExchange.Tests.Services;

/// <summary>Su SQLite vero: timbro unico al giorno (indice univoco) e riscatto con aggiornamento condizionale.</summary>
public class LoyaltyServiceTests : IDisposable
{
    private const string Key = "chiave-di-prova-lunga-almeno-trentadue-caratteri";
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"loy-{Guid.NewGuid():N}.db");
    private readonly Mock<INotificationService> _notifications = new();
    // Mezzogiorno italiano: lontano dal cambio di giorno
    private readonly DateTime _now = new(2027, 4, 14, 10, 0, 0, DateTimeKind.Utc);
    private int _storeId, _ownerId, _userId;

    public LoyaltyServiceTests()
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

    private LoyaltyService Service(ApplicationDbContext db) =>
        new(db, _notifications.Object, Options.Create(new JwtSettings { SecretKey = Key }));

    private async Task<Store> SeedAsync(ProfileType profile = ProfileType.Store, bool program = true)
    {
        await using var db = NewContext();
        var owner = new User { Email = "o@t.it", Username = "owner", PasswordHash = "x", ProfileType = profile };
        var user = new User { Email = "u@t.it", Username = "giulia", PasswordHash = "x" };
        db.Users.AddRange(owner, user);
        await db.SaveChangesAsync();
        var store = new Store { Name = "Games Corner", City = "Milano", Address = "Via Roma 1", OwnerId = owner.Id, Status = StoreStatus.Verified };
        db.Stores.Add(store);
        await db.SaveChangesAsync();
        if (program) db.LoyaltyPrograms.Add(new LoyaltyProgram { StoreId = store.Id, StampsRequired = 3, RewardDescription = "Una bustina in omaggio" });
        await db.SaveChangesAsync();
        (_storeId, _ownerId, _userId) = (store.Id, owner.Id, user.Id);
        return store;
    }

    private string Pass(DateTime at) => LoyaltyPass.Create(_userId, at, Key);

    [Fact]
    public void Pass_RoundTrips_AndExpiresAfterTwoMinutes()
    {
        var token = LoyaltyPass.Create(42, _now, Key);
        LoyaltyPass.Validate(token, _now.AddSeconds(119), Key).Should().Be(42);
        LoyaltyPass.Validate(token.ToUpperInvariant(), _now, Key).Should().Be(42);

        var expired = () => LoyaltyPass.Validate(token, _now.AddMinutes(2).AddSeconds(1), Key);
        expired.Should().Throw<LoyaltyException>().Where(e => e.Code == "PASS_EXPIRED");
    }

    [Fact]
    public void Pass_Tampered_IsRejected()
    {
        var token = LoyaltyPass.Create(42, _now, Key);
        var parts = token.Split('-');
        var other = $"{LoyaltyPass.Create(43, _now, Key).Split('-')[0]}-{parts[1]}-{parts[2]}";
        ((Action)(() => LoyaltyPass.Validate(other, _now, Key))).Should().Throw<LoyaltyException>().Where(e => e.Code == "PASS_INVALID");
        ((Action)(() => LoyaltyPass.Validate(token, _now, Key + "x"))).Should().Throw<LoyaltyException>().Where(e => e.Code == "PASS_INVALID");
        ((Action)(() => LoyaltyPass.Validate("ciao", _now, Key))).Should().Throw<LoyaltyException>();
    }

    [Fact]
    public async Task TwoScansSameDay_GiveOneStamp()
    {
        var store = await SeedAsync();
        await using var db = NewContext();
        var first = await Service(db).StampWithPassAsync(store, Pass(_now), _now);
        var second = await Service(db).StampWithPassAsync(store, Pass(_now.AddHours(3)), _now.AddHours(3));

        first.Outcome.Should().Be(StampOutcome.Stamped);
        first.Username.Should().Be("giulia");
        second.Outcome.Should().Be(StampOutcome.AlreadyToday);
        second.Stamps.Should().Be(1);

        var next = await Service(db).StampWithPassAsync(store, Pass(_now.AddDays(1)), _now.AddDays(1));
        next.Stamps.Should().Be(2);
    }

    [Fact]
    public async Task SimultaneousScans_GiveOneStamp()
    {
        var store = await SeedAsync();
        async Task Scan()
        {
            await using var db = NewContext();
            await Service(db).StampWithPassAsync(store, Pass(_now), _now);
        }
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(Scan)));

        await using var check = NewContext();
        (await check.LoyaltyStamps.CountAsync()).Should().Be(1);
        (await check.LoyaltyCards.SingleAsync()).Stamps.Should().Be(1);
    }

    [Fact]
    public async Task Reward_ReadyAfterRequiredStamps_RedeemSubtracts()
    {
        var store = await SeedAsync();
        await using var db = NewContext();
        var svc = Service(db);
        var redeemEarly = () => svc.RedeemAsync(store, Pass(_now), _now);
        await redeemEarly.Should().ThrowAsync<LoyaltyException>().Where(e => e.Code == "NOT_ENOUGH");

        StampResult last = null!;
        for (var d = 0; d < 3; d++) last = await svc.StampWithPassAsync(store, Pass(_now.AddDays(d)), _now.AddDays(d));
        last.RewardReady.Should().BeTrue();
        _notifications.Verify(n => n.SendAsync(_userId, NotificationType.Loyalty, "Premio pronto da Games Corner", It.IsAny<string>(), _storeId, "Store"), Times.Once);

        var redeemed = await svc.RedeemAsync(store, Pass(_now.AddDays(3)), _now.AddDays(3));
        redeemed.Stamps.Should().Be(0);
        await using var check = NewContext();
        (await check.LoyaltyCards.SingleAsync()).RewardsRedeemed.Should().Be(1);
        await ((Func<Task>)(() => Service(check).RedeemAsync(store, Pass(_now.AddDays(3)), _now.AddDays(3)))).Should().ThrowAsync<LoyaltyException>();
    }

    [Fact]
    public async Task ExpiredPass_IsRefusedWithClearMessage()
    {
        var store = await SeedAsync();
        await using var db = NewContext();
        var act = () => Service(db).StampWithPassAsync(store, Pass(_now), _now.AddMinutes(5));
        await act.Should().ThrowAsync<LoyaltyException>().WithMessage("Il codice è scaduto*");
    }

    [Fact]
    public async Task EventCheckIn_StampsOnlyWhenProgramActive_AndNotTheOwner()
    {
        var store = await SeedAsync();
        await using var db = NewContext();
        var ev = new Event { Title = "Torneo", StoreId = _storeId, OrganizerId = _ownerId };
        await Service(db).StampForEventAsync(ev, _userId, _now);
        await Service(db).StampForEventAsync(ev, _ownerId, _now);
        (await db.LoyaltyCards.CountAsync()).Should().Be(1);
        (await db.LoyaltyStamps.SingleAsync()).Source.Should().Be(LoyaltyStampSource.EventCheckIn);
    }

    [Fact]
    public async Task WithoutStorePlan_NoStampAndConfigureRefused()
    {
        var store = await SeedAsync(profile: ProfileType.Collector);
        await using var db = NewContext();
        var r = await Service(db).StampAsync(store, _userId, LoyaltyStampSource.EventCheckIn, null, _now);
        r.Outcome.Should().Be(StampOutcome.NoProgram);
        var act = () => Service(db).ConfigureAsync(store, 5, "Una bustina", true);
        await act.Should().ThrowAsync<LoyaltyException>().Where(e => e.Status == 402);
    }

    [Fact]
    public async Task Stats_HiddenBelowThreeCustomers()
    {
        await SeedAsync();
        await using var db = NewContext();
        (await Service(db).StatsAsync(_storeId, 3, _now)).Should().BeNull();
    }
}
