using CardExchange.API.Services;
using CardExchange.API.Services.Events;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CardExchange.Tests.Services;

public class EventRegistrationsTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Mock<INotificationService> _notifications = new();
    private readonly DateTime _now = new(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc);
    private EventRegistrations Service => new(_db, _notifications.Object, new Mock<ILogger<EventRegistrations>>().Object);

    public void Dispose() => _db.Dispose();

    private async Task<(Event Ev, List<User> Users)> SetupAsync(int seats, int users, double startInHours = 72)
    {
        var all = Enumerable.Range(0, users + 1).Select(i => new User { Email = $"u{i}@t.it", Username = $"u{i}", PasswordHash = "x" }).ToList();
        _db.Users.AddRange(all);
        await _db.SaveChangesAsync();
        var ev = new Event
        {
            Title = "Torneo", OrganizerId = all[0].Id, Status = EventStatus.Published, StartDate = _now.AddHours(startInHours),
            Address = "Via Roma 1", City = "Milano", Country = "IT", MaxParticipants = seats, EntryFeeEur = 5
        };
        ev.Participants.Add(new EventParticipant { UserId = all[0].Id, Status = ParticipationStatus.Confirmed });
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();
        return (ev, all.Skip(1).ToList());
    }

    private void VerifyNotified(int userId, string fragment, Times times) =>
        _notifications.Verify(n => n.SendAsync(userId, NotificationType.EventUpdate, It.IsAny<string>(),
            It.Is<string>(b => b.Contains(fragment)), It.IsAny<int?>(), "Event"), times);

    [Fact]
    public async Task FullEvent_GoesToWaitlist_InOrder_OrganizerTakesNoSeat()
    {
        var (ev, u) = await SetupAsync(seats: 2, users: 4);
        (await Service.JoinAsync(ev.Id, u[0].Id, _now)).Outcome.Should().Be(JoinOutcome.Registered);
        (await Service.JoinAsync(ev.Id, u[1].Id, _now)).Outcome.Should().Be(JoinOutcome.Registered);
        (await Service.JoinAsync(ev.Id, u[2].Id, _now.AddMinutes(1))).Should().Be(new JoinResult(JoinOutcome.Waitlisted, 1));
        (await Service.JoinAsync(ev.Id, u[3].Id, _now.AddMinutes(2))).Should().Be(new JoinResult(JoinOutcome.Waitlisted, 2));
        var act = () => Service.JoinAsync(ev.Id, u[3].Id, _now);
        await act.Should().ThrowAsync<EventRegistrationException>().WithMessage("*lista d'attesa*");
        ev.ParticipantCount.Should().Be(2);
    }

    [Fact]
    public async Task Leave_OffersSeatToFirst_ThenConfirm()
    {
        var (ev, u) = await SetupAsync(seats: 1, users: 3);
        await Service.JoinAsync(ev.Id, u[0].Id, _now);
        await Service.JoinAsync(ev.Id, u[1].Id, _now.AddMinutes(1));
        await Service.JoinAsync(ev.Id, u[2].Id, _now.AddMinutes(2));

        await Service.LeaveAsync(ev.Id, u[0].Id, _now);

        var offered = ev.Participants.Single(p => p.UserId == u[1].Id);
        offered.Status.Should().Be(ParticipationStatus.Offered);
        offered.OfferExpiresAt.Should().Be(_now + EventRegistrations.OfferWindow);
        EventRegistrations.WaitlistPosition(ev, u[2].Id).Should().Be(1);
        VerifyNotified(u[1].Id, "c'è un posto per te", Times.Once());

        await Service.ConfirmOfferAsync(ev.Id, u[1].Id, _now.AddHours(1));
        offered.Status.Should().Be(ParticipationStatus.Registered);
    }

    [Fact]
    public async Task ExpiredOffer_PassesToNext_AndCloseToStartPromotesDirectly()
    {
        var (ev, u) = await SetupAsync(seats: 1, users: 3, startInHours: 30);
        await Service.JoinAsync(ev.Id, u[0].Id, _now);
        await Service.JoinAsync(ev.Id, u[1].Id, _now.AddMinutes(1));
        await Service.JoinAsync(ev.Id, u[2].Id, _now.AddMinutes(2));
        await Service.LeaveAsync(ev.Id, u[0].Id, _now);

        // 13 ore dopo: l'offerta a u1 è scaduta; mancano 17 ore → u2 riceve un'offerta normale
        var later = _now.AddHours(13);
        (await Service.RunAsync(later)).Expired.Should().Be(1);
        ev.Participants.Single(p => p.UserId == u[1].Id).Status.Should().Be(ParticipationStatus.Cancelled);
        ev.Participants.Single(p => p.UserId == u[2].Id).Status.Should().Be(ParticipationStatus.Offered);

        // Altre 13 ore: mancano 4 ore, nessuno in lista: il posto resta libero
        (await Service.RunAsync(later.AddHours(13))).Expired.Should().Be(1);
        EventRegistrations.SeatsTaken(ev).Should().Be(0);
    }

    [Fact]
    public async Task CloseToStart_FirstInLineIsRegisteredDirectly()
    {
        var (ev, u) = await SetupAsync(seats: 1, users: 2, startInHours: 6);
        await Service.JoinAsync(ev.Id, u[0].Id, _now);
        await Service.JoinAsync(ev.Id, u[1].Id, _now);
        await Service.LeaveAsync(ev.Id, u[0].Id, _now);
        ev.Participants.Single(p => p.UserId == u[1].Id).Status.Should().Be(ParticipationStatus.Registered);
        VerifyNotified(u[1].Id, "Sei dentro", Times.Once());
    }

    [Fact]
    public async Task DeadlinePassed_RejectsJoin()
    {
        var (ev, u) = await SetupAsync(seats: 5, users: 1);
        ev.RegistrationDeadline = _now.AddHours(-1);
        await _db.SaveChangesAsync();
        var act = () => Service.JoinAsync(ev.Id, u[0].Id, _now);
        await act.Should().ThrowAsync<EventRegistrationException>().WithMessage("Le iscrizioni sono chiuse");
    }

    [Fact]
    public async Task Reminders_OnceDayBefore_ForRegisteredAndWaitlist_PlusOrganizerSummary()
    {
        var (ev, u) = await SetupAsync(seats: 1, users: 2, startInHours: 20);
        await Service.JoinAsync(ev.Id, u[0].Id, _now);
        await Service.JoinAsync(ev.Id, u[1].Id, _now);

        (await Service.RunAsync(_now)).Reminders.Should().Be(2);
        (await Service.RunAsync(_now.AddMinutes(15))).Reminders.Should().Be(0);

        VerifyNotified(u[0].Id, "Quota: 5 €, da pagare sul posto", Times.Once());
        VerifyNotified(u[1].Id, "lista d'attesa", Times.Once());
        VerifyNotified(ev.OrganizerId, "1 iscritti su 1 posti, 1 in lista d'attesa", Times.Once());
    }
}
