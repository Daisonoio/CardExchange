using CardExchange.Core.Rules;
using FluentAssertions;

namespace CardExchange.Tests.Rules;

public class SlugsTests
{
    [Theory]
    [InlineData("Games Corner Milano", "games-corner-milano")]
    [InlineData("L'Angolo del Gioco — Città Studi!", "l-angolo-del-gioco-citta-studi")]
    [InlineData("  ***  ", "negozio")]
    [InlineData("Pokémon & Co. 2", "pokemon-co-2")]
    public void From_MakesReadableIds(string text, string expected) => Slugs.From(text).Should().Be(expected);

    [Fact]
    public void From_IsCappedInLength() => Slugs.From(new string('a', 200)).Length.Should().BeLessThanOrEqualTo(Slugs.MaxLength);

    [Fact]
    public void Unique_AppendsTheFirstFreeNumber()
    {
        var taken = new HashSet<string> { "games-corner-milano", "games-corner-milano-2" };
        Slugs.Unique("games-corner-milano", taken.Contains).Should().Be("games-corner-milano-3");
        Slugs.Unique("altro", taken.Contains).Should().Be("altro");
    }
}
