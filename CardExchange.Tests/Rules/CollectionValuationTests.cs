using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using FluentAssertions;

namespace CardExchange.Tests.Rules;

public class CollectionValuationTests
{
    private static readonly DateTime D0 = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Series_CarriesLastKnownPriceForward_AndAppliesQuantityConditionFinish()
    {
        var holdings = new[]
        {
            new HoldingLine(1, CardFinish.Normal, CardCondition.NearMint, 2),     // 2 x 10 -> 2 x 12
            new HoldingLine(2, CardFinish.Foil, CardCondition.LightlyPlayed, 1),  // foil 30 al 70%
        };
        var history = new[]
        {
            new PricePoint(1, D0, 10m, 20m),
            new PricePoint(1, D0.AddDays(2), 12m, 20m),
            new PricePoint(2, D0, 5m, 30m),
        };

        var series = CollectionValuation.Series(holdings, history, D0, D0.AddDays(3));

        series.Select(p => p.Value).Should().Equal(41m, 41m, 45m, 45m);
        series[0].Date.Should().Be(D0);
    }

    [Fact]
    public void Series_BackfillsWithFirstPrice_SoNewHistoryIsNotAGain()
    {
        var holdings = new[] { new HoldingLine(1, CardFinish.Normal, CardCondition.Mint, 1) };
        var history = new[] { new PricePoint(1, D0.AddDays(5), 8m, null) };

        CollectionValuation.Series(holdings, history, D0, D0.AddDays(6)).Should().OnlyContain(p => p.Value == 8m);
    }

    [Fact]
    public void Series_FoilWithoutFoilPrice_UsesBasePrice_AndUnpricedCardsAreIgnored()
    {
        var holdings = new[]
        {
            new HoldingLine(1, CardFinish.Foil, CardCondition.NearMint, 1),
            new HoldingLine(3, CardFinish.Normal, CardCondition.NearMint, 4),
        };
        var history = new[] { new PricePoint(1, D0, 6m, null), new PricePoint(3, D0, null, null) };

        CollectionValuation.Series(holdings, history, D0, D0).Single().Value.Should().Be(6m);
    }

    [Fact]
    public void Series_EmptyOrInvertedRange()
    {
        CollectionValuation.Series(Array.Empty<HoldingLine>(), Array.Empty<PricePoint>(), D0, D0.AddDays(1))
            .Should().HaveCount(2).And.OnlyContain(p => p.Value == 0);
        CollectionValuation.Series(Array.Empty<HoldingLine>(), Array.Empty<PricePoint>(), D0, D0.AddDays(-1)).Should().BeEmpty();
    }
}
