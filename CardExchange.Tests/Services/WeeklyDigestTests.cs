using CardExchange.API.Configuration;
using CardExchange.API.Services;
using CardExchange.API.Services.Email;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace CardExchange.Tests.Services;

public class WeeklyDigestTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IMatchmakingService> _matchmaking = new();
    private readonly Mock<IEmailSender> _email = new();
    private readonly WeeklyDigest _digest;
    private readonly DateTime _now = new(2026, 11, 9, 2, 30, 0, DateTimeKind.Utc);

    public WeeklyDigestTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new ApplicationDbContext(options);
        _matchmaking.Setup(m => m.FindMatchesAsync(It.IsAny<int>(), null, null, null, null)).ReturnsAsync(new[]
        {
            new MatchResult { UserId = 99, Username = "carla", DistanceKm = 3.4, Score = 120,
                TheyHaveIWant = { new MatchCard(), new MatchCard() }, IHaveTheyWant = { new MatchCard() } },
            new MatchResult { UserId = 98, Username = "dario", DistanceKm = 12, Score = 40, TheyHaveIWant = { new MatchCard() } },
        });
        _digest = new WeeklyDigest(_context, _matchmaking.Object, _email.Object,
            Options.Create(new EmailSettings { FrontendBaseUrl = "https://app.test" }), new Mock<ILogger<WeeklyDigest>>().Object);
    }

    public void Dispose() => _context.Dispose();

    private User AddUser(string name, bool confirmed = true, bool location = true, int lastLoginDaysAgo = 2, DateTime? lastDigest = null)
    {
        var u = new User
        {
            Email = $"{name}@t.it", Username = name, FirstName = name, PasswordHash = "x",
            EmailConfirmed = confirmed, LastLoginAt = _now.AddDays(-lastLoginDaysAgo), LastWeeklyDigestAt = lastDigest
        };
        if (location) u.Location = new UserLocation { City = "Milano", Province = "MI", Country = "IT", Latitude = 45.46m, Longitude = 9.19m };
        _context.Users.Add(u);
        return u;
    }

    [Fact]
    public async Task SendsOnlyToEligibleUsers_OncePerWeek()
    {
        var ok = AddUser("ok");
        AddUser("unconfirmed", confirmed: false);
        AddUser("nolocation", location: false);
        AddUser("inactive", lastLoginDaysAgo: 90);
        AddUser("recent", lastDigest: _now.AddDays(-3));
        var optedOut = AddUser("optedout");
        await _context.SaveChangesAsync();
        _context.NotificationPreferences.Add(new NotificationPreference { UserId = optedOut.Id, Type = NotificationType.WeeklyDigest, IsEnabled = false });
        await _context.SaveChangesAsync();

        (await _digest.RunAsync(_now)).Should().Be(1);
        (await _digest.RunAsync(_now.AddDays(1))).Should().Be(0, "il riepilogo è settimanale");
        // Dopo una settimana riparte per "ok" e ora anche per "recent" (ultimo invio 10 giorni prima)
        (await _digest.RunAsync(_now.AddDays(7))).Should().Be(2);

        _email.Verify(e => e.SendAsync("ok@t.it", It.IsAny<string>(), It.Is<EmailMessage>(m =>
            m.Subject.Contains("2 collezionisti") && m.TextBody.Contains("carla a 3 km ha 2 carte che cerchi e ne cerca 1 delle tue")
            && m.TextBody.Contains("https://app.test/matchmaking")), It.IsAny<CancellationToken>()), Times.Exactly(2));
        ok.LastWeeklyDigestAt.Should().Be(_now.AddDays(7));
    }

    [Fact]
    public async Task UserWithoutMatches_GetsNoEmailButIsMarkedAsEvaluated()
    {
        var u = AddUser("lonely");
        await _context.SaveChangesAsync();
        _matchmaking.Setup(m => m.FindMatchesAsync(u.Id, null, null, null, null)).ReturnsAsync(Array.Empty<MatchResult>());

        (await _digest.RunAsync(_now)).Should().Be(0);
        u.LastWeeklyDigestAt.Should().Be(_now);
        _email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Describe_ExplainsMatchDirection()
    {
        WeeklyDigest.Describe(new MatchResult { Username = "x", DistanceKm = 0.3, IHaveTheyWant = { new MatchCard() } })
            .Should().Be("x a 1 km cerca 1 carta che hai");
    }
}
