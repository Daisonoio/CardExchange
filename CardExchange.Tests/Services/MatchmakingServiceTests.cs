using CardExchange.API.Services;
using CardExchange.API.Services.Moderation;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardExchange.Tests.Services;

public class MatchmakingServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly HashSet<int> _blocked = new();
    private readonly MatchmakingService _service;
    private readonly CardInfo _bolt, _ring, _pika;
    private readonly User _me;

    public MatchmakingServiceTests()
    {
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var magic = new Game { Name = "Magic" };
        var pokemon = new Game { Name = "Pokémon" };
        var m10 = new CardSet { Game = magic, Name = "M10", Code = "m10" };
        var base1 = new CardSet { Game = pokemon, Name = "Base", Code = "base1" };
        _bolt = new CardInfo { CardSet = m10, Name = "Lightning Bolt", PriceEur = 2m };
        _ring = new CardInfo { CardSet = m10, Name = "Sol Ring", PriceEur = 3m };
        _pika = new CardInfo { CardSet = base1, Name = "Pikachu", PriceEur = 5m };
        _context.CardInfos.AddRange(_bolt, _ring, _pika);
        _me = AddUser("me", 45.4642m, 9.19m);
        _context.SaveChanges();

        var blocks = new Mock<IUserBlockService>();
        blocks.Setup(b => b.GetBlockedUserIdsAsync(It.IsAny<int>())).ReturnsAsync(() => _blocked);
        _service = new MatchmakingService(_context, blocks.Object, new Mock<ILogger<MatchmakingService>>().Object);
    }

    public void Dispose() => _context.Dispose();

    private User AddUser(string name, decimal? lat, decimal? lng, bool active = true)
    {
        var u = new User { Username = name, Email = $"{name}@t.it", FirstName = name, PasswordHash = "x", IsActive = active };
        if (lat.HasValue) u.Location = new UserLocation { City = "X", Province = "X", Country = "IT", Latitude = lat, Longitude = lng, MaxDistanceKm = 50 };
        _context.Users.Add(u);
        return u;
    }

    private void Has(User u, CardInfo ci, CardCondition cond = CardCondition.NearMint, bool trade = true, decimal? asking = null) =>
        _context.Cards.Add(new Card { User = u, CardInfo = ci, Condition = cond, IsAvailableForTrade = trade, AskingPrice = asking });

    private void Wants(User u, CardInfo ci, CardCondition? atLeast = null, decimal? maxPrice = null, int priority = 2) =>
        _context.WishlistItems.Add(new WishlistItem { User = u, CardInfo = ci, PreferredCondition = atLeast, MaxPrice = maxPrice, Priority = priority });

    [Fact]
    public async Task MutualMatch_ComesFirst_WithDistanceAndCards()
    {
        var near = AddUser("near", 45.47m, 9.20m);   // ~1 km
        var oneWay = AddUser("oneway", 45.48m, 9.21m);
        Wants(_me, _bolt, priority: 1);
        Has(_me, _ring);
        Has(near, _bolt); Wants(near, _ring);
        Has(oneWay, _bolt);
        await _context.SaveChangesAsync();

        var results = (await _service.FindMatchesAsync(_me.Id)).ToList();

        results.Select(r => r.Username).Should().Equal("near", "oneway");
        results[0].IsMutual.Should().BeTrue();
        results[0].TheyHaveIWant.Single().Name.Should().Be("Lightning Bolt");
        results[0].IHaveTheyWant.Single().Name.Should().Be("Sol Ring");
        results[0].DistanceKm.Should().BeLessThan(2);
        results[1].IsMutual.Should().BeFalse();
    }

    [Fact]
    public async Task ExcludesFarBlockedInactiveAndNotForTrade_KeepsUsersWithoutLocation()
    {
        var far = AddUser("far", 41.9m, 12.5m);      // Roma, > 50 km
        var blocked = AddUser("blocked", 45.47m, 9.2m);
        var inactive = AddUser("inactive", 45.47m, 9.2m, active: false);
        var keeper = AddUser("keeper", 45.47m, 9.2m);
        var noLoc = AddUser("noloc", null, null);
        Wants(_me, _bolt);
        foreach (var u in new[] { far, blocked, inactive, noLoc }) Has(u, _bolt);
        Has(keeper, _bolt, trade: false);
        await _context.SaveChangesAsync();
        _blocked.Add(blocked.Id);

        var names = (await _service.FindMatchesAsync(_me.Id)).Select(r => r.Username).ToList();

        names.Should().Equal("noloc");
        (await _service.FindMatchesAsync(_me.Id, radiusKm: 1000)).Select(r => r.Username).Should().BeEquivalentTo("noloc", "far");
    }

    [Fact]
    public async Task AppliesWishlistRules_ConditionAndMaxPriceOnAskingOrMarketPrice()
    {
        var worn = AddUser("worn", 45.47m, 9.2m);
        var pricey = AddUser("pricey", 45.47m, 9.2m);
        var good = AddUser("good", 45.47m, 9.2m);
        Wants(_me, _bolt, atLeast: CardCondition.Excellent, maxPrice: 4m);
        Has(worn, _bolt, CardCondition.HeavilyPlayed);
        Has(pricey, _bolt, CardCondition.NearMint, asking: 6m);   // prezzo indicato oltre il massimo
        Has(good, _bolt, CardCondition.Mint);                       // valore di mercato 2 €
        await _context.SaveChangesAsync();

        (await _service.FindMatchesAsync(_me.Id)).Select(r => r.Username).Should().Equal("good");
    }

    [Fact]
    public async Task FiltersByGame_AndOnlyUserIds_IgnoresRadius()
    {
        var a = AddUser("a", 41.9m, 12.5m);
        var b = AddUser("b", 45.47m, 9.2m);
        Wants(_me, _bolt); Wants(_me, _pika);
        Has(a, _pika); Has(b, _bolt);
        await _context.SaveChangesAsync();
        var pokemonId = _pika.CardSet.GameId;

        (await _service.FindMatchesAsync(_me.Id, radiusKm: 2000, gameId: pokemonId)).Select(r => r.Username).Should().Equal("a");
        (await _service.FindMatchesAsync(_me.Id, onlyUserIds: new[] { a.Id })).Select(r => r.Username).Should().Equal(new[] { "a" },
            "alla serata di scambio i presenti sono nello stesso posto, il raggio non conta");
    }

    [Fact]
    public void Score_RewardsMutualPriorityAndProximity()
    {
        var c = new MatchCard { WishlistPriority = 1 };
        var mutualNear = MatchmakingService.Score(new[] { c }, new[] { new MatchCard() }, 5, 4.5m, 12);
        var oneWayFar = MatchmakingService.Score(new[] { c }, Array.Empty<MatchCard>(), 90, 0, 0);
        mutualNear.Should().Be(100 + 20 + 10 + 5 + 15 + 50 + 15 + 10);
        oneWayFar.Should().Be(10 + 15 + 10);
    }
}
