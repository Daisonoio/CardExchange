using CardExchange.Core.Constants;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using FluentAssertions;

namespace CardExchange.Tests.Rules;

public class WishlistMatchRulesTests
{
    private static Card CardWith(CardCondition condition = CardCondition.NearMint, string? language = "en",
        CardFinish finish = CardFinish.Normal, decimal? value = null) =>
        new() { Condition = condition, Language = language, Finish = finish, EstimatedValue = value };

    [Fact]
    public void WishlistWithoutPreferences_AcceptsAnyCard()
    {
        WishlistMatchRules.Satisfies(CardWith(CardCondition.Damaged, null, CardFinish.Etched), new WishlistItem())
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(CardCondition.Mint, true)]           // migliore
    [InlineData(CardCondition.Excellent, true)]      // uguale
    [InlineData(CardCondition.LightlyPlayed, false)] // peggiore
    public void Condition_AcceptsEqualOrBetter(CardCondition cardCondition, bool expected)
    {
        var wish = new WishlistItem { PreferredCondition = CardCondition.Excellent };
        WishlistMatchRules.Satisfies(CardWith(cardCondition), wish).Should().Be(expected);
    }

    [Theory]
    [InlineData("it", true)]
    [InlineData("en", false)]
    [InlineData(null, false)] // lingua non indicata: non soddisfa una lingua specifica
    public void Language_MustMatchWhenRequested(string? cardLanguage, bool expected)
    {
        var wish = new WishlistItem { PreferredLanguage = "it" };
        WishlistMatchRules.Satisfies(CardWith(language: cardLanguage), wish).Should().Be(expected);
    }

    [Theory]
    [InlineData(CardFinish.Foil, true)]
    [InlineData(CardFinish.Normal, false)]
    [InlineData(CardFinish.ReverseHolo, false)]
    public void Finish_MustMatchWhenRequested(CardFinish cardFinish, bool expected)
    {
        var wish = new WishlistItem { PreferredFinish = CardFinish.Foil };
        WishlistMatchRules.Satisfies(CardWith(finish: cardFinish), wish).Should().Be(expected);
    }

    [Theory]
    [InlineData(9.99, true)]
    [InlineData(10.00, true)]
    [InlineData(10.01, false)]
    public void MaxPrice_RejectsMoreExpensiveCards(double value, bool expected)
    {
        var wish = new WishlistItem { MaxPrice = 10m };
        WishlistMatchRules.Satisfies(CardWith(value: (decimal)value), wish).Should().Be(expected);
    }

    [Fact]
    public void MaxPrice_AcceptsCardsWithoutValue()
    {
        WishlistMatchRules.Satisfies(CardWith(value: null), new WishlistItem { MaxPrice = 1m }).Should().BeTrue();
    }

    [Fact]
    public void ReferencePrice_UsesFoilPriceForNonNormalFinishes()
    {
        var info = new CardInfo { PriceEur = 2m, PriceEurFoil = 9m };
        CardPricing.ReferencePriceEur(info, CardFinish.Normal).Should().Be(2m);
        CardPricing.ReferencePriceEur(info, CardFinish.Foil).Should().Be(9m);
        CardPricing.ReferencePriceEur(new CardInfo { PriceEur = 2m }, CardFinish.Foil).Should().Be(2m);
        CardPricing.ReferencePriceEur(null, CardFinish.Foil, fallback: 5m).Should().Be(5m);
    }

    [Theory]
    [InlineData(" IT ", "it")]
    [InlineData("ja", "ja")]
    [InlineData("xx", null)]
    [InlineData("", null)]
    public void Languages_Normalize(string input, string? expected)
    {
        CardLanguages.Normalize(input).Should().Be(expected);
    }
}
