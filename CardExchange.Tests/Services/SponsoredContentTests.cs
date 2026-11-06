using CardExchange.API.Services.Billing;
using CardExchange.API.Services.Moderation;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CardExchange.Tests.Services;

/// <summary>Spazi sponsorizzati e sponsor di marca, su SQLite vero (aggiornamenti condizionali).</summary>
public class SponsoredContentTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"slots-{Guid.NewGuid():N}.db");
    private readonly Mock<IUserBlockService> _blocks = new();
    private readonly DateTime _now = DateTime.UtcNow;
    private int _adult, _minor, _premium, _storeOwner;

    public SponsoredContentTests()
    {
        using var db = NewContext();
        db.Database.EnsureCreated();
        _blocks.Setup(b => b.GetBlockedUserIdsAsync(It.IsAny<int>())).ReturnsAsync(new HashSet<int>());
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_path); } catch (IOException) { }
    }

    private ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite($"Data Source={_path};Default Timeout=30").Options);

    private SponsoredContent Service(ApplicationDbContext db) => new(db, _blocks.Object);

    private async Task SeedAsync()
    {
        await using var db = NewContext();
        var today = DateOnly.FromDateTime(_now);
        User P(string n, int age) => new()
        {
            Email = $"{n}@t.it", Username = n, PasswordHash = "x", BirthDate = today.AddYears(-age),
            Location = new UserLocation { City = "Milano", Latitude = 45.47m, Longitude = 9.19m }
        };
        var adult = P("adulto", 30); var minor = P("minore", 15); var premium = P("premium", 35); var owner = P("negozio", 45);
        db.Users.AddRange(adult, minor, premium, owner);
        await db.SaveChangesAsync();
        db.UserSubscriptions.Add(new UserSubscription { UserId = premium.Id, Plan = PlanCatalog.All().Single(p => p.Name == "Premium Mensile"), Status = SubscriptionStatus.Active, StartDate = _now.AddDays(-1), EndDate = _now.AddDays(29) });
        var sponsor = new Sponsor { Name = "Ultra Sleeves", WebsiteUrl = "https://example.com" };
        db.Sponsors.Add(sponsor);
        await db.SaveChangesAsync();
        var ship = new Sponsorship { SponsorId = sponsor.Id, Scope = SponsorshipScope.Month, Placement = SponsorshipPlacement.Slot, StartsAt = _now.AddDays(-1), EndsAt = _now.AddDays(10), SlotPlacement = SlotPlacement.HomeFeed, SlotTitle = "Bustine protettive" };
        db.Sponsorships.Add(ship);
        await db.SaveChangesAsync();
        db.SponsoredSlots.AddRange(
            new SponsoredSlot { SponsorshipId = ship.Id, Placement = SlotPlacement.HomeFeed, Title = "Marca", TargetUrl = "/sponsors/1", StartsAt = _now.AddDays(-1), EndsAt = _now.AddDays(10) },
            new SponsoredSlot { OwnerUserId = owner.Id, Placement = SlotPlacement.HomeFeed, Title = "Negozio vicino", TargetUrl = "/stores/1",
                Latitude = 45.4642m, Longitude = 9.19m, RadiusKm = 10, City = "Milano", StartsAt = _now.AddDays(-1), EndsAt = _now.AddDays(6) });
        await db.SaveChangesAsync();
        (_adult, _minor, _premium, _storeOwner) = (adult.Id, minor.Id, premium.Id, owner.Id);
    }

    private async Task<HashSet<string>> TitlesSeenAsync(int userId, int tries)
    {
        var seen = new HashSet<string>();
        for (var i = 0; i < tries; i++)
        {
            await using var db = NewContext();
            var v = await Service(db).PickAsync(userId, SlotPlacement.HomeFeed, null, null, null, _now);
            if (v != null) seen.Add(v.Slot.Title);
        }
        return seen;
    }

    [Fact]
    public async Task Minor_SeesOnlySlotsWithoutZone()
    {
        await SeedAsync();
        (await TitlesSeenAsync(_minor, 6)).Should().BeEquivalentTo(new[] { "Marca" });
    }

    [Fact]
    public async Task Premium_DoesNotSeeBrandSponsors_ButSeesLocalPromotions()
    {
        await SeedAsync();
        (await TitlesSeenAsync(_premium, 6)).Should().BeEquivalentTo(new[] { "Negozio vicino" });
    }

    [Fact]
    public async Task FrequencyCap_ThreePerSlotPerDay_AndImpressionsDeduplicated()
    {
        await SeedAsync();
        (await TitlesSeenAsync(_adult, 10)).Should().BeEquivalentTo(new[] { "Marca", "Negozio vicino" });
        await using var db = NewContext();
        // Due spazi × 3 al giorno: poi più niente
        (await Service(db).PickAsync(_adult, SlotPlacement.HomeFeed, null, null, null, _now)).Should().BeNull();
        (await db.SponsoredSlots.SumAsync(s => s.Impressions)).Should().Be(2);
        (await db.SlotImpressions.SumAsync(i => i.Count)).Should().Be(6);
    }

    [Fact]
    public async Task OwnSlot_IsNotShownToTheOwner_AndWhyExplainsZone()
    {
        await SeedAsync();
        (await TitlesSeenAsync(_storeOwner, 4)).Should().NotContain("Negozio vicino");
        await using var db = NewContext();
        var v = await Service(db).PickAsync(_premium, SlotPlacement.HomeFeed, null, null, null, _now);
        v!.Why.Zone.Should().Be("Zona");
        v.Why.City.Should().Be("Milano");
        v.Why.RadiusKm.Should().Be(10);
    }

    [Fact]
    public void CityTradeNights_CoverAllTradeNightsOfTheCityInPeriod_UntilExpiry()
    {
        var ship = new Sponsorship { Scope = SponsorshipScope.CityTradeNights, ScopeRef = "Milano", StartsAt = _now.AddDays(-1), EndsAt = _now.AddDays(30) };
        Event Ev(EventType type, string city, int days) => new() { Id = 7, Type = type, City = city, StartDate = _now.AddDays(days) };

        SponsoredContent.Covers(ship, Ev(EventType.Trademeetup, "milano", 3), _now).Should().BeTrue();
        SponsoredContent.Covers(ship, Ev(EventType.Tournament, "Milano", 3), _now).Should().BeFalse();
        SponsoredContent.Covers(ship, Ev(EventType.Trademeetup, "Torino", 3), _now).Should().BeFalse();
        SponsoredContent.Covers(ship, Ev(EventType.Trademeetup, "Milano", 40), _now).Should().BeFalse();
        // Scaduta: sparisce
        SponsoredContent.Covers(ship, Ev(EventType.Trademeetup, "Milano", 3), _now.AddDays(31)).Should().BeFalse();
    }
}
