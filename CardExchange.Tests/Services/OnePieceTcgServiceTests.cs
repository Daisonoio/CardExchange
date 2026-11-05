using System.Net;
using System.Text;
using CardExchange.API.Services;
using CardExchange.API.Services.Catalog;
using CardExchange.Core.Entities;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardExchange.Tests.Services;

/// <summary>Catalogo One Piece su OPTCG API, con un server finto che risponde come quello vero.</summary>
public class OnePieceTcgServiceTests
{
    private sealed class FakeApi : HttpMessageHandler
    {
        public readonly Dictionary<string, (HttpStatusCode Status, string Body)> Routes = new();
        public readonly List<string> Calls = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.PathAndQuery;
            Calls.Add(path);
            var (status, body) = Routes.TryGetValue(path, out var r) ? r : (HttpStatusCode.NotFound, "{\"error\":\"Card not found.\"}");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private static string Print(string imageId, string name, string set = "Romance Dawn", decimal? price = 2.0m, string cost = "null", string power = "\"5000\"") => $$"""
        {"inventory_price":1.0,"market_price":{{(price?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null")}},"card_name":"{{name}}","set_name":"{{set}}",
         "card_text":"[On Play] Draw 1 card.","set_id":"OP-01","rarity":"L","card_set_id":"{{imageId.Split('_')[0]}}","card_color":"Red",
         "card_type":"Leader","life":"5","card_cost":{{cost}},"card_power":{{power}},"sub_types":"Straw Hat Crew","counter_amount":1000,
         "attribute":"Slash","card_image_id":"{{imageId}}","card_image":"https://optcgapi.com/media/static/Card_Images/{{imageId}}.jpg"}
        """;

    private static (OnePieceTcgService Service, FakeApi Api) Create()
    {
        var api = new FakeApi();
        var fx = new Mock<IExchangeRates>();
        fx.Setup(f => f.UsdToEurAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0.9m);
        var http = new HttpClient(api) { BaseAddress = new Uri(OnePieceTcgService.BaseUrl) };
        var service = new OnePieceTcgService(http, new MemoryCache(new MemoryCacheOptions()), fx.Object, new Mock<ILogger<OnePieceTcgService>>().Object);
        return (service, api);
    }

    [Fact]
    public async Task GetByCode_PicksTheExactPrint_AndConvertsPrice()
    {
        var (service, api) = Create();
        api.Routes["/api/sets/card/OP01-001/"] = (HttpStatusCode.OK,
            $"[{Print("OP01-001", "Roronoa Zoro (001)", price: 2.19m)},{Print("OP01-001_p1", "Roronoa Zoro (001) (Parallel)", price: 568.01m)}]");

        var basePrint = await service.GetCardByCodeAsync("OP01-001");
        var parallel = await service.GetCardByCodeAsync("op01-001_P1");

        basePrint!.Name.Should().Be("Roronoa Zoro");
        basePrint.PriceEur.Should().Be(1.97m);
        basePrint.Power.Should().Be(5000);
        basePrint.Counter.Should().Be(1000);
        basePrint.Cost.Should().BeNull();
        parallel!.Code.Should().Be("OP01-001_p1");
        parallel.Name.Should().Be("Roronoa Zoro (Parallel)");
        parallel.PriceEur.Should().Be(511.21m);
    }

    [Fact]
    public async Task GetByCode_StarterAndPromo_UseTheirCollections()
    {
        var (service, api) = Create();
        api.Routes["/api/decks/card/ST01-001/"] = (HttpStatusCode.OK, $"[{Print("ST01-001", "Monkey.D.Luffy (001)", "Starter Deck 1: Straw Hat Crew")}]");
        api.Routes["/api/promos/card/P-001/"] = (HttpStatusCode.OK, $"[{Print("P-001", "Monkey.D.Luffy (Alternate Art)", "One Piece Promotion Cards")}]");

        (await service.GetCardByCodeAsync("ST01-001"))!.Set!.Name.Should().Be("Starter Deck 1: Straw Hat Crew");
        (await service.GetCardByCodeAsync("P-001"))!.Name.Should().Be("Monkey.D.Luffy (Alternate Art)");
        api.Calls.Should().NotContain(c => c.StartsWith("/api/sets/"));
        (await service.GetCardByCodeAsync("OP01-999")).Should().BeNull();
    }

