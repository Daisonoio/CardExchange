using CardExchange.API.Configuration;
using CardExchange.API.Services;
using CardExchange.API.Services.Email;
using CardExchange.API.Services.Stores;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace CardExchange.Tests.Services;

public class StoreMonthlyReportsTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Mock<IEmailSender> _email = new();
    private readonly Mock<INotificationService> _notifications = new();
    // 2 febbraio 2027, ore 03:00 italiane: il mese concluso è gennaio 2027
    private readonly DateTime _now = new(2027, 2, 2, 2, 0, 0, DateTimeKind.Utc);

    public void Dispose() => _db.Dispose();

    private StoreMonthlyReports Service() => new(_db, new StoreStatsService(_db), _notifications.Object, _email.Object,
        Options.Create(new EmailSettings { FrontendBaseUrl = "https://app.test" }), new Mock<ILogger<StoreMonthlyReports>>().Object);

    private Store AddStore(string name, StoreStatus status = StoreStatus.Verified)
    {
        var owner = new User { Email = $"{name}@t.it", Username = name, FirstName = name, PasswordHash = "x", EmailConfirmed = true };
        var store = new Store { Name = name, Owner = owner, Status = status, City = "Milano", Address = "Via Roma 1" };
        _db.Stores.Add(store);
        return store;
    }

    private void Activity(Store s, StoreActivityType type, int user, int refId, DateTime atUtc) =>
        _db.StoreActivities.Add(new StoreActivity { StoreId = s.Id, Type = type, UserId = user, RefId = refId, OccurredAt = atUtc });

    [Fact]
    public void MonthBounds_UseItalianTime()
    {
        StoreMonthlyReports.LastCompletedMonth(_now).Should().Be(new DateOnly(2027, 1, 1));
        // 31 gennaio alle 23:30 UTC in Italia è già febbraio
        StoreMonthlyReports.LastCompletedMonth(new DateTime(2027, 1, 31, 23, 30, 0, DateTimeKind.Utc)).Should().Be(new DateOnly(2027, 1, 1));
        var (from, to) = StoreMonthlyReports.MonthBounds(new DateOnly(2027, 1, 1));
        from.Should().Be(new DateTime(2026, 12, 31, 23, 0, 0));
        to.Should().Be(new DateTime(2027, 1, 31, 23, 0, 0));
    }

    [Fact]
    public async Task CreatesOneReportPerVerifiedStore_WithPreviousMonth_AndNotifiesOwner()
    {
        var games = AddStore("games");
        var pending = AddStore("pending", StoreStatus.Pending);
        await _db.SaveChangesAsync();
        // Gennaio: 3 persone (2 scambi); dicembre: 1 persona
        Activity(games, StoreActivityType.MeetupCompleted, 10, 1, new DateTime(2027, 1, 10, 15, 0, 0, DateTimeKind.Utc));
        Activity(games, StoreActivityType.MeetupCompleted, 11, 1, new DateTime(2027, 1, 10, 15, 0, 0, DateTimeKind.Utc));
        Activity(games, StoreActivityType.MeetupCompleted, 12, 2, new DateTime(2027, 1, 20, 15, 0, 0, DateTimeKind.Utc));
        Activity(games, StoreActivityType.EventCheckIn, 13, 0, new DateTime(2026, 12, 20, 15, 0, 0, DateTimeKind.Utc));
        Activity(games, StoreActivityType.EventCheckIn, 14, 0, new DateTime(2027, 2, 1, 15, 0, 0, DateTimeKind.Utc)); // febbraio: escluso
        await _db.SaveChangesAsync();

        (await Service().RunAsync(_now)).Should().Be(1);
        (await Service().RunAsync(_now.AddDays(1))).Should().Be(0, "un solo riepilogo per mese");

        var report = await _db.StoreMonthlyReports.SingleAsync();
        report.StoreId.Should().Be(games.Id);
        report.Month.Should().Be(new DateOnly(2027, 1, 1));
        report.SentAt.Should().NotBeNull();
        var content = System.Text.Json.JsonSerializer.Deserialize<StoreReportContent>(report.MetricsJson, StoreMonthlyReports.Json)!;
        content.Stats.PeopleBroughtIn.Should().Be(3);
        content.Stats.TradesCompletedAtStore.Should().Be(2);
        content.Previous.PeopleBroughtIn.Should().Be(1);

        _notifications.Verify(n => n.SendAsync(games.OwnerId, NotificationType.StoreActivity, It.IsAny<string>(),
            It.Is<string>(b => b.StartsWith("CardExchange ti ha portato 3 persone in negozio")), games.Id, "StoreReport"), Times.Once);
        _notifications.Verify(n => n.SendAsync(pending.OwnerId, It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()), Times.Never);
        _email.Verify(e => e.SendAsync("games@t.it", It.IsAny<string>(), It.Is<EmailMessage>(m =>
            m.Subject.Contains("gennaio 2027") && m.TextBody.Contains("2 scambi conclusi da te")
            && m.TextBody.Contains($"https://app.test/stores/{games.Id}/results")), It.IsAny<CancellationToken>()), Times.Once);
    }
}
