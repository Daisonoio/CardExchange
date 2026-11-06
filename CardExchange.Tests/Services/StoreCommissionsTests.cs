using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CardExchange.API.Configuration;
using CardExchange.API.Services;
using CardExchange.API.Services.Billing;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace CardExchange.Tests.Services;

public class StoreCommissionsTests : IDisposable
{
    private const string Secret = "whsec_test_secret";
    private readonly ApplicationDbContext _db;
    private readonly Mock<INotificationService> _notifications = new();
    private readonly StoreCommissions _commissions;
    private readonly StripeBilling _stripe;
    private readonly SubscriptionPlan _premium, _store;
    private readonly User _owner;
    private readonly Store _shop;
    private int _seq;

    public StoreCommissionsTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.Roles.Add(new Role { Name = "PremiumUser" });
        _premium = PlanCatalog.All().First(p => p.Name == "Premium Mensile");
        _store = PlanCatalog.All().First(p => p.Name == "Negozio");
        _db.SubscriptionPlans.AddRange(_premium, _store);
        _owner = new User { Email = "o@t.it", Username = "owner", PasswordHash = "x" };
        _db.Users.Add(_owner);
        _db.SaveChanges();
        _shop = new Store { Name = "Games Corner", City = "Milano", Address = "x", OwnerId = _owner.Id, Status = StoreStatus.Verified };
        _db.Stores.Add(_shop);
        _db.SaveChanges();
        _commissions = new StoreCommissions(_db, _notifications.Object, new Mock<ILogger<StoreCommissions>>().Object);
        var options = Options.Create(new BillingOptions { Stripe = new StripeOptions { SecretKey = "sk_test_x", WebhookSecret = Secret } });
        _stripe = new StripeBilling(_db, new BillingService(_db, _notifications.Object, new Mock<ILogger<BillingService>>().Object, _commissions),
            _notifications.Object, options, new Mock<ILogger<StripeBilling>>().Object, null, _commissions);
    }

    public void Dispose() => _db.Dispose();

    private async Task<User> UserAsync(string name, AttributionSource? source = AttributionSource.Qr, int attributedMonthsAgo = 1)
    {
        var u = new User { Email = $"{name}@t.it", Username = name, PasswordHash = "x" };
        _db.Users.Add(u);
        await _db.SaveChangesAsync();
        if (source != null)
        {
            var a = new StoreAttribution { UserId = u.Id, StoreId = _shop.Id, Source = source.Value };
            _db.StoreAttributions.Add(a);
            await _db.SaveChangesAsync();
            a.CreatedAt = DateTime.UtcNow.AddMonths(-attributedMonthsAgo);
            await _db.SaveChangesAsync();
        }
        return u;
    }

    private Task<WebhookResult> Send(string type, object obj, string? eventId = null)
    {
        var now = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        var json = JsonSerializer.Serialize(new { id = eventId ?? $"evt_c{++_seq}", @object = "event", api_version = "2025-01-01", created = now, type, data = new { @object = obj } });
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var sig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{now}.{json}"))).ToLowerInvariant();
        return _stripe.HandleWebhookAsync(json, $"t={now},v1={sig}");
    }

    /// <summary>Fattura pagata: 4,99 € con IVA, 4,09 € senza.</summary>
    private static object Invoice(string id, string sub, int userId, int planId, string reason = "subscription_create", string pi = "pi_sub_1") => new
    {
        id, @object = "invoice", customer = "cus_" + userId, amount_paid = 499, total_excluding_tax = 409, billing_reason = reason,
        parent = new { type = "subscription_details", subscription_details = new { subscription = sub,
            metadata = new Dictionary<string, string> { ["userId"] = userId.ToString(), ["planId"] = planId.ToString() } } },
        lines = new { @object = "list", data = new[] { new { id = "il_" + id, @object = "line_item", period = new { start = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds(), end = new DateTimeOffset(DateTime.UtcNow.AddMonths(1)).ToUnixTimeSeconds() } } } },
        payments = new { @object = "list", data = new[] { new { id = "inpay_" + id, @object = "invoice_payment", payment = new { type = "payment_intent", payment_intent = pi } } } }
    };

    [Fact]
    public async Task PremiumOfAttributedUser_EarnsQuarterOfNet_Once_RenewalsToo()
    {
        var u = await UserAsync("anna");
        await Send("invoice.paid", Invoice("in_1", "sub_1", u.Id, _premium.Id), "evt_dup");
        await Send("invoice.paid", Invoice("in_1", "sub_1", u.Id, _premium.Id), "evt_dup");
        await Send("invoice.paid", Invoice("in_2", "sub_1", u.Id, _premium.Id, "subscription_cycle", "pi_sub_2"));

        var list = await _db.StoreCommissions.OrderBy(c => c.Id).ToListAsync();
        list.Should().HaveCount(2);
        list[0].Should().Match<StoreCommission>(c => c.StoreId == _shop.Id && c.GrossEur == 4.09m && c.AmountEur == 1.02m
            && c.Status == CommissionStatus.Pending && c.PaymentIntentId == "pi_sub_1");
    }

    [Fact]
    public async Task NoCommission_WithoutQrAttribution_AfterTwelveMonths_OrOnStorePlans()
    {
        var none = await UserAsync("none", source: null);
        var manual = await UserAsync("manual", AttributionSource.Manual);
        var old = await UserAsync("old", attributedMonthsAgo: 13);
        var shopkeeper = await UserAsync("shop");
        await Send("invoice.paid", Invoice("in_a", "sub_a", none.Id, _premium.Id));
        await Send("invoice.paid", Invoice("in_b", "sub_b", manual.Id, _premium.Id));
        await Send("invoice.paid", Invoice("in_c", "sub_c", old.Id, _premium.Id));
        await Send("invoice.paid", Invoice("in_d", "sub_d", shopkeeper.Id, _store.Id));
        (await _db.StoreCommissions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Refund_Reverses_Otherwise_ApprovedAfter30Days_AndCreditedOnStripe()
    {
        var a = await UserAsync("anna");
        var b = await UserAsync("bruno");
        await Send("invoice.paid", Invoice("in_1", "sub_1", a.Id, _premium.Id, pi: "pi_a"));
        await Send("invoice.paid", Invoice("in_2", "sub_2", b.Id, _premium.Id, pi: "pi_b"));
        await Send("charge.refunded", new { id = "ch_b", @object = "charge", payment_intent = "pi_b", amount = 499, amount_refunded = 499, refunded = true });

        (await _commissions.ApproveAsync(DateTime.UtcNow.AddDays(10))).Should().Be(0, "30 giorni per eventuali rimborsi");
        (await _commissions.ApproveAsync(DateTime.UtcNow.AddDays(31))).Should().Be(1);

        // Il proprietario paga il piano con Stripe: lo sconto va sul suo conto
        _db.UserSubscriptions.Add(new UserSubscription { UserId = _owner.Id, PlanId = _store.Id, Status = SubscriptionStatus.Active,
            StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddMonths(1), ExternalCustomerId = "cus_owner" });
        await _db.SaveChangesAsync();
        var stripe = new Mock<IStripeBilling>();
        stripe.SetupGet(s => s.IsEnabled).Returns(true);
        stripe.Setup(s => s.CreditCustomerAsync("cus_owner", 1.02m, It.IsAny<string>())).ReturnsAsync("cbtxn_1");

        var payouts = await _commissions.PayoutAsync(DateTime.UtcNow.AddDays(31), stripe.Object);
        payouts.Should().ContainSingle().Which.Should().Match<StorePayout>(p => p.AmountEur == 1.02m && p.Status == PayoutStatus.Done && p.Reference == "cbtxn_1");
        var statuses = await _db.StoreCommissions.OrderBy(c => c.Id).Select(c => c.Status).ToListAsync();
        statuses.Should().Equal(CommissionStatus.Credited, CommissionStatus.Reversed);
        (await _commissions.PayoutAsync(DateTime.UtcNow.AddDays(32), stripe.Object)).Should().BeEmpty("niente da liquidare due volte");

        var summary = await _commissions.SummaryAsync(_shop.Id, DateTime.UtcNow);
        summary.CreditedEur.Should().Be(1.02m);
    }
}
