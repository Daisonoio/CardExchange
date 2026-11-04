using CardExchange.Core.Rules;
using FluentAssertions;

namespace CardExchange.Tests.Rules;

public class AgeRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 4);

    [Theory]
    [InlineData("2012-11-04", 14)] // compie 14 anni oggi
    [InlineData("2012-11-05", 13)] // li compie domani
    [InlineData("2008-11-03", 18)]
    [InlineData("1990-01-01", 36)]
    public void AgeOn_CountsCompletedYears(string birth, int expected)
    {
        AgeRules.AgeOn(DateOnly.Parse(birth), Today).Should().Be(expected);
    }

    [Fact]
    public void LeapDayBirthday_CountsOnMarch1InNonLeapYears()
    {
        var birth = new DateOnly(2012, 2, 29);
        AgeRules.AgeOn(birth, new DateOnly(2026, 2, 28)).Should().Be(13);
        AgeRules.AgeOn(birth, new DateOnly(2026, 3, 1)).Should().Be(14);
        AgeRules.AgeOn(birth, new DateOnly(2028, 2, 29)).Should().Be(16);
    }

    [Fact]
    public void MinimumAgeAndPlausibility()
    {
        AgeRules.MeetsMinimum(new DateOnly(2012, 11, 4), Today).Should().BeTrue();
        AgeRules.MeetsMinimum(new DateOnly(2012, 11, 5), Today).Should().BeFalse();
        AgeRules.IsMinor(new DateOnly(2010, 1, 1), Today).Should().BeTrue();
        AgeRules.IsPlausible(Today.AddDays(1), Today).Should().BeFalse();
        AgeRules.IsPlausible(new DateOnly(1890, 1, 1), Today).Should().BeFalse();
        AgeRules.IsPlausible(new DateOnly(1950, 1, 1), Today).Should().BeTrue();
    }
}
