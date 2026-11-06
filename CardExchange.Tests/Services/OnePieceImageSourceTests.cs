using CardExchange.API.DTOs.OnePieceTcg;
using CardExchange.API.Services;
using CardExchange.API.Services.Catalog;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CardExchange.Tests.Services;

/// <summary>Sorgente delle immagini One Piece: invariata finché non si configura un modello (diritti verificati).</summary>
public class OnePieceImageSourceTests
{
    private const string Template = "https://img.example.it/op/{set}/{code}.webp";

    [Fact]
    public void UrlFor_FillsCodeSetAndNumber()
    {
        OnePieceImageSource.UrlFor("op01-001_P1", Template).Should().Be("https://img.example.it/op/OP01/OP01-001_p1.webp");
        OnePieceImageSource.UrlFor("ST01-012", "https://x.it/{number}.png").Should().Be("https://x.it/ST01-012.png");
        OnePieceImageSource.UrlFor("OP01-001", null).Should().BeNull();
    }

    [Fact]
    public void Template_IgnoresEmptyOrMalformedValues()
    {
        IConfiguration Config(string? v) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [OnePieceImageSource.ConfigKey] = v }).Build();
        OnePieceImageSource.Template(Config("")).Should().BeNull();
        OnePieceImageSource.Template(Config("https://x.it/immagine.png")).Should().BeNull();
        OnePieceImageSource.Template(Config(Template)).Should().Be(Template);
        OnePieceImageSource.Template(null).Should().BeNull();
    }

    [Fact]
    public void Map_UsesTemplateOnlyWhenConfigured()
    {
        var api = new OptcgApiCard { CardImageId = "OP01-001", CardName = "Roronoa Zoro (001)", CardImage = "https://optcgapi.com/media/static/Card_Images/OP01-001.jpg" };
        OnePieceTcgService.Map(api, 0.9m).Images!.Large.Should().Be(api.CardImage);
        OnePieceTcgService.Map(api, 0.9m, Template).Images!.Large.Should().Be("https://img.example.it/op/OP01/OP01-001.webp");
    }

    [Fact]
    public async Task Backfill_MovesOnlyOnePieceCards_AndIsIdempotent()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var set = new CardSet { Name = "Romance Dawn", Code = "OP01", Game = new Game { Name = "One Piece TCG", Publisher = "Bandai" } };
        db.CardInfos.AddRange(
            new CardInfo { Name = "Zoro", CardSet = set, OnePieceTcgId = "OP01-001", ImageUrl = "https://optcgapi.com/a.jpg" },
            new CardInfo { Name = "Zoro (Parallel)", CardSet = set, OnePieceTcgId = "OP01-001_p1", ImageUrl = "https://optcgapi.com/b.jpg" },
            new CardInfo { Name = "Pikachu", CardSet = set, ImageUrl = "https://pokemon/x.png" });
        await db.SaveChangesAsync();

        (await OnePieceImageSource.BackfillAsync(db, Template, batchSize: 1)).Should().Be(2);
        (await OnePieceImageSource.BackfillAsync(db, Template)).Should().Be(0);
        (await OnePieceImageSource.BackfillAsync(db, null)).Should().Be(0);

        var all = await db.CardInfos.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
        all[1].ImageSmall.Should().Be("https://img.example.it/op/OP01/OP01-001_p1.webp");
        all[2].ImageUrl.Should().Be("https://pokemon/x.png");
    }
}
