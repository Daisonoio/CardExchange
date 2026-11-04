using CardExchange.API.Services;
using FluentAssertions;

namespace CardExchange.Tests.Services;

public class ReferralCodesTests
{
    [Fact]
    public void Generate_ProducesUnambiguousCodes()
    {
        var codes = Enumerable.Range(0, 500).Select(_ => ReferralCodes.Generate()).ToList();
        codes.Should().OnlyContain(c => c.Length == ReferralCodes.Length && !c.Any(ch => "01OIL".Contains(ch)));
        codes.Distinct().Count().Should().BeGreaterThan(495);
    }

    [Theory]
    [InlineData(" abcd2345 ", "ABCD2345")]
    [InlineData("ABCD234", null)]   // troppo corto
    [InlineData("ABCD2340", null)]  // contiene 0
    [InlineData(null, null)]
    public void Normalize_AcceptsOnlyValidCodes(string? input, string? expected)
    {
        ReferralCodes.Normalize(input).Should().Be(expected);
    }
}
