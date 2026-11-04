using System.Text;
using CardExchange.API.Services.Import;
using FluentAssertions;

namespace CardExchange.Tests.Services;

public class CsvExportTests
{
    [Theory]
    [InlineData("Lightning Bolt", "Lightning Bolt")]
    [InlineData("Fire // Ice", "Fire // Ice")]
    [InlineData("Borrowing 100,000 Arrows", "\"Borrowing 100,000 Arrows\"")]
    [InlineData("detto \"buono\"", "\"detto \"\"buono\"\"\"")]
    [InlineData("=HYPERLINK(\"http://x\")", "\"'=HYPERLINK(\"\"http://x\"\")\"")]
    [InlineData("+39 333", "'+39 333")]
    [InlineData("-5", "'-5")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("riga1\nriga2", "\"riga1\nriga2\"")]
    [InlineData(null, "")]
    public void Field_QuotesAndNeutralizesFormulas(string? input, string expected)
    {
        CsvExport.Field(input).Should().Be(expected);
    }

    [Fact]
    public void ToBytes_HasBomAndInvariantNumbers()
    {
        var bytes = CsvExport.ToBytes(new[] { "Name", "Value" }, new[] { new[] { "Sol Ring", CsvExport.Number(2.5m) } });
        bytes.Take(3).Should().Equal(Encoding.UTF8.GetPreamble());
        Encoding.UTF8.GetString(bytes.Skip(3).ToArray()).Should().Be("Name,Value\r\nSol Ring,2.50\r\n");
    }

    [Fact]
    public void ExportedCollection_IsReadBackByOurImport()
    {
        var header = new[] { "Game", "Name", "Set code", "Set name", "Collector number", "Quantity", "Condition", "Language", "Finish",
            "Rarity", "For trade", "Market value EUR", "Asking price EUR", "Scryfall ID", "Notes" };
        var rows = new[]
        {
            new[] { "Magic", "Borrowing 100,000 Arrows", "por", "Portal Three Kingdoms", "1", "3", "LightlyPlayed", "it", "Foil",
                "uncommon", "yes", "1.20", "", "0f3c3a8e-7a0b-4c55-9d1e-2b1f0c9a1d11", "=cmd" },
        };
        var text = Encoding.UTF8.GetString(CsvExport.ToBytes(header, rows)).TrimStart('\uFEFF');

        var parsed = CardExchange.API.Services.Import.CollectionCsvParser.Parse(text);
        var row = parsed.Rows.Single();
        row.Name.Should().Be("Borrowing 100,000 Arrows");
        row.SetCode.Should().Be("por");
        row.CollectorNumber.Should().Be("1");
        row.Quantity.Should().Be(3);
        row.Condition.Should().Be(CardExchange.Core.Entities.CardCondition.LightlyPlayed);
        row.Language.Should().Be("it");
        row.Finish.Should().Be(CardExchange.Core.Entities.CardFinish.Foil);
        row.ScryfallId.Should().Be("0f3c3a8e-7a0b-4c55-9d1e-2b1f0c9a1d11");
    }
}
