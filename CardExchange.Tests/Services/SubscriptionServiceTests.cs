using CardExchange.API.Services;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.Tests.Services;

public class SubscriptionServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly SubscriptionService _service;

    public SubscriptionServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _service = new SubscriptionService(_context);
    }

    [Fact]
    public async Task HasActiveSubscriptionAsync_WithNoSubscription_ReturnsFalse()
    {
        var result = await _service.HasActiveSubscriptionAsync(1);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasActiveSubscriptionAsync_WithActiveSubscription_ReturnsTrue()
    {
        // Arrange
        var plan = new SubscriptionPlan
        {
            Name = "Premium Monthly",
            Tier = SubscriptionTier.Premium,
            Cycle = BillingCycle.Monthly,
            Price = 9.99m,
            MaxCards = 999,
            MaxWishlistItems = 999,
            MaxActiveTradeOffers = 999,
            MaxDailyMessages = 999
        };
        _context.SubscriptionPlans.Add(plan);
        await _context.SaveChangesAsync();

        _context.UserSubscriptions.Add(new UserSubscription
        {
            UserId = 1,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            StartDate = DateTime.UtcNow.AddDays(-10),
            EndDate = DateTime.UtcNow.AddDays(20)
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.HasActiveSubscriptionAsync(1);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasActiveSubscriptionAsync_WithExpiredSubscription_ReturnsFalse()
    {
        var plan = new SubscriptionPlan
        {
            Name = "Premium",
            Tier = SubscriptionTier.Premium,
            Cycle = BillingCycle.Monthly,
            Price = 9.99m,
            MaxCards = 999,
            MaxWishlistItems = 999,
            MaxActiveTradeOffers = 999,
            MaxDailyMessages = 999
        };
        _context.SubscriptionPlans.Add(plan);
        await _context.SaveChangesAsync();

        _context.UserSubscriptions.Add(new UserSubscription
        {
            UserId = 2,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            StartDate = DateTime.UtcNow.AddDays(-40),
            EndDate = DateTime.UtcNow.AddDays(-10) // Expired
        });
        await _context.SaveChangesAsync();

        var result = await _service.HasActiveSubscriptionAsync(2);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserActivePlanAsync_WithNoSubscription_ReturnsFreePlan()
    {
        var plan = await _service.GetUserActivePlanAsync(999);

        plan.Should().NotBeNull();
        plan!.Tier.Should().Be(SubscriptionTier.Free);
        plan.MaxCards.Should().Be(int.MaxValue, "di default la collezione del piano gratuito è illimitata");
        plan.MaxWishlistItems.Should().Be(20);
        plan.MaxDailyMessages.Should().Be(10);
    }

    [Fact]
    public async Task CheckLimitAsync_FreePlanByDefault_HasNoCardLimit()
    {
        (await _service.CheckLimitAsync(1, "cards", 10)).Should().BeTrue();
        (await _service.CheckLimitAsync(1, "cards", 100_000)).Should().BeTrue();
    }

    [Theory]
    [InlineData(1000, 999, true)]
    [InlineData(1000, 1000, false)]
    [InlineData(0, 100_000, true)] // 0 = nessun limite
    public async Task CheckLimitAsync_ConfiguredFreeCardLimit_IsEnforced(int configuredLimit, int currentCount, bool expected)
    {
        var service = new SubscriptionService(_context,
            Microsoft.Extensions.Options.Options.Create(new CardExchange.API.Configuration.PlanLimitsOptions { FreeMaxCards = configuredLimit }));

        (await service.CheckLimitAsync(1, "cards", currentCount)).Should().Be(expected);
    }

    [Fact]
    public async Task CheckLimitAsync_FreePlanOtherLimits_StillApply()
    {
        (await _service.CheckLimitAsync(1, "wishlist", 20)).Should().BeFalse();
        (await _service.CheckLimitAsync(1, "trades", 4)).Should().BeTrue();
    }

    [Fact]
    public async Task CheckLimitAsync_UnknownLimitType_ReturnsFalse()
    {
        var result = await _service.CheckLimitAsync(1, "unknown", 0);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CheckLimitAsync_WishlistBelowLimit_ReturnsTrue()
    {
        var result = await _service.CheckLimitAsync(1, "wishlist", 5);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckLimitAsync_MessagesAtLimit_ReturnsFalse()
    {
        var result = await _service.CheckLimitAsync(1, "messages", 10);
        result.Should().BeFalse();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