    [Fact]
    public async Task Search_MergesCollections_WithoutDuplicates_AndCachesPrintsForImport()
    {
        var (service, api) = Create();
        api.Routes["/api/sets/filtered/?card_name=luffy"] = (HttpStatusCode.OK,
            $"[{Print("OP05-119", "Monkey.D.Luffy (119)")},{Print("OP01-024", "Monkey.D.Luffy (024)")},{Print("OP01-024_p1", "Monkey.D.Luffy (024) (Parallel)")}]");
        api.Routes["/api/decks/filtered/?card_name=luffy"] = (HttpStatusCode.OK, $"[{Print("ST01-001", "Monkey.D.Luffy (001)")}]");
        api.Routes["/api/promos/filtered/?card_name=luffy"] = (HttpStatusCode.OK, $"[{Print("OP01-024", "Monkey.D.Luffy (024)")},{Print("P-001", "Monkey.D.Luffy")}]");

        var result = await service.SearchCardsAsync("luffy");

        // Tutti "Monkey.D.Luffy" (una parola inizia con la ricerca): ordinati per codice, base prima delle varianti
        result!.Data.Select(c => c.Code).Should().Equal("OP01-024", "OP01-024_p1", "OP05-119", "P-001", "ST01-001");
        result.Total.Should().Be(5);

        var calls = api.Calls.Count;
        (await service.GetCardByCodeAsync("OP01-024_p1"))!.Name.Should().Be("Monkey.D.Luffy (Parallel)");
        api.Calls.Count.Should().Be(calls, "la carta appena trovata con la ricerca non richiede un'altra chiamata");
    }

    [Theory]
    [InlineData("Monkey.D.Luffy (Parallel)", "monkey.d.luffy", 0)]
    [InlineData("Monkey.D.Luffy", "luffy", 1)]
    [InlineData("Luffy, Now's Your Chance", "luffy", 1)]
    [InlineData("Gum-Gum Pistol", "luffy", 2)]
    public void NameRank_ExactThenWordStart(string name, string query, int rank) =>
        OnePieceTcgService.NameRank(name, query).Should().Be(rank);

    [Fact]
    public async Task Search_ByCode_ReturnsAllPrints()
    {
        var (service, api) = Create();
        api.Routes["/api/sets/card/OP01-024/"] = (HttpStatusCode.OK,
            $"[{Print("OP01-024_p1", "Monkey.D.Luffy (024) (Parallel)")},{Print("OP01-024", "Monkey.D.Luffy (024)")}]");

        var result = await service.SearchCardsAsync("op01-024");

        result!.Data.Select(c => c.Code).Should().Equal("OP01-024", "OP01-024_p1");
        api.Calls.Should().NotContain(c => c.Contains("filtered"));
    }

    [Fact]
    public async Task Search_NoResults_IsEmpty_ButServiceDown_IsNull()
    {
        var (service, api) = Create();
        (await service.SearchCardsAsync("zzzzz"))!.Data.Should().BeEmpty();

        foreach (var c in new[] { "sets", "decks", "promos" })
            api.Routes[$"/api/{c}/filtered/?card_name=down"] = (HttpStatusCode.BadGateway, "");
        (await service.SearchCardsAsync("down")).Should().BeNull();
    }

    [Fact]
    public void MapToCardInfo_StoresPriceImagesAndPrintCode()
    {
        var card = OnePieceTcgService.Map(System.Text.Json.JsonSerializer.Deserialize<CardExchange.API.DTOs.OnePieceTcg.OptcgApiCard>(
            Print("OP01-001_p1", "Roronoa Zoro (001) (Parallel)", price: 10m))!, 0.9m);
        var info = new CardInfo();
        Create().Service.MapToCardInfo(card, info);

        info.OnePieceTcgId.Should().Be("OP01-001_p1");
        info.CardNumber.Should().Be("OP01-001");
        info.PriceEur.Should().Be(9m);
        info.PriceUsd.Should().Be(10m);
        info.ImageUrl.Should().EndWith("OP01-001_p1.jpg");
    }
}
