using System.Security.Claims;
using System.Text.Json;
using CardExchange.API.Configuration;
using CardExchange.API.Controllers;
using CardExchange.API.Services;
using CardExchange.API.Services.Email;
using CardExchange.API.Services.Stores;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace CardExchange.Tests.Controllers;

public class StoreRequestTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Mock<IEmailSender> _email = new();
    private readonly User _mario;
    private readonly Game _magic;

    public StoreRequestTests()
    {
        _mario = new User { Email = "mario@t.it", Username = "mario", FirstName = "Mario", LastName = "Rossi", PasswordHash = "x" };
        _magic = new Game { Name = "Magic: The Gathering" };
        _db.Users.Add(_mario);
        _db.Games.Add(_magic);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private StoresController Controller() =>
        new(_db, new Mock<INotificationService>().Object, new Mock<ILogger<StoresController>>().Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, _mario.Id.ToString()) }, "test")) } }
        };

    private StoreRequestNotifier Notifier(string mailbox) =>
        new(_email.Object, Options.Create(new StoreRequestOptions { NotifyEmail = mailbox }), Options.Create(new EmailSettings()), new Mock<ILogger<StoreRequestNotifier>>().Object);

    private StoresController.StoreRequest Complete() => new()
    {
        Name = "Games Corner", Description = "Tornei ogni settimana, area gioco e scambi.", Address = "Via Torino 12",
        City = "Milano", Province = "MI", PostalCode = "20123", Latitude = 45.46m, Longitude = 9.18m,
        Phone = "02 1234567", Website = "gamescorner.it", OpeningHours = "Mar-Sab 10-19", GameIds = new() { _magic.Id }
    };

    [Fact]
    public async Task Incomplete_IsRejected_WithTheMissingFields_AndNoEmail()
    {
        var request = Complete();
        request.Phone = "";
        request.Website = null;
        var result = await Controller().Create(request, Notifier("richieste@cardexchange.it"));

        var body = JsonSerializer.SerializeToElement(((BadRequestObjectResult)result.Result!).Value);
        body.GetProperty("missing").EnumerateArray().Select(e => e.GetString()).Should().BeEquivalentTo("phone", "website");
        _db.Stores.Should().BeEmpty();
        _email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Complete_IsSaved_AndTheSummaryGoesToTheConfiguredMailbox()
    {
        EmailMessage? sent = null;
        _email.Setup(e => e.SendAsync("richieste@cardexchange.it", It.IsAny<string>(), It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, EmailMessage, CancellationToken>((_, _, m, _) => sent = m)
            .Returns(Task.CompletedTask);

        var result = await Controller().Create(Complete(), Notifier("richieste@cardexchange.it"));

        result.Result.Should().BeOfType<OkObjectResult>();
        var store = await _db.Stores.SingleAsync();
        store.Status.Should().Be(StoreStatus.Pending);
        store.Website.Should().Be("https://gamescorner.it/");
        sent!.Subject.Should().Be("Nuova richiesta negozio: Games Corner (Milano)");
        sent.TextBody.Should().Contain("Telefono: 02 1234567").And.Contain("Giochi: Magic: The Gathering")
            .And.Contain("Instagram: —").And.Contain("@mario");
    }

    [Fact]
    public async Task NoMailboxConfigured_TheRequestIsStillSaved()
    {
        (await Controller().Create(Complete(), Notifier(""))).Result.Should().BeOfType<OkObjectResult>();
        (await _db.Stores.CountAsync()).Should().Be(1);
        _email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
