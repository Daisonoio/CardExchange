using CardExchange.API.Services.Security;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace CardExchange.Tests.Services;

public class LoginAttemptGuardTests
{
    private readonly LoginAttemptGuard _guard = new(new MemoryCache(new MemoryCacheOptions()));
    private readonly DateTime _t0 = new(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FiveFailures_LockTheAccount_ForFifteenMinutes_CaseInsensitive()
    {
        for (var i = 0; i < 4; i++) _guard.RecordFailure("Mario", _t0.AddMinutes(i));
        _guard.LockedForMinutes("mario", _t0.AddMinutes(4)).Should().BeNull();

        _guard.RecordFailure("MARIO", _t0.AddMinutes(4));
        _guard.LockedForMinutes("mario", _t0.AddMinutes(5)).Should().Be(10);
        _guard.LockedForMinutes("mario", _t0.AddMinutes(16)).Should().BeNull("il primo errore è uscito dalla finestra");
        _guard.LockedForMinutes("giulia", _t0.AddMinutes(5)).Should().BeNull("gli altri account non sono toccati");
    }

    [Fact]
    public void SuccessfulLogin_ResetsTheCount()
    {
        for (var i = 0; i < 4; i++) _guard.RecordFailure("anna", _t0);
        _guard.Reset("anna");
        _guard.RecordFailure("anna", _t0);
        _guard.LockedForMinutes("anna", _t0).Should().BeNull();
    }
}
