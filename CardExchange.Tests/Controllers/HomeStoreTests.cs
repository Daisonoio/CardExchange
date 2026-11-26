using System.Security.Claims;
using System.Text.Json;
using CardExchange.API.Controllers;
using CardExchange.API.Services;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardExchange.Tests.Controllers;

public class HomeStoreTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly User _owner, _mario;
    private readonly Store _corner, _other, _pending;

    public HomeStoreTests()
    {
        User U(string n) => new() { Email = $"{n}@t.it", Username = n, PasswordHash = "x" };
        _owner = U("owner");
        _mario = U("mario");
        Store S(string name, StoreStatus status) => new() { Owner = _owner, Name = name, Slug = name.ToLowerInvariant().Replace(' ', '-'), Address = "Via Roma 1", City = "Milano", Status = status };
        _corner = S("Games Corner", StoreStatus.Verified);
        _other = S("Drago Nero", StoreStatus.Verified);
        _pending = S("In Attesa", StoreStatus.Pending);
        _db.Users.AddRange(_owner, _mario);
        _db.Stores.AddRange(_corner, _other, _pending);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private StoresController Controller(int userId) =>
        new(_db, new Mock<INotificationService>().Object, new Mock<ILogger<StoresController>>().Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test")) } }
        };

    private static JsonElement Json(IActionResult r) => JsonSerializer.SerializeToElement(((ObjectResult)r).Value);

    [Fact]
    public async Task Set_Get_And_Clear()
    {
        var c = Controller(_mario.Id);
        Json(await c.GetHomeStore()).GetProperty("store").ValueKind.Should().Be(JsonValueKind.Null);

        Json(await c.SetHomeStore(_corner.Id)).GetProperty("changed").GetBoolean().Should().BeTrue();
        Json(await c.GetHomeStore()).GetProperty("store").GetProperty("Name").GetString().Should().Be("Games Corner");

        (await c.ClearHomeStore()).Should().BeOfType<NoContentResult>();
        (await _db.Users.FindAsync(_mario.Id))!.HomeStoreId.Should().BeNull();
    }

    [Fact]
    public async Task OnlyIfUnset_KeepsTheChosenStore()
    {
        var c = Controller(_mario.Id);
        await c.SetHomeStore(_corner.Id);
        Json(await c.SetHomeStore(_other.Id, onlyIfUnset: true)).GetProperty("changed").GetBoolean().Should().BeFalse();
        (await _db.Users.FindAsync(_mario.Id))!.HomeStoreId.Should().Be(_corner.Id);
    }

    [Fact]
    public async Task OnlyVerifiedStores()
    {
        (await Controller(_mario.Id).SetHomeStore(_pending.Id)).Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Owner_SeesHomeCustomers_OnlyFromThree()
    {
        _mario.HomeStoreId = _corner.Id;
        _db.Users.Add(new User { Email = "a@t.it", Username = "a", PasswordHash = "x", HomeStoreId = _corner.Id });
        await _db.SaveChangesAsync();
        Json(await Controller(_owner.Id).Attributions(_corner.Id)).GetProperty("homeCustomers").ValueKind.Should().Be(JsonValueKind.Null);

        _db.Users.Add(new User { Email = "b@t.it", Username = "b", PasswordHash = "x", HomeStoreId = _corner.Id });
        await _db.SaveChangesAsync();
        Json(await Controller(_owner.Id).Attributions(_corner.Id)).GetProperty("homeCustomers").GetInt32().Should().Be(3);
    }
}
