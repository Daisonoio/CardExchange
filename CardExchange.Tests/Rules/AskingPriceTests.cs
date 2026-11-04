using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using FluentAssertions;

namespace CardExchange.Tests.Rules;

/// <summary>
/// Prezzo indicato dal proprietario: indicativo, ha la precedenza sul valore di mercato per mostrare il prezzo,
/// ma non vincola gli accordi tra utenti.
/// </summary>
public class AskingPriceTests
{
    private static Card CardWorth(decimal market, decimal? asking = null) =>
        new() { CardInfo = new CardInfo { PriceEur = market }, Condition = CardCondition.NearMint, AskingPrice = asking };

    [Fact]
    public void EffectivePrice_UsesAskingPrice_WhenSet()
    {
        var card = CardWorth(10m, asking: 5m);
        CardPricing.MarketValueEur(card).Should().Be(10m, "il valore di mercato resta il riferimento");
        CardPricing.EffectivePriceEur(card).Should().Be(5m);
    }

    [Fact]
    public void EffectivePrice_FallsBackToMarketValue()
    {
        CardPricing.EffectivePriceEur(CardWorth(10m)).Should().Be(10m);
    }

    [Fact]
    public void WishlistMaxPrice_IsComparedWithAskingPrice()
    {
        var wish = new WishlistItem { MaxPrice = 6m };
        WishlistMatchRules.Satisfies(CardWorth(10m, asking: 5m), wish).Should().BeTrue("la cede a 5 €, sotto il massimo di 6 €");
        WishlistMatchRules.Satisfies(CardWorth(10m), wish).Should().BeFalse("senza prezzo indicato vale il mercato, 10 €");
    }

    [Fact]
    public void Suggestion_TargetsAskingPrice_NotMarketValue()
    {
        // Esempio: chiedo una carta da 10 € di mercato che il proprietario cede a 5 €; io ho tre carte da 6 €.
        var requested = CardWorth(10m, asking: 5m);
        var target = CardPricing.EffectivePriceEur(requested)!.Value;

        var picks = TradeBalance.SuggestOffer(new[] { new OfferCandidate(1, 6m, 3, 1) }, target);

        picks.Should().Equal((1, 1)); // una sola carta da 6 € basta come proposta di partenza
    }

    [Fact]
    public void Evaluation_DoesNotPreventUnevenDeals()
    {
        // Tre carte da 6 € per una da 10 €: il confronto è solo informativo, nessuna regola lo blocca
        var result = TradeBalance.Evaluate(offeredTotal: 18m, requestedTotal: 10m);
        result.Difference.Should().Be(8m);
    }
}
