using CardExchange.Core.Rules;
using FluentAssertions;

namespace CardExchange.Tests.Rules;

public class StoreSubmissionRulesTests
{
    private static StoreSubmissionRules.Fields Complete() => new(
        "Games Corner", "Tornei ogni settimana, area gioco e scambi.", "Via Torino 12", "Milano", "MI", "20123", "IT",
        45.46m, 9.18m, "+39 02 1234567", "www.gamescorner.it", "Mar-Sab 10-19", new[] { 1, 2 });

    [Fact]
    public void Complete_IsValid_InstagramNotNeeded() =>
        StoreSubmissionRules.Check(Complete()).IsValid.Should().BeTrue();

    [Fact]
    public void EveryFieldButInstagram_IsRequired()
    {
        var empty = new StoreSubmissionRules.Fields("", " ", "", "", "", "", "IT", 0, 0, "", "", "", Array.Empty<int>());
        StoreSubmissionRules.Check(empty).Missing.Should().BeEquivalentTo(
            "name", "description", "address", "city", "province", "postalCode", "location", "openingHours", "phone", "website", "games");
    }

    [Fact]
    public void Formats_AreChecked()
    {
        var bad = Complete() with { Description = "Troppo corta", PostalCode = "2012", Phone = "chiamami", Website = "non un sito" };
        var result = StoreSubmissionRules.Check(bad);
        result.Missing.Should().BeEmpty();
        result.Invalid.Should().BeEquivalentTo("description", "postalCode", "phone", "website");
    }

    [Theory]
    [InlineData("www.esempio.it", "https://www.esempio.it")]
    [InlineData("http://esempio.it/negozio/", "http://esempio.it/negozio")]
    [InlineData("esempio", null)]
    public void Website_IsNormalized(string input, string? expected) =>
        StoreSubmissionRules.NormalizeWebsite(input).Should().Be(expected);
}
