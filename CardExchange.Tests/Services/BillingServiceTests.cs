using CardExchange.API.Services;
using CardExchange.API.Services.Billing;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardExchange.Tests.Services;

public class BillingServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<INotificationService> _notifications = new();
    private readonly BillingService _billing;
    private readonly User _user;
    private readonly SubscriptionPlan _monthly;
    private readonly SubscriptionPlan _annual;
    private readonly SubscriptionPlan _store;

    public BillingServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new ApplicationDbContext(options);
        _context.Roles.Add(new Role { Name = "PremiumUser" });
        _user = new User { Email = "a@t.it", Username = "anna", FirstName = "Anna", PasswordHash = "x" };
        _context.Users.Add(_user);
        _monthly = new SubscriptionPlan { Name = "Premium Mensile", Tier = SubscriptionTier.Premium, Cycle = BillingCycle.Monthly, Price = 4.99m };
        _annual = new SubscriptionPlan { Name = "Premium Annuale", Tier = SubscriptionTier.Premium, Cycle = BillingCycle.Annual, Price = 47.99m };
        _store = new SubscriptionPlan { Name = "Negozio Annuale", Tier = SubscriptionTier.Store, Cycle = BillingCycle.Annual, Price = 99m };
        _context.SubscriptionPlans.AddRange(_monthly, _annual, _store);
        _context.SaveChanges();
        _billing = new BillingService(_context, _notifications.Object, new Mock<ILogger<BillingService>>().Object);
    }

    public void Dispose() => _context.Dispose();

    private bool HasPremiumRole() => _context.UserRoles.Any(ur => ur.UserId == _user.Id && ur.Role.Name == "PremiumUser");

    [Fact]
    public async Task Activate_StartsNowAndGrantsPremiumRole()
    {
        var sub = await _billing.ActivateAsync(new ActivationRequest(_user.Id, _monthly, SubscriptionPaymentMethod.Manual, 4.99m));

        sub.StartDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        sub.EndDate.Should().BeCloseTo(DateTime.UtcNow.AddMonths(1), TimeSpan.FromSeconds(5));
        HasPremiumRole().Should().BeTrue();
    }

    [Fact]
    public async Task Activate_SameTierWithoutAutoRenew_QueuesAfterCurrentPeriod()
    {
        var first = await _billing.ActivateAsync(new ActivationRequest(_user.Id, _monthly, SubscriptionPaymentMethod.Manual, 4.99m));
        var second = await _billing.ActivateAsync(new ActivationRequest(_user.Id, _annual, SubscriptionPaymentMethod.Manual, 47.99m));

        second.StartDate.Should().Be(first.EndDate, "i giorni già pagati non si perdono");
        second.EndDate.Should().Be(first.EndDate.AddYears(1));
    }

    [Fact]
    public async Task Activate_DifferentTier_StartsImmediately()
    {
        await _billing.ActivateAsync(new ActivationRequest(_user.Id, _monthly, SubscriptionPaymentMethod.Manual, 4.99m));
        var store = await _billing.ActivateAsync(new ActivationRequest(_user.Id, _store, SubscriptionPaymentMethod.Manual, 99m));

        store.StartDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Activate_WithCustomEndDate_UsesIt()
    {
        var end = DateTime.UtcNow.AddDays(14);
        var sub = await _billing.ActivateAsync(new ActivationRequest(_user.Id, _store, SubscriptionPaymentMethod.Manual, 0m, EndDate: end));
        sub.EndDate.Should().Be(end);
    }

    [Fact]
    public async Task ConfirmPending_ActivatesAndQueuesOnSameTier()
    {
        var current = await _billing.ActivateAsync(new ActivationRequest(_user.Id, _monthly, SubscriptionPaymentMethod.Manual, 4.99m));
        var pending = new UserSubscription
        {
            UserId = _user.Id, PlanId = _annual.Id, Status = SubscriptionStatus.PendingPayment,
            PaymentMethod = SubscriptionPaymentMethod.BankTransfer, PaymentReference = "CX-1-ABCDEF"
        };
        _context.UserSubscriptions.Add(pending);
        await _context.SaveChangesAsync();

        var confirmed = await _billing.ConfirmPendingAsync(pending, adminId: 7, amountPaid: null, note: "bonifico ricevuto");

        confirmed.Status.Should().Be(SubscriptionStatus.Active);
        confirmed.StartDate.Should().Be(current.EndDate);
        confirmed.AmountPaid.Should().Be(47.99m);
        confirmed.ActivatedById.Should().Be(7);
        await FluentActions.Invoking(() => _billing.ConfirmPendingAsync(confirmed, 7, null, null))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Revoke_EndsNowAndRemovesRole()
    {
        var sub = await _billing.ActivateAsync(new ActivationRequest(_user.Id, _monthly, SubscriptionPaymentMethod.Manual, 4.99m));
        await _billing.RevokeAsync(sub, 7, "rimborso");

        sub.Status.Should().Be(SubscriptionStatus.Cancelled);
        sub.EndDate.Should().BeOnOrBefore(DateTime.UtcNow);
        sub.AdminNote.Should().Contain("rimborso");
        HasPremiumRole().Should().BeFalse();
    }

    [Fact]
    public async Task QueuedPeriodAlone_DoesNotGrantRole()
    {
        _context.UserSubscriptions.Add(new UserSubscription
        {
            UserId = _user.Id, PlanId = _monthly.Id, Status = SubscriptionStatus.Active,
            StartDate = DateTime.UtcNow.AddDays(3), EndDate = DateTime.UtcNow.AddDays(33)
        });
        await _context.SaveChangesAsync();

        await _billing.SyncPremiumRoleAsync(_user.Id);
        HasPremiumRole().Should().BeFalse("il periodo non è ancora iniziato");
    }

    [Fact]
    public async Task Maintenance_ExpiresRemindsAndCleansUp()
    {
        var now = DateTime.UtcNow.AddDays(40);
        var expired = new UserSubscription { UserId = _user.Id, PlanId = _monthly.Id, Status = SubscriptionStatus.Active,
            StartDate = now.AddDays(-31), EndDate = now.AddHours(-1) };
        var expiring = new UserSubscription { UserId = _user.Id, PlanId = _store.Id, Status = SubscriptionStatus.Active,
            StartDate = now.AddDays(-360), EndDate = now.AddDays(5) };
        var autoRenew = new UserSubscription { UserId = _user.Id, PlanId = _monthly.Id, Status = SubscriptionStatus.Active,
            StartDate = now.AddDays(-25), EndDate = now.AddDays(5), AutoRenew = true };
        var oldRequest = new UserSubscription { UserId = _user.Id, PlanId = _annual.Id, Status = SubscriptionStatus.PendingPayment };
        _context.UserSubscriptions.AddRange(expired, expiring, autoRenew, oldRequest);
        await _context.SaveChangesAsync(); // CreatedAt = adesso, cioè 40 giorni prima di "now"

        var result = await _billing.RunMaintenanceAsync(now);

        result.Should().Be((1, 1, 1));
        expired.Status.Should().Be(SubscriptionStatus.Expired);
        oldRequest.Status.Should().Be(SubscriptionStatus.Cancelled);
        expiring.ExpiryReminderSentAt.Should().Be(now);
        _notifications.Verify(n => n.SendAsync(_user.Id, NotificationType.SubscriptionExpiring, It.IsAny<string>(),
            It.IsAny<string?>(), null, "Subscription"), Times.Once);
        _notifications.Verify(n => n.SendAsync(_user.Id, NotificationType.SubscriptionExpired, It.IsAny<string>(),
            It.IsAny<string?>(), null, "Subscription"), Times.Once);

        // Seconda esecuzione: nessun promemoria duplicato
        (await _billing.RunMaintenanceAsync(now)).Should().Be((0, 0, 0));
    }

    [Fact]
    public async Task Maintenance_SkipsReminderWhenRenewalAlreadyQueued()
    {
        var now = DateTime.UtcNow;
        var current = new UserSubscription { UserId = _user.Id, PlanId = _monthly.Id, Status = SubscriptionStatus.Active,
            StartDate = now.AddDays(-25), EndDate = now.AddDays(5) };
        var queued = new UserSubscription { UserId = _user.Id, PlanId = _annual.Id, Status = SubscriptionStatus.Active,
            StartDate = now.AddDays(5), EndDate = now.AddDays(370) };
        _context.UserSubscriptions.AddRange(current, queued);
        await _context.SaveChangesAsync();

        await _billing.RunMaintenanceAsync(now);

        _notifications.Verify(n => n.SendAsync(It.IsAny<int>(), NotificationType.SubscriptionExpiring, It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void BankTransferReference_IsUniqueAndReadable()
    {
        var refs = Enumerable.Range(0, 200).Select(_ => _billing.NewBankTransferReference(42)).ToList();
        refs.Should().OnlyHaveUniqueItems();
        refs.Should().AllSatisfy(r => r.Should().MatchRegex("^CX-42-[A-HJKMNP-Z2-9]{6}$"));
    }
}
