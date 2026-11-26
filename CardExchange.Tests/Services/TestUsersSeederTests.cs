using CardExchange.API.Services.Seeding;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CardExchange.Tests.Services;

public class TestUsersSeederTests
{
    [Fact]
    public async Task CreatesEveryProfile_Once_AndKeepsChangesOnRestart()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        (await TestUsersSeeder.SeedAsync(db, NullLogger.Instance)).Created.Should().Be(11);
        var users = await db.Users.IgnoreQueryFilters().ToListAsync();
        users.Select(u => u.ProfileType).Distinct().Should().BeEquivalentTo(new[] { ProfileType.Collector, ProfileType.Seller, ProfileType.Store, ProfileType.Organizer });
        users.Single(u => u.Username == "test.senzaeta").BirthDate.Should().BeNull();
        users.Single(u => u.Username == "test.nonverificato").EmailConfirmed.Should().BeFalse();
        BCrypt.Net.BCrypt.Verify(TestUsersSeeder.Password, users[0].PasswordHash).Should().BeTrue();

        var store = await db.Stores.SingleAsync();
        store.Status.Should().Be(StoreStatus.Verified);
        users.Single(u => u.Username == "test.daqr").HomeStoreId.Should().Be(store.Id);
        (await db.StoreAttributions.CountAsync()).Should().Be(1);

        // Una modifica fatta durante i test resta al riavvio
        users.Single(u => u.Username == "test.collezionista").ProfileType = ProfileType.Seller;
        await db.SaveChangesAsync();
        var again = await TestUsersSeeder.SeedAsync(db, NullLogger.Instance);
        again.Created.Should().Be(0);
        again.Present.Should().Be(11);
        (await db.Users.IgnoreQueryFilters().CountAsync()).Should().Be(11);
        (await db.Stores.CountAsync()).Should().Be(1);
        (await db.Users.SingleAsync(u => u.Username == "test.collezionista")).ProfileType.Should().Be(ProfileType.Seller);
    }
}
