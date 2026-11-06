using CardExchange.API.Services.Entitlements;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CardExchange.Tests.Services;

public class EntitlementServiceTests : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly ApplicationDbContext _db;
    private readonly DateTime _now = DateTime.UtcNow;

    public EntitlementServiceTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new EntitlementCacheInterceptor(_cache)).Options);
        _db.SubscriptionPlans.AddRange(PlanCatalog.All());
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _cache.Dispose();
    }

    private EntitlementService Service => new(_db, _cache);

    private async Task<int> UserWithAsync(params string[] plans)
    {
        var u = new User { Email = $"{Guid.NewGuid():N}@t.it", Username = Guid.NewGuid().ToString("N")[..10], PasswordHash = "x" };
        _db.Users.Add(u);
        await _db.SaveChangesAsync();
        foreach (var name in plans)
            _db.UserSubscriptions.Add(new UserSubscription { UserId = u.Id, PlanId = _db.SubscriptionPlans.Single(p => p.Name == name).Id,
                Status = SubscriptionStatus.Active, StartDate = _now.AddDays(-1), EndDate = _now.AddDays(30) });
        await _db.SaveChangesAsync();
        return u.Id;
    }

    [Fact]
    public async Task EachPlan_HasItsRightsAndLimits()
    {
        var free = await Service.GetAsync(await UserWithAsync());
        var premium = await Service.GetAsync(await UserWithAsync("Premium Mensile"));
        var seller = await Service.GetAsync(await UserWithAsync("Banco"));
        var store = await Service.GetAsync(await UserWithAsync("Negozio"));
        var pro = await Service.GetAsync(await UserWithAsync("Negozio Pro"));

        free.Should().Match<UserEntitlements>(e => e.Tier == SubscriptionTier.Free && !e.IsPaid && e.Capabilities.Count == 0);
        (free.Limit(PlanLimit.RadiusKm), free.Limit(PlanLimit.BroadcastsPerDay), free.Limit(PlanLimit.WishlistItems)).Should().Be((30, 3, 20));

        premium.Has(Capability.InstantAlerts).Should().BeTrue();
        premium.Has(Capability.FeaturedStore).Should().BeFalse();
        (premium.Limit(PlanLimit.RadiusKm), premium.Limit(PlanLimit.ActiveReservations)).Should().Be((200, 5));

        seller.Has(Capability.SellerShowcase).Should().BeTrue();
        (seller.Limit(PlanLimit.BroadcastsPerDay), seller.Limit(PlanLimit.MonthlyPromotionCredits)).Should().Be((8, 2));

        store.Has(Capability.FeaturedStore).Should().BeTrue();
        store.Has(Capability.LocalDemand).Should().BeFalse("la domanda locale è del Negozio Pro");
        store.Has(Capability.Buylist).Should().BeFalse();

        pro.Has(Capability.LocalDemand).Should().BeTrue();
        pro.Has(Capability.Buylist).Should().BeTrue();
        pro.Has(Capability.Reservations).Should().BeTrue();
        pro.Limit(PlanLimit.MonthlyPromotionCredits).Should().Be(4);
        pro.Badge.Should().Be("Store");
        seller.Badge.Should().Be("Premium");
    }

    [Fact]
    public async Task TwoActivePlans_AreCombined_AndExpiredPlansDoNotCount()
    {
        var id = await UserWithAsync("Banco", "Negozio");
        var e = await Service.GetAsync(id);
        e.Tier.Should().Be(SubscriptionTier.Store);
        e.Has(Capability.SellerShowcase).Should().BeTrue();
        e.Has(Capability.FeaturedStore).Should().BeTrue();

        var expired = await UserWithAsync();
        _db.UserSubscriptions.Add(new UserSubscription { UserId = expired, PlanId = _db.SubscriptionPlans.Single(p => p.Name == "Negozio Pro").Id,
            Status = SubscriptionStatus.Active, StartDate = _now.AddDays(-40), EndDate = _now.AddDays(-1) });
        await _db.SaveChangesAsync();
        (await Service.HasAsync(expired, Capability.LocalDemand)).Should().BeFalse();
    }

    [Fact]
    public async Task ChangingASubscription_RefreshesTheCache()
    {
        var id = await UserWithAsync("Negozio");
        (await Service.HasAsync(id, Capability.Buylist)).Should().BeFalse();

        // Passaggio al Negozio Pro: il salvataggio svuota la cache di quell'utente
        _db.UserSubscriptions.Add(new UserSubscription { UserId = id, PlanId = _db.SubscriptionPlans.Single(p => p.Name == "Negozio Pro").Id,
            Status = SubscriptionStatus.Active, StartDate = _now.AddDays(-1), EndDate = _now.AddDays(30) });
        await _db.SaveChangesAsync();
        (await Service.HasAsync(id, Capability.Buylist)).Should().BeTrue();

        // Senza passare dal salvataggio la cache resta (5 minuti)
        _cache.Set(EntitlementService.Key(id), UserEntitlements.Combine(PlanCatalog.Free(int.MaxValue), Array.Empty<SubscriptionPlan>()));
        (await Service.HasAsync(id, Capability.Buylist)).Should().BeFalse();
        Service.Invalidate(id);
        (await Service.HasAsync(id, Capability.Buylist)).Should().BeTrue();
    }

    [Fact]
    public void OldAnnualStorePlan_IsNoLongerSold_ButKeepsStoreRights()
    {
        var old = PlanCatalog.All().Single(p => p.Name == "Negozio Annuale");
        old.IsActive.Should().BeFalse();
        UserEntitlements.CapabilitiesOf(old).Should().Contain(Capability.FeaturedStore).And.NotContain(Capability.LocalDemand);
    }
}
