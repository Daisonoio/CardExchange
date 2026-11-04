using CardExchange.API.Controllers;
using FluentAssertions;

namespace CardExchange.Tests.Controllers;

public class ExplorePrivacyTests
{
    [Theory]
    [InlineData(0.05, 1)]   // anche a pochi metri si mostra "~1 km"
    [InlineData(1.4, 1)]
    [InlineData(1.6, 2)]
    [InlineData(23.5, 24)]
    public void Distance_IsRoundedToWholeKm_WithMinimumOne(double km, int expected)
    {
        ExploreController.ApproxKm(km).Should().Be(expected);
        ExploreController.ApproxKm(null).Should().BeNull();
    }

    [Fact]
    public void NearbyUsers_ShareTheSameArea_AndAreaIsNotTheirPosition()
    {
        // Due utenti a poche centinaia di metri nella stessa cella: stessa zona sulla mappa
        var a = ExploreController.AreaOf(45.4642, 9.1900);
        var b = ExploreController.AreaOf(45.4655, 9.1932);
        a.Should().Be(b);
        a.Should().NotBe((45.4642, 9.1900));
        // Il centro della zona è entro mezza cella (circa 1,1 km) dal punto reale
        Math.Abs(a.Lat - 45.4642).Should().BeLessThanOrEqualTo(ExploreController.AreaCellDegrees / 2);
        Math.Abs(a.Lng - 9.1900).Should().BeLessThanOrEqualTo(ExploreController.AreaCellDegrees / 2);
    }
}
