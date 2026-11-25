using System.Security.Claims;
using System.Text.Json;
using CardExchange.API.Controllers;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardExchange.Tests.Controllers;

public class AdminUsersControllerTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private int _admin, _collector, _minor, _store;

    public AdminUsersControllerTests()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        User U(string n, DateOnly? birth, ProfileType p = ProfileType.Collector) =>
            new() { Email = $"{n}@t.it", Username = n, PasswordHash = "x", BirthDate = birth, ProfileType = p, RefreshToken = "rt" };
        var admin = U("admin", today.AddYears(-40));
        var collector = U("mario", today.AddYears(-30));
        var minor = U("luca15", today.AddYears(-15));
        var store = U("gamescorner", null, ProfileType.Store);
        var removed = U("deleted_user_9", null);
        removed.Email = "deleted_9@removed.local";
        _db.Users.AddRange(admin, collector, minor, store, removed);
        _db.SaveChanges();
        (_admin, _collector, _minor, _store) = (admin.Id, collector.Id, minor.Id, store.Id);
    }

    public void Dispose() => _db.Dispose();

    private AdminUsersController Controller(bool canBan = false)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, _admin.ToString()), new(ClaimTypes.Role, "Admin") };
        if (canBan) claims.Add(new Claim("Permission", "USERS.BAN"));
        return new AdminUsersController(_db, new Mock<ILogger<AdminUsersController>>().Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } }
        };
    }

    private static JsonElement Json(IActionResult r) =>
        JsonSerializer.SerializeToElement(((ObjectResult)r).Value);

    [Fact]
    public async Task List_SearchesAndFilters_AndHidesDeletedAccounts()
    {
        var all = Json(await Controller().List(null, null, null));
        all.GetProperty("total").GetInt32().Should().Be(4);

        var byName = Json(await Controller().List("MAR", null, null));
        byName.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("Username").GetString()).Should().Equal("mario");

        var stores = Json(await Controller().List(null, ProfileType.Store, null));
        stores.GetProperty("items").EnumerateArray().Single().GetProperty("Username").GetString().Should().Be("gamescorner");
    }

    [Fact]
    public async Task ChangingProfileType_IsRecordedInTheAudit_WithNote()
    {
        await Controller().SetProfileType(_collector, new AdminUsersController.ProfileTypeRequest(ProfileType.Seller, "Banco al mercatino di Porta Genova"));
        await Controller().SetProfileType(_collector, new AdminUsersController.ProfileTypeRequest(ProfileType.Seller));   // nessun cambio

        (await _db.Users.SingleAsync(u => u.Id == _collector)).ProfileType.Should().Be(ProfileType.Seller);
        var entry = await _db.AdminAuditEntries.SingleAsync();
        (entry.AdminId, entry.Action, entry.Detail, entry.Note).Should().Be((_admin, "ProfileType", "Collector → Seller", "Banco al mercatino di Porta Genova"));

        var detail = Json(await Controller().Get(_collector));
        detail.GetProperty("profileType").GetString().Should().Be("Seller");
        detail.GetProperty("audit").EnumerateArray().Single().GetProperty("admin").GetString().Should().Be("admin");
    }

    [Fact]
    public async Task Detail_ShowsOnlyTheAgeGroup()
    {
        Json(await Controller().Get(_minor)).GetProperty("ageGroup").GetString().Should().Be("minor");
        Json(await Controller().Get(_store)).GetProperty("ageGroup").GetString().Should().Be("unknown");
        Json(await Controller().Get(_collector)).GetProperty("ageGroup").GetString().Should().Be("adult");
        Json(await Controller().Get(_collector)).TryGetProperty("BirthDate", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Deactivation_NeedsTheBanPermission_LogsOut_AndIsAudited()
    {
        var denied = await Controller().SetActive(_collector, new AdminUsersController.ActiveRequest(false));
        ((ObjectResult)denied).StatusCode.Should().Be(403);
        (await Controller(canBan: true).SetActive(_admin, new AdminUsersController.ActiveRequest(false))).Should().BeOfType<BadRequestObjectResult>();

        await Controller(canBan: true).SetActive(_collector, new AdminUsersController.ActiveRequest(false, "Truffa confermata"));
        // Gli account disattivati sono esclusi dal filtro globale degli utenti
        var u = await _db.Users.IgnoreQueryFilters().SingleAsync(x => x.Id == _collector);
        (u.IsActive, u.RefreshToken).Should().Be((false, (string?)null));
        (await _db.AdminAuditEntries.SingleAsync()).Action.Should().Be("Deactivate");
    }

    [Fact]
    public async Task LiftChat_OnlyWhenSuspended()
    {
        (await Controller().LiftChat(_collector, null)).Should().BeOfType<BadRequestObjectResult>();
        (await _db.Users.SingleAsync(x => x.Id == _collector)).MessagingSuspendedUntil = DateTime.UtcNow.AddDays(5);
        await _db.SaveChangesAsync();

        (await Controller().LiftChat(_collector, new AdminUsersController.NoteRequest("Falso allarme"))).Should().BeOfType<OkObjectResult>();
        (await _db.Users.SingleAsync(x => x.Id == _collector)).MessagingSuspendedUntil.Should().BeNull();
        (await _db.AdminAuditEntries.SingleAsync()).Action.Should().Be("ChatLifted");
    }
}
