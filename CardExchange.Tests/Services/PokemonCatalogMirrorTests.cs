using CardExchange.API.DTOs.PokemonTcg;
using CardExchange.API.Services;
using CardExchange.API.Services.Catalog;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardExchange.Tests.Services;

public class PokemonCatalogMirrorTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Mock<IPokemonTcgService> _api = new();
    private readonly DateTime _now = new(2026, 11, 6, 2, 0, 0, DateTimeKind.Utc);

    public void Dispose() => _db.Dispose();

    private PokemonCatalogMirror Mirror => new(_db, _api.Object, new Mock<ILogger<PokemonCatalogMirror>>().Object);

    private static PokemonTcgCard Card(string setId, string number, string name, int total) =>
        new() { Id = $"{setId}-{number}", Name = name, Number = number, Set = new PokemonTcgSet { Id = setId, Name = setId.ToUpper(), PrintedTotal = total, Total = total } };

    private void SetupApi(int sets = 14)
    {
        var all = Enumerable.Range(1, sets).Select(i => new PokemonTcgSet { Id = $"sv{i}", Name = $"SV {i}", ReleaseDate = $"2023/{i:00}/01", PrintedTotal = 2 }).ToList();
        all.Add(new PokemonTcgSet { Id = "base1", Name = "Base", ReleaseDate = "1999/01/09", PrintedTotal = 102 });
        _api.Setup(a => a.GetAllSetsAsync()).ReturnsAsync(all);
        _api.Setup(a => a.GetSetByIdAsync(It.IsAny<string>())).ReturnsAsync((string id) => all.FirstOrDefault(s => s.Id == id));
        _api.Setup(a => a.GetCardsBySetAsync(It.IsAny<string>(), 1, 250)).ReturnsAsync((string id, int _, int _) =>
            new PokemonTcgResponse<List<PokemonTcgCard>> { Data = new() { Card(id, "1", "Pikachu", 2), Card(id, "58", "Pikachu", 102) }, Page = 1, PageSize = 250, TotalCount = 2 });
        _api.Setup(a => a.MapToCardSet(It.IsAny<PokemonTcgSet>(), It.IsAny<CardSet>())).Callback((PokemonTcgSet s, CardSet d) =>
        {
            d.PokemonTcgId = s.Id; d.Name = s.Name; d.Code = s.Id.ToUpper(); d.PrintedTotal = s.PrintedTotal;
        });
        _api.Setup(a => a.MapToCardInfo(It.IsAny<PokemonTcgCard>(), It.IsAny<CardInfo>())).Callback((PokemonTcgCard c, CardInfo d) =>
        {
            d.PokemonTcgId = c.Id; d.Name = c.Name; d.CardNumber = c.Number;
        });
    }

    [Fact]
    public async Task Mirrors_RecentAndUsedSets_InBatches_ThenSkipsFreshOnes()
    {
        SetupApi();
        // "base1" non è recente ma è nella collezione di qualcuno: va in copia
        var game = new Game { Name = PokemonCatalogMirror.GameName, Publisher = "P" };
        var baseSet = new CardSet { Name = "Base", Code = "BASE1", PokemonTcgId = "base1", Game = game, PrintedTotal = 102 };
        var info = new CardInfo { Name = "Charizard", PokemonTcgId = "base1-4", CardNumber = "4", CardSet = baseSet };
        _db.CardInfos.Add(info);
        _db.Users.Add(new User { Email = "u@t.it", Username = "u", PasswordHash = "x" });
        await _db.SaveChangesAsync();
        _db.Cards.Add(new Card { UserId = _db.Users.First().Id, CardInfoId = info.Id });
        await _db.SaveChangesAsync();

        var wanted = await Mirror.WantedSetIdsAsync();
        wanted.Should().HaveCount(PokemonCatalogMirror.RecentSets + 1).And.Contain("base1").And.Contain("sv14").And.NotContain("sv1");

        (await Mirror.RunAsync(_now)).Should().Be(PokemonCatalogMirror.SetsPerRun);
        (await Mirror.RunAsync(_now)).Should().Be(PokemonCatalogMirror.SetsPerRun);
        (await Mirror.RunAsync(_now)).Should().Be(1);
        (await Mirror.RunAsync(_now.AddDays(1))).Should().Be(0, "ogni set si rinnova al massimo una volta a settimana");

        var status = await Mirror.StatusAsync();
        status.Sets.Should().Be(13);
        status.Cards.Should().Be(13 * 2 + 1);
        _db.CardInfos.Count(ci => ci.PokemonTcgId == "base1-58").Should().Be(1, "nessun doppione");
    }

    [Fact]
    public async Task ApiDown_SetIsRetriedLater()
    {
        SetupApi(sets: 1);
        _api.Setup(a => a.GetCardsBySetAsync(It.IsAny<string>(), 1, 250)).ReturnsAsync((PokemonTcgResponse<List<PokemonTcgCard>>?)null);
        (await Mirror.RunAsync(_now)).Should().Be(0);
        (await _db.CardSets.CountAsync(s => s.MirroredAt != null)).Should().Be(0);
    }

    [Fact]
    public async Task Import_ResolvesFromLocalCopy_WithoutTheApi()
    {
        SetupApi(sets: 1);
        await Mirror.MirrorSetAsync("base1", _now);
        var provider = new PokemonCatalogProvider(_db, _api.Object);
        _api.Invocations.Clear();

        (await provider.ResolveAsync(new CatalogLookup("Pikachu", "base1", null, "58")))!.ExternalId.Should().Be("base1-58");
        (await provider.ResolveAsync(new CatalogLookup("pikachu", null, "Base Set", "58/102")))!.ExternalId.Should().Be("base1-58");
        _api.Verify(a => a.SearchCardsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }
}
