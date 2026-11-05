using CardExchange.API.Services;
using CardExchange.API.Services.Catalog;
using CardExchange.API.Services.Monitoring;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardExchange.Tests.Services;

public class ErrorFingerprintTests
{
    [Fact]
    public void SameErrorWithDifferentIds_SameFingerprint()
    {
        var a = ErrorFingerprint.Compute("Server", "Cat", "Carta 123 non trovata", "at A.B() in /x.cs:line 10");
        var b = ErrorFingerprint.Compute("Server", "Cat", "Carta 456 non trovata", "at A.B() in /x.cs:line 99");
        a.Should().Be(b);
        ErrorFingerprint.Compute("Server", "Cat", "Altro errore", null).Should().NotBe(a);
        ErrorFingerprint.Compute("Client", "Cat", "Carta 1 non trovata", "at A.B()").Should().NotBe(a);
    }
}

public class MonitoringTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly Mock<INotificationService> _notifications = new();
    private readonly MonitoringAlerts _alerts;
    private readonly User _admin;

    public MonitoringTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _admin = new User { Email = "a@t.it", Username = "admin", PasswordHash = "x" };
        var role = new Role { Name = "Admin" };
        _db.UserRoles.Add(new UserRole { User = _admin, Role = role });
        _db.Users.Add(new User { Email = "u@t.it", Username = "user", PasswordHash = "x" });
        _db.SaveChanges();
        _alerts = new MonitoringAlerts(_db, _notifications.Object);
    }

    public void Dispose() => _db.Dispose();

    private static ErrorReport Err(string msg, ErrorSource source = ErrorSource.Server) =>
        new(source, "CardExchange.API.Controllers.X", msg, "at X.Y()", null, DateTime.UtcNow);

    [Fact]
    public async Task SameErrors_AreGrouped_AndOnlyNewOnesAlertAdmins()
    {
        var alerted = await ErrorTrackingHostedService.SaveAsync(_db, _alerts, new[] { Err("Carta 1 non trovata"), Err("Carta 2 non trovata"), Err("Timeout") }, default);
        alerted.Should().Be(2);
        _db.ErrorGroups.Should().HaveCount(2);
        _db.ErrorGroups.Single(g => g.Message.StartsWith("Carta")).Count.Should().Be(2);

        (await ErrorTrackingHostedService.SaveAsync(_db, _alerts, new[] { Err("Carta 3 non trovata") }, default)).Should().Be(0, "gruppo già noto");
        _db.ErrorGroups.Single(g => g.Message.StartsWith("Carta")).Count.Should().Be(3);

        _notifications.Verify(n => n.SendAsync(_admin.Id, NotificationType.SystemAnnouncement, It.Is<string>(t => t.StartsWith("Nuovo errore")), It.IsAny<string>(), null, "Monitoring"), Times.Exactly(2));
    }

    [Fact]
    public async Task ResolvedError_ThatComesBack_IsReopenedAndAlerted()
    {
        await ErrorTrackingHostedService.SaveAsync(_db, _alerts, new[] { Err("Timeout del catalogo") }, default);
        _db.ErrorGroups.Single().IsResolved = true;
        await _db.SaveChangesAsync();

        (await ErrorTrackingHostedService.SaveAsync(_db, _alerts, new[] { Err("Timeout del catalogo") }, default)).Should().Be(1);
        _db.ErrorGroups.Single().IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task CatalogOutage_AlertsAfterTwoFailures_OnceAndOnRecovery()
    {
        var checker = new CatalogHealthChecker(_db, Mock.Of<ICatalogRegistry>(), _alerts, new Mock<ILogger<CatalogHealthChecker>>().Object, Mock.Of<IScryfallService>());
        var now = DateTime.UtcNow;

        await checker.RecordAsync("pokemon", "HttpRequestException: 503", now, default);
        _notifications.Invocations.Should().BeEmpty("un solo errore può essere un caso");
        await checker.RecordAsync("pokemon", "HttpRequestException: 503", now.AddMinutes(15), default);
        await checker.RecordAsync("pokemon", "HttpRequestException: 503", now.AddMinutes(30), default);
        _notifications.Verify(n => n.SendAsync(_admin.Id, NotificationType.SystemAnnouncement, "Catalogo pokemon non raggiungibile", It.IsAny<string>(), null, "Monitoring"), Times.Once);

        await checker.RecordAsync("pokemon", null, now.AddMinutes(45), default);
        _notifications.Verify(n => n.SendAsync(_admin.Id, NotificationType.SystemAnnouncement, "Catalogo pokemon di nuovo raggiungibile", It.IsAny<string>(), null, "Monitoring"), Times.Once);
        _db.CatalogStatuses.Single().ConsecutiveFailures.Should().Be(0);
    }
}
