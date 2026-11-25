using CardExchange.API.Services;
using CardExchange.API.Services.Moderation;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardExchange.Tests.Services;

public class ChatSafetyRulesTests
{
    [Theory]
    [InlineData("chiamami al 333 123 4567", ChatSignal.Contact)]
    [InlineData("3331234567", ChatSignal.Contact)]
    [InlineData("scrivimi su WhatsApp", ChatSignal.Contact)]
    [InlineData("ci sentiamo su telegram?", ChatSignal.Contact)]
    [InlineData("mi trovi come @mario_88", ChatSignal.Contact)]
    [InlineData("mario.rossi@gmail.com", ChatSignal.Contact)]
    [InlineData("guarda www.miosito.it", ChatSignal.Link)]
    [InlineData("Quanti anni hai?", ChatSignal.Grooming)]
    [InlineData("non dirlo ai tuoi genitori", ChatSignal.Grooming)]
    [InlineData("mandami una foto", ChatSignal.Grooming)]
    [InlineData("ti vengo a prendere io", ChatSignal.Grooming)]
    [InlineData("hai della droga?", ChatSignal.Illegal)]
    [InlineData("ti faccio una ricarica postepay", ChatSignal.Payment)]
    public void Detects_RiskyMessages(string text, ChatSignal expected) =>
        ChatSafetyRules.Detect(text).HasFlag(expected).Should().BeTrue(text);

    [Theory]
    [InlineData("Ciao! Ti va lo scambio del Charizard 4/102?")]
    [InlineData("Ho la OP01-001 e la 025/165, prezzo 12,50 €")]
    [InlineData("Ci vediamo sabato alle 16 da Games Corner?")]
    [InlineData("La carta è in condizioni Near Mint, set Base Set 2")]
    public void Ignores_NormalTradeMessages(string text) =>
        ChatSafetyRules.Detect(text).Should().Be(ChatSignal.None);
}

