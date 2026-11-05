using CardExchange.API.Services.Stores;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CardExchange.Tests.Services;

public class StoreStatsTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly DateTime _nov = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    public void Dispose() => _db.Dispose();

    private void Add(StoreActivityType type, int? user, int? refId = null, int days = 0, int store = 1) =>
        _db.StoreActivities.Add(new StoreActivity { StoreId = store, Type = type, UserId = user, RefId = refId, OccurredAt = _nov.AddDays(days) });

    [Fact]
    public async Task PeopleBroughtIn_CountsDistinctPeople_FromTradesAndCheckIns()
    {
        Add(StoreActivityType.MeetupCompleted, 10, refId: 1);
        Add(StoreActivityType.MeetupCompleted, 11, refId: 1);
        Add(StoreActivityType.MeetupCompleted, 10, refId: 2);   // stessa persona, altro scambio
        Add(StoreActivityType.MeetupCompleted, 12, refId: 2);
        Add(StoreActivityType.EventCheckIn, 12);                // già contata
        Add(StoreActivityType.EventCheckIn, 13);
        Add(StoreActivityType.ProfileView, null);
        Add(StoreActivityType.ProfileView, 14);
        Add(StoreActivityType.Follow, 14);
        Add(StoreActivityType.ProfileView, 15, days: 40);        // fuori periodo
        Add(StoreActivityType.ProfileView, 16, store: 2);       // altro negozio
        _db.StoreAttributions.Add(new StoreAttribution { UserId = 20, StoreId = 1, CreatedAt = _nov });
        await _db.SaveChangesAsync();

        var stats = await new StoreStatsService(_db).ComputeAsync(1, _nov.AddDays(-15), _nov.AddDays(15));

        stats.PeopleBroughtIn.Should().Be(4);
        stats.TradesCompletedAtStore.Should().Be(2);
        stats.EventCheckIns.Should().Be(2);
        stats.ProfileViews.Should().Be(2);
        stats.NewFollowers.Should().Be(1);
        stats.QrSignups.Should().Be(1);
    }

    [Fact]
    public void Recorder_CountsVisitsOncePerPersonPerDay()
    {
        var recorder = new StoreActivityRecorder(new MemoryCache(new MemoryCacheOptions()));
        recorder.Record(1, StoreActivityType.ProfileView, 5);
        recorder.Record(1, StoreActivityType.ProfileView, 5);
        recorder.Record(1, StoreActivityType.ProfileView, null, visitorKey: "1.2.3.4");
        recorder.Record(1, StoreActivityType.ProfileView, null, visitorKey: "1.2.3.4");
        recorder.Record(1, StoreActivityType.Follow, 5);
        recorder.Record(1, StoreActivityType.Follow, 5); // non deduplicato: ogni follow conta

        var items = new List<StoreActivity>();
        while (recorder.Reader.TryRead(out var a)) items.Add(a);
        items.Count(i => i.Type == StoreActivityType.ProfileView).Should().Be(2);
        items.Count(i => i.Type == StoreActivityType.Follow).Should().Be(2);
    }
}
