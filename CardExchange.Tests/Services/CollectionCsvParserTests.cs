using CardExchange.API.Services.Import;
using CardExchange.Core.Entities;
using FluentAssertions;

namespace CardExchange.Tests.Services;

public class CollectionCsvParserTests
{
    [Fact]
    public void ManaBoxStyle_MapsColumnsAndValues()
    {
        var csv = "﻿Name,Set code,Set name,Collector number,Foil,Rarity,Quantity,ManaBox ID,Scryfall ID,Purchase price,Misprint,Altered,Condition,Language\n" +
                  "Lightning Bolt,m10,Magic 2010,146,foil,common,3,123,5c2c0f2f-1b7d-4b4f-9d8a-2a3e4e5f6a7b,0.5,false,false,near_mint,it\n" +
                  "\"Fire // Ice\",mh2,Modern Horizons 2,290,normal,uncommon,1,124,,0,false,false,light_played,en\n";

        var result = CollectionCsvParser.Parse(csv);

        result.Error.Should().BeNull();
        result.DetectedFormat.Should().Be("ManaBox");
        result.Rows.Should().HaveCount(2);

        var bolt = result.Rows[0];
        bolt.Name.Should().Be("Lightning Bolt");
        bolt.SetCode.Should().Be("m10");
        bolt.CollectorNumber.Should().Be("146");
        bolt.Quantity.Should().Be(3);
        bolt.Finish.Should().Be(CardFinish.Foil);
        bolt.Condition.Should().Be(CardCondition.NearMint);
        bolt.Language.Should().Be("it");
        bolt.ScryfallId.Should().Be("5c2c0f2f-1b7d-4b4f-9d8a-2a3e4e5f6a7b");
        bolt.LineNumber.Should().Be(2);

        var fireIce = result.Rows[1];
        fireIce.Name.Should().Be("Fire // Ice");
        fireIce.Condition.Should().Be(CardCondition.LightlyPlayed);
        fireIce.Finish.Should().Be(CardFinish.Normal);
        fireIce.ScryfallId.Should().BeNull();
    }

    [Fact]
    public void DragonShieldStyle_WithSepLineAndLanguageNames()
    {
        var csv = "sep=,\r\n" +
                  "Folder Name,Quantity,Trade Quantity,Card Name,Set Code,Set Name,Card Number,Condition,Printing,Language,Price Bought,Date Bought\r\n" +
                  "Binder,2,0,Counterspell,DMR,Dominaria Remastered,45,NearMint,Foil,Italian,1.00,2024-01-01\r\n" +
                  "Binder,1,0,Opt,DMR,Dominaria Remastered,60,Played,Normal,Japanese,0.10,2024-01-01\r\n";

        var result = CollectionCsvParser.Parse(csv);

        result.Error.Should().BeNull();
        result.DetectedFormat.Should().Be("Dragon Shield");
        result.Rows.Should().HaveCount(2);
        result.Rows[0].Name.Should().Be("Counterspell");
        result.Rows[0].SetCode.Should().Be("dmr");
        result.Rows[0].Finish.Should().Be(CardFinish.Foil);
        result.Rows[0].Language.Should().Be("it");
        result.Rows[0].LineNumber.Should().Be(3);
        result.Rows[1].Condition.Should().Be(CardCondition.ModeratelyPlayed);
        result.Rows[1].Language.Should().Be("ja");
    }

    [Fact]
    public void MoxfieldStyle_EditionCodeAndEtched()
    {
        var csv = "\"Count\",\"Tradelist Count\",\"Name\",\"Edition\",\"Condition\",\"Language\",\"Foil\",\"Tags\",\"Collector Number\"\n" +
                  "\"1\",\"1\",\"Sol Ring\",\"cmm\",\"Near Mint\",\"English\",\"etched\",\"\",\"464\"\n";

        var result = CollectionCsvParser.Parse(csv);

        result.DetectedFormat.Should().Be("Moxfield");
        var row = result.Rows.Single();
        row.SetCode.Should().Be("cmm");
        row.Finish.Should().Be(CardFinish.Etched);
        row.Language.Should().Be("en");
        row.Condition.Should().Be(CardCondition.NearMint);
    }

    [Fact]
    public void EditionWithSetName_IsKeptAsSetName()
    {
        var csv = "Count,Name,Edition\n1,Island,Kamigawa: Neon Dynasty\n";
        var row = CollectionCsvParser.Parse(csv).Rows.Single();
        row.SetCode.Should().BeNull();
        row.SetName.Should().Be("Kamigawa: Neon Dynasty");
    }

    [Fact]
    public void CardmarketStyle_SemicolonAndNumericLanguage()
    {
        var csv = "idProduct;Count;Name;Expansion;Condition;Language;isFoil\n" +
                  "12345;4;\"Goblin; Guide\";Zendikar;EX;5;1\n";

        var result = CollectionCsvParser.Parse(csv);

        result.DetectedFormat.Should().Be("Cardmarket");
        var row = result.Rows.Single();
        row.Name.Should().Be("Goblin; Guide");
        row.Quantity.Should().Be(4);
        row.Condition.Should().Be(CardCondition.Excellent);
        row.Language.Should().Be("it");
        row.Finish.Should().Be(CardFinish.Foil);
        row.SetName.Should().Be("Zendikar");
    }

    [Fact]
    public void InvalidValues_ProduceErrorsAndWarnings()
    {
        var csv = "Name,Quantity,Condition,Language\n" +
                  "Shock,abc,NM,en\n" +
                  "Shock,1,Like new,Klingon\n" +
                  ",1,NM,en\n";

        var rows = CollectionCsvParser.Parse(csv).Rows;

        rows[0].Error.Should().Contain("Quantità non valida");
        rows[1].Error.Should().BeNull();
        rows[1].Condition.Should().Be(CardCondition.NearMint);
        rows[1].Warnings.Should().HaveCount(2);
        rows[1].Language.Should().BeNull();
        rows[2].Error.Should().Be("Nome della carta mancante");
    }

    [Fact]
    public void QuotedFieldsWithNewlinesAndEscapedQuotes_AreParsed()
    {
        var csv = "Name,Quantity\n\"Say \"\"Hi\"\"\nTwice\",2\n";
        var row = CollectionCsvParser.Parse(csv).Rows.Single();
        row.Name.Should().Be("Say \"Hi\"\nTwice");
        row.Quantity.Should().Be(2);
    }

    [Fact]
    public void MissingNameColumn_ReturnsError()
    {
        CollectionCsvParser.Parse("Foo,Bar\n1,2\n").Error.Should().NotBeNull();
        CollectionCsvParser.Parse("").Error.Should().NotBeNull();
    }

    [Fact]
    public void BlankLines_AreSkipped()
    {
        var rows = CollectionCsvParser.Parse("Name\nOpt\n\n,\nShock\n").Rows;
        rows.Select(r => r.Name).Should().Equal("Opt", "Shock");
    }
}
