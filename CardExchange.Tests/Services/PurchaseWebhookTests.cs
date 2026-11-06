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

public class PurchaseWebhookTests : IDisposable
{
    private const string Secret = "whsec_test_secret";
    private readonly ApplicationDbContext _db;
    private readonly Mock<INotificationService> _notifications = new();
    private readonly StripeBilling _stripe;
    private readonly PurchaseService _purchases;
    private readonly User _user;
    private int _seq;

    public PurchaseWebhookTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _user = new User { Email = "a@t.it", Username = "anna", PasswordHash = "x" };
        _db.Users.Add(_user);
        _db.SaveChanges();
        _purchases = new PurchaseService(_db, _notifications.Object, new Mock<ILogger<PurchaseService>>().Object);
        var options = Options.Create(new BillingOptions { Stripe = new StripeOptions { SecretKey = "sk_test_x", WebhookSecret = Secret } });
        _stripe = new StripeBilling(_db, new BillingService(_db, _notifications.Object, new Mock<ILogger<BillingService>>().Object),
            _notifications.Object, options, new Mock<ILogger<StripeBilling>>().Object, _purchases);
    }

    public void Dispose() => _db.Dispose();

    private Task<WebhookResult> Send(string type, object obj, string? eventId = null)
    {
        var now = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        var json = JsonSerializer.Serialize(new { id = eventId ?? $"evt_p{++_seq}", @object = "event", api_version = "2025-01-01", created = now, type, data = new { @object = obj } });
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var sig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{now}.{json}"))).ToLowerInvariant();
        return _stripe.HandleWebhookAsync(json, $"t={now},v1={sig}");
    }

    private object Session(int purchaseId, string paymentStatus = "paid") => new
    {
        id = "cs_pay_1", @object = "checkout.session", mode = "payment", payment_status = paymentStatus, payment_intent = "pi_1",
        amount_total = 2000, client_reference_id = _user.Id.ToString(),
        metadata = new Dictionary<string, string> { ["userId"] = _user.Id.ToString(), ["purchaseId"] = purchaseId.ToString() }
    };

    private static object Charge(long refunded, bool full) => new
    {
        id = "ch_1", @object = "charge", payment_intent = "pi_1", amount = 2000, amount_refunded = refunded, refunded = full
    };

    [Fact]
    public async Task Paid_ActivatesOnce_EvenWithRepeatedEvents()
    {
        var p = await _purchases.CreatePendingAsync(_user.Id, "credits-5");
        (await _purchases.CreditBalanceAsync(_user.Id)).Should().Be(0, "il ritorno dal browser non attiva nulla");

        (await Send("checkout.session.completed", Session(p.Id), "evt_same")).Should().Be(WebhookResult.Processed);
        (await Send("checkout.session.completed", Session(p.Id), "evt_same")).Should().Be(WebhookResult.Duplicate);
        (await Send("checkout.session.async_payment_succeeded", Session(p.Id))).Should().Be(WebhookResult.Processed);

        (await _purchases.CreditBalanceAsync(_user.Id)).Should().Be(5);
        var saved = await _db.Purchases.SingleAsync();
        (saved.Status, saved.StripePaymentIntentId).Should().Be((PurchaseStatus.Paid, "pi_1"));
    }

    [Fact]
    public async Task DelayedPayment_ActivatesOnAsyncSuccess()
    {
        var p = await _purchases.CreatePendingAsync(_user.Id, "credits-15");
        await Send("checkout.session.completed", Session(p.Id, "unpaid"));
        (await _purchases.CreditBalanceAsync(_user.Id)).Should().Be(0);
        await Send("checkout.session.async_payment_succeeded", Session(p.Id));
        (await _purchases.CreditBalanceAsync(_user.Id)).Should().Be(15);
    }

    [Fact]
    public async Task Refund_RemovesOnlyUnusedCredits()
    {
        var p = await _purchases.CreatePendingAsync(_user.Id, "credits-5");
        await Send("checkout.session.completed", Session(p.Id));
        // L'utente ha già usato 3 crediti
        _db.PromotionCreditEntries.Add(new PromotionCreditEntry { UserId = _user.Id, Delta = -3, Reason = "Promozione", CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        (await Send("charge.refunded", Charge(800, full: false))).Should().Be(WebhookResult.Processed);   // 40% → 2 crediti
        (await _purchases.CreditBalanceAsync(_user.Id)).Should().Be(0);
        (await _db.Purchases.SingleAsync()).Status.Should().Be(PurchaseStatus.Paid, "rimborso parziale");

        await Send("charge.refunded", Charge(2000, full: true));
        (await _purchases.CreditBalanceAsync(_user.Id)).Should().Be(0, "mai sotto zero");
        (await _db.Purchases.SingleAsync()).Status.Should().Be(PurchaseStatus.Refunded);
    }

    [Fact]
    public async Task ExpiredSession_MarksPurchaseExpired()
    {
        var p = await _purchases.CreatePendingAsync(_user.Id, "credits-5");
        await Send("checkout.session.expired", Session(p.Id, "unpaid"));
        (await _db.Purchases.SingleAsync()).Status.Should().Be(PurchaseStatus.Expired);
    }
}
