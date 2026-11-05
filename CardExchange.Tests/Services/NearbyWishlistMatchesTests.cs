using CardExchange.API.Services;
using CardExchange.API.Services.Moderation;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CardExchange.Tests.Services;

public class NearbyWishlistMatchesTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Mock<IUserBlockService> _blocks = new();

    public void Dispose() => _db.Dispose();

    private User AddUser(string name, decimal lat, decimal lon)
    {
        var u = new User { Email = $"{name}@t.it", Username = name, PasswordHash = "x",
            Location = new UserLocation { City = "Milano", Province = "MI", Country = "IT", Latitude = lat, Longitude = lon } };
        _db.Users.Add(u);
        return u;
    }

    [Fact]
    public async Task ReturnsNearbyCards_ByPrice_WithRoundedDistance_AndRules()
    {
        var game = new Game { Name = "Magic" };
        var other = new Game { Name = "Pokémon" };
        var set = new CardSet { Name = "Alpha", Code = "LEA", Game = game };
        var pset = new CardSet { Name = "Base", Code = "BS", Game = other };
        var lotus = new CardInfo { Name = "Black Lotus", CardSet = set, PriceEur = 9000 };
        var bolt = new CardInfo { Name = "Lightning Bolt", CardSet = set, PriceEur = 5 };
        var pika = new CardInfo { Name = "Pikachu", CardSet = pset, PriceEur = 50 };
        _db.CardInfos.AddRange(lotus, bolt, pika);
        var me = AddUser("me", 45.4642m, 9.1900m);
        var near = AddUser("near", 45.4700m, 9.2000m);         // < 1 km → mostrato come 1
        var far = AddUser("far", 41.9028m, 12.4964m);          // Roma
        var blocked = AddUser("blocked", 45.4650m, 9.1910m);
        await _db.SaveChangesAsync();
        _db.WishlistItems.AddRange(
            new WishlistItem { UserId = me.Id, CardInfoId = lotus.Id },
            new WishlistItem { UserId = me.Id, CardInfoId = bolt.Id, PreferredCondition = CardCondition.NearMint },
            new WishlistItem { UserId = me.Id, CardInfoId = pika.Id });
        _db.Cards.AddRange(
            new Card { UserId = near.Id, CardInfoId = lotus.Id, Condition = CardCondition.LightlyPlayed, IsAvailableForTrade = true },
            new Card { UserId = near.Id, CardInfoId = bolt.Id, Condition = CardCondition.Mint, IsAvailableForTrade = true },
            new Card { UserId = near.Id, CardInfoId = bolt.Id, Condition = CardCondition.Damaged, IsAvailableForTrade = true }, // condizione insufficiente
            new Card { UserId = near.Id, CardInfoId = pika.Id, Condition = CardCondition.Mint, IsAvailableForTrade = true },
            new Card { UserId = near.Id, CardInfoId = lotus.Id, Condition = CardCondition.Mint, IsAvailableForTrade = false }, // non disponibile
            new Card { UserId = far.Id, CardInfoId = lotus.Id, Condition = CardCondition.Mint, IsAvailableForTrade = true },     // troppo lontano
            new Card { UserId = blocked.Id, CardInfoId = lotus.Id, Condition = CardCondition.Mint, IsAvailableForTrade = true },
            new Card { UserId = me.Id, CardInfoId = lotus.Id, Condition = CardCondition.Mint, IsAvailableForTrade = true });     // mia
        await _db.SaveChangesAsync();
        _blocks.Setup(b => b.GetBlockedUserIdsAsync(me.Id)).ReturnsAsync(new HashSet<int> { blocked.Id });
        var service = new NearbyWishlistMatches(_db, _blocks.Object);

        var all = await service.FindAsync(me.Id, 45.4642, 9.19, 25, null);
        all.Select(m => m.CardName).Should().Equal("Black Lotus", "Pikachu", "Lightning Bolt");
        all.Should().OnlyContain(m => m.OwnerUsername == "near" && m.DistanceKm == 1);

        var magic = await service.FindAsync(me.Id, 45.4642, 9.19, 25, game.Id);
        magic.Select(m => m.CardName).Should().Equal("Black Lotus", "Lightning Bolt");

        (await service.FindAsync(me.Id, 45.4642, 9.19, 1000, null)).Should().Contain(m => m.OwnerUsername == "far");
    }
}
