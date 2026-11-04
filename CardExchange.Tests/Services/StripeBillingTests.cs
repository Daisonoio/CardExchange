using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CardExchange.API.Configuration;
using CardExchange.API.Services;
using CardExchange.API.Services.Billing;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace CardExchange.Tests.Services;

public class StripeBillingTests : IDisposable
{
    private const string Secret = "whsec_test_secret";
    private readonly ApplicationDbContext _context;
    private readonly Mock<INotificationService> _notifications = new();
    private readonly StripeBilling _stripe;
    private readonly User _user;
    private readonly SubscriptionPlan _plan;
    private int _eventSeq;

    public StripeBillingTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new ApplicationDbContext(options);
        _context.Roles.Add(new Role { Name = "PremiumUser" });
        _user = new User { Email = "a@t.it", Username = "anna", FirstName = "Anna", PasswordHash = "x" };
        _plan = new SubscriptionPlan { Name = "Premium Mensile", Tier = SubscriptionTier.Premium, Cycle = BillingCycle.Monthly, Price = 4.99m };
        _context.Users.Add(_user);
        _context.SubscriptionPlans.Add(_plan);
        _context.SaveChanges();
        var billingOptions = Options.Create(new BillingOptions { Stripe = new StripeOptions { SecretKey = "sk_test_x", WebhookSecret = Secret } });
        var billing = new BillingService(_context, _notifications.Object, new Mock<ILogger<BillingService>>().Object);
        _stripe = new StripeBilling(_context, billing, _notifications.Object, billingOptions, new Mock<ILogger<StripeBilling>>().Object);
    }

    public void Dispose() => _context.Dispose();

    private static long Unix(DateTime d) => new DateTimeOffset(d).ToUnixTimeSeconds();

    private (string Json, string Signature) Signed(string type, object obj, string? eventId = null)
    {
        var json = JsonSerializer.Serialize(new
        {
            id = eventId ?? $"evt_{++_eventSeq}",
            @object = "event",
            api_version = "2025-01-01",
            created = Unix(DateTime.UtcNow),
            type,
            data = new { @object = obj }
        });
        var ts = Unix(DateTime.UtcNow).ToString();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var sig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{ts}.{json}"))).ToLowerInvariant();
        return (json, $"t={ts},v1={sig}");
    }

    private Task<WebhookResult> Send(string type, object obj, string? eventId = null)
    {
        var (json, sig) = Signed(type, obj, eventId);
        return _stripe.HandleWebhookAsync(json, sig);
    }

    private Dictionary<string, string> Meta() => new() { ["userId"] = _user.Id.ToString(), ["planId"] = _plan.Id.ToString() };

    private object Checkout() => new
    {
        id = "cs_1", @object = "checkout.session", mode = "subscription", subscription = "sub_1", customer = "cus_1",
        client_reference_id = _user.Id.ToString(), amount_total = 499, metadata = Meta()
    };

    private object Invoice(string id, string reason, DateTime periodEnd, long amount = 499) => new
    {
        id, @object = "invoice", customer = "cus_1", amount_paid = amount, billing_reason = reason,
        parent = new { type = "subscription_details", subscription_details = new { subscription = "sub_1", metadata = Meta() } },
        lines = new
        {
            @object = "list",
            data = new[] { new { id = "il_" + id, @object = "line_item", period = new { start = Unix(periodEnd.AddMonths(-1)), end = Unix(periodEnd) } } }
        }
    };

    private object Subscription(string status, bool cancelAtPeriodEnd, DateTime periodEnd) => new
    {
        id = "sub_1", @object = "subscription", status, cancel_at_period_end = cancelAtPeriodEnd, customer = "cus_1",
        items = new { @object = "list", data = new[] { new { id = "si_1", @object = "subscription_item", current_period_end = Unix(periodEnd) } } }
    };

    private UserSubscription Stored() => _context.UserSubscriptions.Single(s => s.ExternalSubscriptionId == "sub_1");

    [Fact]
    public async Task InvalidSignature_IsRejected()
    {
        var (json, _) = Signed("checkout.session.completed", Checkout());
        (await _stripe.HandleWebhookAsync(json, "t=1,v1=deadbeef")).Should().Be(WebhookResult.InvalidSignature);
        (await _stripe.HandleWebhookAsync(json, null)).Should().Be(WebhookResult.InvalidSignature);
        _context.UserSubscriptions.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckoutCompleted_ActivatesAutoRenewingSubscription_Once()
    {
        (await Send("checkout.session.completed", Checkout(), "evt_same")).Should().Be(WebhookResult.Processed);
        (await Send("checkout.session.completed", Checkout(), "evt_same")).Should().Be(WebhookResult.Duplicate);
        (await Send("checkout.session.completed", Checkout())).Should().Be(WebhookResult.Processed, "evento diverso, stesso abbonamento");

        var sub = Stored();
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.PaymentMethod.Should().Be(SubscriptionPaymentMethod.Stripe);
        sub.AutoRenew.Should().BeTrue();
        sub.ExternalCustomerId.Should().Be("cus_1");
        sub.AmountPaid.Should().Be(4.99m);
        _context.UserRoles.Any(r => r.UserId == _user.Id).Should().BeTrue();
    }

    [Fact]
    public async Task InvoicePaidBeforeCheckout_ActivatesWithStripePeriod()
    {
        var end = DateTime.UtcNow.Date.AddMonths(1).AddHours(10);
        (await Send("invoice.paid", Invoice("in_1", "subscription_create", end))).Should().Be(WebhookResult.Processed);
        (await Send("checkout.session.completed", Checkout())).Should().Be(WebhookResult.Processed);

        _context.UserSubscriptions.Count().Should().Be(1);
        Stored().EndDate.Should().Be(end);
    }

    [Fact]
    public async Task Renewal_ExtendsPeriodAndAddsAmount()
    {
        await Send("checkout.session.completed", Checkout());
        var firstEnd = DateTime.UtcNow.Date.AddMonths(1);
        await Send("invoice.paid", Invoice("in_1", "subscription_create", firstEnd));
        Stored().AmountPaid.Should().Be(4.99m, "la prima fattura è già contata nel checkout");

        var nextEnd = firstEnd.AddMonths(1);
        await Send("invoice.paid", Invoice("in_2", "subscription_cycle", nextEnd));

        var sub = Stored();
        sub.EndDate.Should().Be(nextEnd);
        sub.AmountPaid.Should().Be(9.98m);
        sub.PaymentReference.Should().Be("in_2");
    }

    [Fact]
    public async Task CancelAtPeriodEnd_ThenDeleted_ExpiresAndRemovesRole()
    {
        await Send("checkout.session.completed", Checkout());
        var end = DateTime.UtcNow.AddDays(20);

        await Send("customer.subscription.updated", Subscription("active", cancelAtPeriodEnd: true, end));
        Stored().AutoRenew.Should().BeFalse();
        Stored().CancelledAt.Should().NotBeNull();

        await Send("customer.subscription.updated", Subscription("active", cancelAtPeriodEnd: false, end));
        Stored().AutoRenew.Should().BeTrue("riattivato dal portale");

        await Send("customer.subscription.deleted", Subscription("canceled", false, end));
        var sub = Stored();
        sub.Status.Should().Be(SubscriptionStatus.Expired);
        sub.EndDate.Should().BeOnOrBefore(DateTime.UtcNow);
        _context.UserRoles.Any(r => r.UserId == _user.Id).Should().BeFalse();
    }

    [Fact]
    public async Task PaymentFailed_NotifiesUser()
    {
        await Send("checkout.session.completed", Checkout());
        (await Send("invoice.payment_failed", Invoice("in_3", "subscription_cycle", DateTime.UtcNow.AddMonths(1), 0)))
            .Should().Be(WebhookResult.Processed);
        _notifications.Verify(n => n.SendAsync(_user.Id, NotificationType.SubscriptionExpiring, "Pagamento non riuscito",
            It.IsAny<string?>(), null, "Subscription"), Times.Once);
    }

    [Fact]
    public async Task UnknownEvents_AreAcknowledgedButIgnored()
    {
        (await Send("customer.created", new { id = "cus_9", @object = "customer" })).Should().Be(WebhookResult.Ignored);
        (await Send("invoice.paid", new { id = "in_x", @object = "invoice", amount_paid = 100 })).Should().Be(WebhookResult.Ignored);
    }
}
