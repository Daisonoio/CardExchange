using CardExchange.API.Services;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardExchange.Tests.Services;

public class ReferralRewardsTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Mock<INotificationService> _notifications = new();
    private readonly DateTime _now = DateTime.UtcNow;
    private SubscriptionPlan _premium = null!;
    private User _mario = null!;

    public void Dispose() => _db.Dispose();
    private ReferralRewards Service => new(_db, _notifications.Object, new Mock<ILogger<ReferralRewards>>().Object);

    private async Task SeedAsync()
    {
        _premium = new SubscriptionPlan { Name = "Premium", Tier = SubscriptionTier.Premium, Cycle = BillingCycle.Monthly, Price = 4.99m, IsActive = true };
        _db.SubscriptionPlans.Add(_premium);
        _mario = new User { Email = "mario@t.it", Username = "mario", PasswordHash = "x" };
        _db.Users.Add(_mario);
        await _db.SaveChangesAsync();
    }

    /// <summary>Invitato che soddisfa (o no) le condizioni: email confermata, 10 carte, 3 giorni di attività.</summary>
    private async Task<User> InviteeAsync(string name, bool confirmed = true, int cards = 10, int days = 3, string domain = "gmail.com", string? fingerprint = null)
    {
        var u = new User { Email = $"{name}@{domain}", Username = name, PasswordHash = "x", EmailConfirmed = confirmed, ReferredByUserId = _mario.Id, SignupFingerprint = fingerprint ?? name };
        _db.Users.Add(u);
        await _db.SaveChangesAsync();
        if (cards > 0) _db.Cards.Add(new Card { UserId = u.Id, CardInfoId = 1, Quantity = cards });
        for (var i = 0; i < days; i++) _db.UserActiveDays.Add(new UserActiveDay { UserId = u.Id, Day = DateOnly.FromDateTime(_now).AddDays(-i) });
        await _db.SaveChangesAsync();
        return u;
    }

    [Fact]
    public async Task ThreeActiveInvitees_GiveOnePremiumMonth_Once()
    {
        await SeedAsync();
        await InviteeAsync("a"); await InviteeAsync("b");
        await InviteeAsync("c", confirmed: false);
        await InviteeAsync("d", cards: 9);
        await InviteeAsync("e", days: 2);
        await InviteeAsync("f", domain: "mailinator.com");

        (await Service.RunAsync(_now)).Should().Be((2, 0));
        _notifications.Verify(n => n.SendAsync(_mario.Id, NotificationType.SystemAnnouncement, It.IsAny<string>(),
            It.Is<string>(b => b.Contains("ancora 1 invito")), null, "Referral"), Times.Once);

        await InviteeAsync("g");
        (await Service.RunAsync(_now)).Should().Be((1, 1));
        (await Service.RunAsync(_now)).Should().Be((0, 0));

        var sub = await _db.UserSubscriptions.SingleAsync();
        sub.Should().BeEquivalentTo(new { UserId = _mario.Id, PlanId = _premium.Id, PaymentMethod = SubscriptionPaymentMethod.Reward, AmountPaid = 0m });
        (sub.EndDate - sub.StartDate).TotalDays.Should().BeInRange(28, 31);
        (await Service.ProgressAsync(_mario.Id, _now)).Should().Be(new ReferralProgress(3, 0, 3, 1, 1, 6));
    }

    [Fact]
    public async Task SameFingerprint_CountsOnce()
    {
        await SeedAsync();
        await InviteeAsync("a", fingerprint: "same"); await InviteeAsync("b", fingerprint: "same"); await InviteeAsync("c", fingerprint: "same");
        await Service.RunAsync(_now);
        (await Service.CountQualifiedAsync(_mario.Id)).Should().Be(1);
        (await _db.UserSubscriptions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task YearlyCap_SixMonths()
    {
        await SeedAsync();
        for (var i = 0; i < 6; i++)
            _db.UserSubscriptions.Add(new UserSubscription { UserId = _mario.Id, PlanId = _premium.Id, PaymentMethod = SubscriptionPaymentMethod.Reward, StartDate = _now, EndDate = _now.AddMonths(1) });
        await _db.SaveChangesAsync();
        for (var i = 0; i < 21; i++) await InviteeAsync($"u{i}");   // 21 attivi = 7 mesi guadagnati
        (await Service.RunAsync(_now)).MonthsGranted.Should().Be(0);
        _notifications.Verify(n => n.SendAsync(_mario.Id, It.IsAny<NotificationType>(), It.IsAny<string>(),
            It.Is<string>(b => b.Contains("Hai già ricevuto i 6 mesi")), null, "Referral"), Times.Once);
    }

    [Fact]
    public void Rules()
    {
        ReferralRules.IsDisposableEmail("x@YopMail.com").Should().BeTrue();
        ReferralRules.IsDisposableEmail("x@gmail.com").Should().BeFalse();
        var day = new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc);
        ReferralRules.SignupFingerprint("1.2.3.4", day).Should().Be(ReferralRules.SignupFingerprint("1.2.3.4", day.AddHours(5)));
        ReferralRules.SignupFingerprint("1.2.3.4", day).Should().NotBe(ReferralRules.SignupFingerprint("1.2.3.4", day.AddDays(1)));
        ReferralRules.SignupFingerprint(null, day).Should().BeNull();
    }
}