/// <summary>Regole della chat su SQLite vero (la sospensione usa un aggiornamento condizionale).</summary>
public class ChatSafetyTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"safety-{Guid.NewGuid():N}.db");
    private readonly Mock<INotificationService> _notifications = new();
    // 12:00 e 23:30 ora italiana
    private static readonly DateTime Noon = new(2026, 11, 25, 11, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Night = new(2026, 11, 25, 22, 30, 0, DateTimeKind.Utc);
    private int _adult, _adult2, _minor, _storeId;

    public ChatSafetyTests()
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

    private ChatSafety Service(ApplicationDbContext db) => new(db, _notifications.Object, new Mock<ILogger<ChatSafety>>().Object);

    private async Task SeedAsync(bool trade = true, int extraMinors = 0)
    {
        await using var db = NewContext();
        var today = DateOnly.FromDateTime(Noon);
        User U(string n, int age) => new() { Email = $"{n}@t.it", Username = n, PasswordHash = "x", BirthDate = today.AddYears(-age) };
        var adult = U("adulto", 35); var adult2 = U("adulta", 30); var minor = U("ragazzo", 15);
        db.Users.AddRange(adult, adult2, minor);
        for (var i = 0; i < extraMinors; i++) db.Users.Add(U($"minore{i}", 15));
        await db.SaveChangesAsync();
        if (trade) db.TradeOffers.Add(new TradeOffer { SenderId = adult.Id, ReceiverId = minor.Id, Status = TradeOfferStatus.Pending });
        var store = new Store { Name = "Games Corner", City = "Milano", Address = "Via Roma 1", OwnerId = adult2.Id, Status = StoreStatus.Verified };
        db.Stores.Add(store);
        await db.SaveChangesAsync();
        (_adult, _adult2, _minor, _storeId) = (adult.Id, adult2.Id, minor.Id, store.Id);
    }

    [Fact]
    public async Task AdultToMinor_NeedsAnExistingTrade()
    {
        await SeedAsync(trade: false);
        await using var db = NewContext();
        (await Service(db).CheckMessageAsync(_adult, _minor, "Ciao, scambi?", null, Noon)).Code.Should().Be("TRADE_REQUIRED");
        // Tra adulti nessun vincolo
        (await Service(db).CheckMessageAsync(_adult, _adult2, "Ciao, scambi?", null, Noon)).Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task AdultToMinor_WithTrade_DaytimeOnly()
    {
        await SeedAsync();
        await using var db = NewContext();
        (await Service(db).CheckMessageAsync(_adult, _minor, "Ti va lo scambio del Pikachu?", null, Noon)).Allowed.Should().BeTrue();
        (await Service(db).CheckMessageAsync(_adult, _minor, "Ti va lo scambio?", null, Night)).Code.Should().Be("QUIET_HOURS");
        (await Service(db).CheckMessageAsync(_minor, _adult, "Sì!", null, Night)).Code.Should().Be("QUIET_HOURS");
        (await Service(db).CheckMessageAsync(_adult, _adult2, "Ti va lo scambio?", null, Night)).Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Contacts_AreBlockedForEveryone_FlaggedOnlyTowardMinors()
    {
        await SeedAsync();
        await using var db = NewContext();
        (await Service(db).CheckMessageAsync(_adult, _adult2, "scrivimi su whatsapp 3331234567", null, Noon)).Code.Should().Be("BLOCKED_CONTACT");
        (await db.SafetyFlags.CountAsync()).Should().Be(0);
        (await Service(db).CheckMessageAsync(_adult, _minor, "scrivimi su whatsapp", null, Noon)).Code.Should().Be("BLOCKED_CONTACT");
        (await db.SafetyFlags.SingleAsync()).Kind.Should().Be(SafetyFlagKind.ContactToMinor);
    }

    [Fact]
    public async Task Grooming_IsBlocked_AndRepeatedSevereFlagsSuspendTheChat()
    {
        await SeedAsync();
        await using (var db = NewContext())
        {
            var first = await Service(db).CheckMessageAsync(_adult, _minor, "quanti anni hai? sei sola a casa?", null, Noon);
            first.Code.Should().Be("BLOCKED_CONTENT");
            var second = await Service(db).CheckMessageAsync(_adult, _minor, "mandami una foto", null, Noon.AddMinutes(5));
            second.Code.Should().Be("BLOCKED_CONTENT");
        }
        await using var check = NewContext();
        (await check.SafetyFlags.CountAsync(f => f.Kind == SafetyFlagKind.Grooming && f.InvolvesMinor)).Should().Be(2);
        (await check.Users.SingleAsync(u => u.Id == _adult)).MessagingSuspendedUntil.Should().BeAfter(Noon);
        // Chat sospesa: anche i messaggi normali, anche verso altri adulti
        (await Service(check).CheckMessageAsync(_adult, _adult2, "Ciao", null, Noon.AddMinutes(10))).Code.Should().Be("CHAT_SUSPENDED");
    }

    [Fact]
    public async Task IllegalContent_IsBlockedAndFlagged_BetweenAdultsToo()
    {
        await SeedAsync();
        await using var db = NewContext();
        (await Service(db).CheckMessageAsync(_adult, _adult2, "ho anche della droga se vuoi", null, Noon)).Code.Should().Be("BLOCKED_CONTENT");
        (await db.SafetyFlags.SingleAsync()).Kind.Should().Be(SafetyFlagKind.Illegal);
    }

    [Fact]
    public async Task PaymentRequests_AreBlocked_OnlyWithMinors()
    {
        await SeedAsync();
        await using var db = NewContext();
        (await Service(db).CheckMessageAsync(_adult, _minor, "fammi una ricarica postepay", null, Noon)).Code.Should().Be("BLOCKED_PAYMENT");
        (await Service(db).CheckMessageAsync(_adult, _adult2, "ti faccio un bonifico", null, Noon)).Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task AdultContactingManyMinors_IsStoppedAndFlagged()
    {
        await SeedAsync(extraMinors: ChatSafety.MaxMinorsPerWeek);
        await using (var db = NewContext())
        {
            var minors = await db.Users.Where(u => u.Username.StartsWith("minore")).Select(u => u.Id).ToListAsync();
            foreach (var m in minors)
            {
                db.TradeOffers.Add(new TradeOffer { SenderId = _adult, ReceiverId = m, Status = TradeOfferStatus.Pending });
                var conv = new Conversation { User1Id = Math.Min(_adult, m), User2Id = Math.Max(_adult, m) };
                db.Conversations.Add(conv);
                await db.SaveChangesAsync();
                db.Messages.Add(new Message { ConversationId = conv.Id, SenderId = _adult, Content = "Ciao", CreatedAt = Noon.AddDays(-1) });
            }
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("UPDATE Messages SET CreatedAt = {0}", Noon.AddDays(-1));
        }
        await using var check = NewContext();
        (await Service(check).CheckMessageAsync(_adult, _minor, "Ciao, scambi?", null, Noon)).Code.Should().Be("TOO_MANY_CONTACTS");
        (await check.SafetyFlags.SingleAsync()).Kind.Should().Be(SafetyFlagKind.ManyMinors);
    }

    [Fact]
    public async Task MeetupWithMinor_OnlyAtPartnerStore_DuringTheDay()
    {
        await SeedAsync();
        await using var db = NewContext();
        var svc = Service(db);
        (await svc.CheckMeetupAsync(_adult, _minor, null, Noon)).Code.Should().Be("STORE_REQUIRED");
        (await svc.CheckMeetupAsync(_adult, _minor, _storeId, Night)).Code.Should().Be("DAYTIME_REQUIRED");
        (await svc.CheckMeetupAsync(_adult, _minor, _storeId, Noon)).Allowed.Should().BeTrue();
        (await svc.CheckMeetupAsync(_adult, _adult2, null, Night)).Allowed.Should().BeTrue();
    }
}
