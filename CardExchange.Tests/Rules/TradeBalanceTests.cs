using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using FluentAssertions;

namespace CardExchange.Tests.Rules;

public class TradeBalanceTests
{
    [Theory]
    [InlineData(10, 10, TradeVerdict.Fair)]
    [InlineData(10.5, 10, TradeVerdict.Fair)]            // entro 1 € minimo
    [InlineData(105, 100, TradeVerdict.Fair)]            // entro il 10%
    [InlineData(120, 100, TradeVerdict.FavorsReceiver)]  // chi propone dà troppo
    [InlineData(80, 100, TradeVerdict.FavorsProposer)]   // chi propone riceve troppo
    [InlineData(0, 0.5, TradeVerdict.Fair)]
    public void Evaluate_UsesTenPercentOrOneEuroTolerance(double offered, double requested, TradeVerdict expected)
    {
        TradeBalance.Evaluate((decimal)offered, (decimal)requested).Verdict.Should().Be(expected);
    }

    [Fact]
    public void CopyValue_AppliesFinishAndCondition()
    {
        var info = new CardInfo { PriceEur = 10m, PriceEurFoil = 30m };
        CardPricing.MarketValueEur(new Card { CardInfo = info, Condition = CardCondition.NearMint }).Should().Be(10m);
        CardPricing.MarketValueEur(new Card { CardInfo = info, Condition = CardCondition.LightlyPlayed, Finish = CardFinish.Foil }).Should().Be(21m);
        CardPricing.MarketValueEur(new Card { CardInfo = new CardInfo(), EstimatedValue = 4m, Condition = CardCondition.Excellent }).Should().Be(3.6m);
        CardPricing.MarketValueEur(new Card { CardInfo = new CardInfo(), Condition = CardCondition.Mint }).Should().BeNull();
    }

    [Fact]
    public void SuggestOffer_ReachesTargetWithinTolerance()
    {
        var candidates = new[]
        {
            new OfferCandidate(1, 6m, 1, 2),
            new OfferCandidate(2, 3m, 3, 2),
            new OfferCandidate(3, 50m, 1, 2),
        };

        var picks = TradeBalance.SuggestOffer(candidates, 12m);
        var total = picks.Sum(p => candidates.First(c => c.CardId == p.CardId).UnitValue * p.Quantity);

        total.Should().BeInRange(12m - 1.2m, 12m + 1.2m);
        picks.Should().NotContain(p => p.CardId == 3, "la carta da 50 € sbilancerebbe lo scambio");
    }

    [Fact]
    public void SuggestOffer_PrefersHighPriorityWishlistCards()
    {
        var candidates = new[]
        {
            new OfferCandidate(1, 10m, 1, 3), // priorità bassa
            new OfferCandidate(2, 10m, 1, 1), // priorità alta
        };

        TradeBalance.SuggestOffer(candidates, 10m).Should().Equal((2, 1));
    }

    [Fact]
    public void SuggestOffer_WhenNothingFitsUnderTarget_AddsClosestCard()
    {
        // Target 5 €: l'unica carta vale 7 € (sopra la tolleranza) ma è meglio di niente
        TradeBalance.SuggestOffer(new[] { new OfferCandidate(1, 7m, 1, 1) }, 5m).Should().Equal((1, 1));
        // Ma non se peggiora la distanza (20 € contro un target di 5 €)
        TradeBalance.SuggestOffer(new[] { new OfferCandidate(1, 20m, 1, 1) }, 5m).Should().BeEmpty();
    }

    [Fact]
    public void SuggestOffer_UsesQuantitiesAndIgnoresUnpricedCards()
    {
        var picks = TradeBalance.SuggestOffer(new[]
        {
            new OfferCandidate(1, 2m, 5, 1),
            new OfferCandidate(2, 0m, 3, 1),
        }, 6m);

        picks.Should().Equal((1, 3));
    }

    [Fact]
    public void SuggestOffer_PrefersBalancedOverHighPriorityWhenPriorityWouldUnbalance()
    {
        // Caso reale: target 5,62 €. Counterspell (priorità alta) + Fire // Ice restano a 3,98 €;
        // una sola Lightning Bolt (5,47 €) bilancia lo scambio.
        var candidates = new[]
        {
            new OfferCandidate(1, 3.83m, 1, 1), // Counterspell
            new OfferCandidate(2, 5.47m, 5, 2), // Lightning Bolt
            new OfferCandidate(3, 0.15m, 1, 2), // Fire // Ice
        };

        TradeBalance.SuggestOffer(candidates, 5.62m).Should().Equal((2, 1));
    }

    [Fact]
    public void SuggestOffer_WithManyUnits_StillFindsBalancedOffer()
    {
        // Oltre il limite della ricerca esaustiva: 20 copie da 0,50 € e 5 da 3 €
        var candidates = new[]
        {
            new OfferCandidate(1, 0.5m, 20, 1),
            new OfferCandidate(2, 3m, 5, 2),
        };

        var picks = TradeBalance.SuggestOffer(candidates, 9m);
        var total = picks.Sum(p => (p.CardId == 1 ? 0.5m : 3m) * p.Quantity);

        total.Should().BeInRange(8m, 10m);
    }
}

