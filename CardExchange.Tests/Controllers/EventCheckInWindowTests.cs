using CardExchange.API.Controllers;
using CardExchange.Core.Entities;
using FluentAssertions;

namespace CardExchange.Tests.Controllers;

public class EventCheckInWindowTests
{
    private static readonly DateTime Start = new(2026, 11, 10, 19, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void OpensHalfHourBefore_ClosesAtEnd()
    {
        var (opens, closes) = EventsController.CheckInWindow(new Event { StartDate = Start, EndDate = Start.AddHours(3) });
        opens.Should().Be(Start.AddMinutes(-30));
        closes.Should().Be(Start.AddHours(3));
    }

    [Fact]
    public void WithoutEndDate_ClosesSixHoursAfterStart()
    {
        EventsController.CheckInWindow(new Event { StartDate = Start }).Closes.Should().Be(Start.AddHours(6));
    }
}
