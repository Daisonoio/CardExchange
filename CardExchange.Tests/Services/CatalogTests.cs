using System.Text.Json;
using CardExchange.API.Authorization;
using CardExchange.API.Services;
using CardExchange.API.Services.Catalog;
using CardExchange.API.Services.Import;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardExchange.Tests.Services;

public class CatalogTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly CatalogStore _store;
    private readonly FakeProvider _provider;

    public CatalogTests()
    {
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        _store = new CatalogStore(_context);
        _provider = new FakeProvider(_store);
    }

    public void Dispose() => _context.Dispose();

    /// <summary>Provider di prova: un gioco nuovo implementato solo con lettura, ricerca e risoluzione.</summary>
    private class FakeProvider : GenericCatalogProvider
    {
        public readonly Dictionary<string, CatalogCard> Cards = new()
        {
            ["c1"] = new CatalogCard { ExternalId = "c1", Name = "Elsa - Snow Queen", SetCode = "1", SetName = "The First Chapter", Number = "42", Rarity = "Legendary", PriceEur = 10m, PriceEurFoil = 30m, ImageSmall = "s.png", ImageLarge = "l.png", Data = new Dictionary<string, object?> { ["ink"] = "Amethyst" } },
            ["c2"] = new CatalogCard { ExternalId = "c2", Name = "Stitch - Rock Star", SetCode = "1", SetName = "The First Chapter", Number = "23", PriceEur = 2m },
        };
        public int Calls;

        public FakeProvider(CatalogStore store) : base(store) { }
        public override string Key => "fake";
        public override string GameName => "Fake Game";
        public override string Publisher => "Test";
        public override string Description => "Gioco di prova";
        public override string ImportHint => "set e numero";

        public override Task<IReadOnlyList<CatalogCard>> SearchAsync(string query, int limit, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CatalogCard>>(Cards.Values.Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(limit).ToList());

        public override Task<CatalogCard?> GetAsync(string externalId, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(Cards.TryGetValue(externalId, out var c) ? c : null);
        }

        public override Task<CatalogCard?> ResolveAsync(CatalogLookup l, CancellationToken ct = default) =>
            Task.FromResult(Cards.Values.FirstOrDefault(c =>
                (l.SetCode != null && l.Number != null && c.SetCode == l.SetCode && c.Number == l.Number)
                || (l.Name != null && c.Name.Equals(l.Name, StringComparison.OrdinalIgnoreCase))));
    }

    [Fact]
    public async Task Import_CreatesGameSetAndCard_ThenReusesFreshCard()
    {
        var first = await _provider.ImportAsync("c1");
        var second = await _provider.ImportAsync("c1");

        first!.IsNew.Should().BeTrue();
        second!.CardInfoId.Should().Be(first.CardInfoId);
        second.IsNew.Should().BeFalse();
        _provider.Calls.Should().Be(1, "una scheda aggiornata da meno di 24 ore non si richiede di nuovo");

        var info = await _context.CardInfos.Include(ci => ci.CardSet).ThenInclude(s => s.Game).SingleAsync();
        info.CatalogSource.Should().Be("fake");
        info.ExternalId.Should().Be("c1");
        info.PriceEurFoil.Should().Be(30m);
        info.ImageSmall.Should().Be("s.png");
        info.CardSet.Code.Should().Be("1");
        info.CardSet.Game.CatalogKey.Should().Be("fake");
        JsonDocument.Parse(info.GameData!).RootElement.GetProperty("ink").GetString().Should().Be("Amethyst");
        (await _provider.ImportAsync("missing")).Should().BeNull();
    }

    [Fact]
    public async Task Refresh_UpdatesPrices()
    {
        var id = (await _provider.ImportAsync("c2"))!.CardInfoId;
        _provider.Cards["c2"] = _provider.Cards["c2"] with { PriceEur = 3.5m };
        var info = await _context.CardInfos.FindAsync(id);

        (await _provider.RefreshAsync(new[] { info! })).Should().Be(1);
        info!.PriceEur.Should().Be(3.5m);
    }

    [Theory]
    [InlineData("Pokémon TCG", "pokemon", "Pokémon TCG", true)]
    [InlineData("pokemon", "pokemon", "Pokémon TCG", true)]
    [InlineData("Yu-Gi-Oh!", "yugioh", "Yu-Gi-Oh!", true)]
    [InlineData("Lorcana", "lorcana", "Disney Lorcana", true)]
    [InlineData("Magic: The Gathering", "lorcana", "Disney Lorcana", false)]
    [InlineData("One Piece TCG", "onepiece", "One Piece TCG", true)]
    public void GameMatches_RecognisesGameNames(string fileGame, string key, string name, bool expected)
    {
        CollectionImportService.GameMatches(fileGame, key, name).Should().Be(expected);
    }

    [Fact]
    public async Task CsvImport_OtherGame_PreviewAndCommitThroughProvider()
    {
        var user = new User { Username = "anna", Email = "a@t.it", FirstName = "Anna", PasswordHash = "x" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        var registry = new CatalogRegistry(new ICatalogProvider[] { _provider });
        var subs = new Mock<ISubscriptionService>();
        subs.Setup(s => s.GetUserActivePlanAsync(It.IsAny<int>())).ReturnsAsync((SubscriptionPlan?)null);
        var service = new CollectionImportService(_context, new Mock<IScryfallService>().Object, new Mock<IMagicCatalogService>().Object,
            subs.Object, new Mock<IWishlistAlertService>().Object, new Mock<ILogger<CollectionImportService>>().Object, registry);

        var csv = "Game,Name,Set code,Collector number,Quantity,Condition,Finish\n" +
                  "Fake Game,,1,42,2,NearMint,Foil\n" +          // set + numero, senza nome
                  "Fake Game,Stitch - Rock Star,,,1,Excellent,\n" +
                  "Magic,Lightning Bolt,m10,146,1,NM,\n" +          // altro gioco
                  "Fake Game,Inesistente,,,1,NM,\n";

        var preview = await service.PreviewAsync(csv, "fake");

        preview.ReadyCount.Should().Be(2);
        preview.Rows[0].Match!.ExternalId.Should().Be("c1");
        preview.Rows[0].Match!.PriceEur.Should().Be(30m, "foil");
        preview.Rows[2].Error.Should().Contain("altro gioco");
        preview.Rows[3].Error.Should().Contain("non trovata");

        var commit = await service.CommitAsync(user.Id, new ImportCommitRequest
        {
            IsAvailableForTrade = true,
            Rows = preview.Rows.Where(r => r.Match != null).Select(r => new ImportCommitRow
            {
                CatalogKey = r.Match!.CatalogKey, ExternalId = r.Match.ExternalId, Quantity = r.Quantity, Condition = r.Condition, Finish = r.Finish
            }).Append(new ImportCommitRow { CatalogKey = "fake", ExternalId = "forged", Quantity = 1 }).ToList()
        });

        commit.CreatedCopies.Should().Be(2);
        commit.Failed.Should().ContainSingle(f => f.Contains("forged"));
        var cards = await _context.Cards.Include(c => c.CardInfo).Where(c => c.UserId == user.Id).ToListAsync();
        cards.Should().HaveCount(2);
        cards.Single(c => c.CardInfo.ExternalId == "c1").Should().Match<Card>(c => c.Quantity == 2 && c.Finish == CardFinish.Foil && c.IsAvailableForTrade);
    }

    [Fact]
    public void EcbRate_IsInverted()
    {
        const string xml = "<gesmes:Envelope xmlns:gesmes=\"http://www.gesmes.org/xml/2002-08-01\" xmlns=\"http://www.ecb.int/vocabulary/2002-08-01/eurofxref\"><Cube><Cube time=\"2026-11-03\"><Cube currency=\"USD\" rate=\"1.25\"/><Cube currency=\"JPY\" rate=\"160\"/></Cube></Cube></gesmes:Envelope>";
        EcbExchangeRates.ParseUsdToEur(xml).Should().Be(0.8m);
    }

    [Fact]
    public void Lorcast_MapsCardWithStringOrNumberPrices()
    {
        const string json = """
        {"id":"crd_1","name":"Elsa","version":"Spirit of Winter","cost":"8","ink":"Amethyst","type":["Character"],
         "rarity":"Legendary","collector_number":"207","set":{"code":"1","name":"The First Chapter"},
         "image_uris":{"digital":{"small":"s.avif","normal":"n.avif"}},"prices":{"usd":null,"usd_foil":"952.84"}}
        """;
        var card = JsonSerializer.Deserialize<LorcanaProvider.LorcastCard>(json)!;
        var mapped = LorcanaProvider.Map(card, 0.9m);

        mapped.Name.Should().Be("Elsa - Spirit of Winter");
        mapped.SetCode.Should().Be("1");
        mapped.Number.Should().Be("207");
        mapped.PriceUsdFoil.Should().Be(952.84m);
        mapped.PriceEurFoil.Should().Be(857.56m);
        mapped.PriceEur.Should().BeNull();
        mapped.PriceConverted.Should().BeTrue();
        mapped.ImageLarge.Should().Be("n.avif");
    }
}
