using CardExchange.Core.Rules;
using FluentAssertions;

namespace CardExchange.Tests.Services;

public class TimeZonesTests
{
    private readonly DateTime _utc = new(2026, 11, 7, 14, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void FormatsInRecipientTimeZone_DefaultItaly()
    {
        TimeZones.FormatWhen(_utc, null).Should().Be("sabato 7 novembre alle 15:30");
        TimeZones.FormatWhen(_utc, "Europe/London").Should().Be("sabato 7 novembre alle 14:30");
        TimeZones.FormatTime(_utc, "America/New_York").Should().Be("09:30");
        TimeZones.FormatTime(_utc, "Not/AZone").Should().Be("15:30", "un fuso sconosciuto usa quello italiano");
    }

    [Fact]
    public void Validates()
    {
        TimeZones.IsValid("Europe/Rome").Should().BeTrue();
        TimeZones.IsValid("").Should().BeFalse();
        TimeZones.IsValid("Mars/Olympus").Should().BeFalse();
    }

    [Fact]
    public void BroadcastBody_UsesRecipientZone()
    {
        BroadcastRules.Body("Fiera", null, _utc, null, "Europe/Lisbon").Should().Be("Fiera · fino alle 14:30");
        BroadcastRules.Body("Fiera", null, _utc, null).Should().Be("Fiera · fino alle 15:30");
    }
}
