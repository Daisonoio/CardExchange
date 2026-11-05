using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Events
{
    public enum JoinOutcome { Registered, Waitlisted }

    public record JoinResult(JoinOutcome Outcome, int? WaitlistPosition);

    public class EventRegistrationException : Exception
    {
        public EventRegistrationException(string message) : base(message) { }
    }

    /// <summary>
    /// Iscrizioni agli eventi con posti limitati: lista d'attesa, posto liberato da confermare entro 12 ore,
    /// promemoria il giorno prima e riepilogo per l'organizzatore. La quota si paga di persona all'evento.
    /// </summary>
    public class EventRegistrations
    {
        /// <summary>Tempo per confermare un posto liberato.</summary>
        public static readonly TimeSpan OfferWindow = TimeSpan.FromHours(12);
        /// <summary>Se mancano meno di così all'inizio, il primo in lista entra subito senza dover confermare.</summary>
        public static readonly TimeSpan DirectPromotionBefore = TimeSpan.FromHours(12);
        public static readonly TimeSpan ReminderBefore = TimeSpan.FromHours(24);

        /// <summary>Stati che occupano un posto.</summary>
        public static readonly ParticipationStatus[] SeatStatuses =
            { ParticipationStatus.Registered, ParticipationStatus.Confirmed, ParticipationStatus.Attended, ParticipationStatus.Offered };

        public static bool HoldsSeat(ParticipationStatus s) => SeatStatuses.Contains(s);

        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;
        private readonly ILogger<EventRegistrations> _logger;

        public EventRegistrations(ApplicationDbContext db, INotificationService notifications, ILogger<EventRegistrations> logger)
        {
            _db = db;
            _notifications = notifications;
            _logger = logger;
        }

        /// <summary>Posti occupati (l'organizzatore non occupa un posto).</summary>
        public static int SeatsTaken(Event e) => e.Participants.Count(p => HoldsSeat(p.Status) && p.UserId != e.OrganizerId);

        /// <summary>Posizione in lista d'attesa (1 = il prossimo), null se non in lista.</summary>
        public static int? WaitlistPosition(Event e, int userId)
        {
            var list = Waitlist(e).Select(p => p.UserId).ToList();
            var i = list.IndexOf(userId);
            return i < 0 ? null : i + 1;
        }

        public static List<EventParticipant> Waitlist(Event e) => e.Participants
            .Where(p => p.Status == ParticipationStatus.Waitlisted)
            .OrderBy(p => p.WaitlistedAt ?? p.CreatedAt).ThenBy(p => p.Id)
            .ToList();

        public static bool RegistrationOpen(Event e, DateTime nowUtc) =>
            e.Status == EventStatus.Published && e.StartDate > nowUtc && (e.RegistrationDeadline == null || e.RegistrationDeadline > nowUtc);

        public async Task<JoinResult> JoinAsync(int eventId, int userId, DateTime nowUtc)
        {
            var ev = await LoadAsync(eventId) ?? throw new EventRegistrationException("Evento non trovato");
            if (ev.Status != EventStatus.Published) throw new EventRegistrationException("L'evento non è attivo");
            if (ev.StartDate <= nowUtc) throw new EventRegistrationException("L'evento è già iniziato o terminato");
            if (ev.RegistrationDeadline.HasValue && ev.RegistrationDeadline <= nowUtc)
                throw new EventRegistrationException("Le iscrizioni sono chiuse");

            var existing = ev.Participants.FirstOrDefault(p => p.UserId == userId);
            if (existing != null && existing.Status != ParticipationStatus.Cancelled)
                throw new EventRegistrationException(existing.Status == ParticipationStatus.Waitlisted
                    ? "Sei già in lista d'attesa" : "Sei già iscritto a questo evento");

            var full = ev.MaxParticipants.HasValue && SeatsTaken(ev) >= ev.MaxParticipants.Value;
            var participant = existing ?? new EventParticipant { EventId = ev.Id, UserId = userId };
            if (existing == null) ev.Participants.Add(participant);
            participant.IsDeleted = false;
            participant.OfferExpiresAt = null;
            participant.ReminderSentAt = null;
            participant.CheckedInAt = null;
            if (full)
            {
                participant.Status = ParticipationStatus.Waitlisted;
                participant.WaitlistedAt = nowUtc;
            }
            else
            {
                participant.Status = ParticipationStatus.Registered;
                participant.WaitlistedAt = null;
            }
            ev.ParticipantCount = SeatsTaken(ev);
            await _db.SaveChangesAsync();
            return full
                ? new JoinResult(JoinOutcome.Waitlisted, WaitlistPosition(ev, userId))
                : new JoinResult(JoinOutcome.Registered, null);
        }

        /// <summary>Ritiro (anche dalla lista d'attesa o rinuncia a un posto offerto): libera il posto per il primo in lista.</summary>
        public async Task LeaveAsync(int eventId, int userId, DateTime nowUtc)
        {
            var ev = await LoadAsync(eventId) ?? throw new EventRegistrationException("Evento non trovato");
            var p = ev.Participants.FirstOrDefault(x => x.UserId == userId && x.Status != ParticipationStatus.Cancelled)
                    ?? throw new EventRegistrationException("Non sei iscritto a questo evento");
            p.Status = ParticipationStatus.Cancelled;
            p.OfferExpiresAt = null;
            await PromoteAsync(ev, nowUtc);
            ev.ParticipantCount = SeatsTaken(ev);
            await _db.SaveChangesAsync();
        }

        /// <summary>Conferma il posto liberato.</summary>
        public async Task ConfirmOfferAsync(int eventId, int userId, DateTime nowUtc)
        {
            var ev = await LoadAsync(eventId) ?? throw new EventRegistrationException("Evento non trovato");
            var p = ev.Participants.FirstOrDefault(x => x.UserId == userId && x.Status == ParticipationStatus.Offered)
                    ?? throw new EventRegistrationException("Non hai un posto da confermare");
            if (p.OfferExpiresAt.HasValue && p.OfferExpiresAt < nowUtc)
                throw new EventRegistrationException("Il tempo per confermare è scaduto");
            p.Status = ParticipationStatus.Registered;
            p.OfferExpiresAt = null;
            await _db.SaveChangesAsync();
        }

        /// <summary>Dà i posti liberi ai primi in lista d'attesa (anche dopo un aumento dei posti).</summary>
        public async Task<int> PromoteAsync(Event ev, DateTime nowUtc)
        {
            if (ev.Status != EventStatus.Published || ev.StartDate <= nowUtc) return 0;
            var promoted = 0;
            foreach (var next in Waitlist(ev))
            {
                if (ev.MaxParticipants.HasValue && SeatsTaken(ev) >= ev.MaxParticipants.Value) break;
                var direct = ev.StartDate - nowUtc <= DirectPromotionBefore;
                next.Status = direct ? ParticipationStatus.Registered : ParticipationStatus.Offered;
                next.OfferExpiresAt = direct ? null : nowUtc + OfferWindow;
                promoted++;
                var tz = next.User?.TimeZone;
                await NotifyAsync(next.UserId, ev, "Si è liberato un posto!", direct
                    ? $"Sei dentro: ora sei iscritto a \"{ev.Title}\" ({TimeZones.FormatWhen(ev.StartDate, tz)})."
                    : $"Per \"{ev.Title}\" c'è un posto per te: confermalo entro {TimeZones.FormatWhen(next.OfferExpiresAt!.Value, tz)}, poi passa al prossimo in lista.");
            }
            return promoted;
        }

        /// <summary>
        /// Lavoro periodico: posti offerti e non confermati tornano disponibili, promemoria del giorno prima
        /// a iscritti e lista d'attesa, riepilogo all'organizzatore.
        /// </summary>
        public async Task<(int Expired, int Reminders)> RunAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var expiredEvents = await _db.Events
                .Include(e => e.Participants).ThenInclude(p => p.User)
                .Where(e => e.Status == EventStatus.Published && e.StartDate > nowUtc
                            && e.Participants.Any(p => p.Status == ParticipationStatus.Offered && p.OfferExpiresAt < nowUtc))
                .ToListAsync(ct);
            var expired = 0;
            foreach (var ev in expiredEvents)
            {
                foreach (var p in ev.Participants.Where(p => p.Status == ParticipationStatus.Offered && p.OfferExpiresAt < nowUtc))
                {
                    p.Status = ParticipationStatus.Cancelled;
                    p.OfferExpiresAt = null;
                    expired++;
                    await NotifyAsync(p.UserId, ev, "Posto non confermato",
                        $"Il posto per \"{ev.Title}\" è passato al prossimo in lista perché non è stato confermato in tempo.");
                }
                await PromoteAsync(ev, nowUtc);
                ev.ParticipantCount = SeatsTaken(ev);
            }
            if (expired > 0) await _db.SaveChangesAsync(ct);

            var soon = nowUtc + ReminderBefore;
            var upcoming = await _db.Events
                .Include(e => e.Participants).ThenInclude(p => p.User)
                .Include(e => e.Organizer)
                .Where(e => e.Status == EventStatus.Published && e.StartDate > nowUtc && e.StartDate <= soon
                            && (e.OrganizerSummarySentAt == null
                                || e.Participants.Any(p => p.ReminderSentAt == null && p.UserId != e.OrganizerId
                                                           && (p.Status == ParticipationStatus.Registered || p.Status == ParticipationStatus.Confirmed
                                                               || p.Status == ParticipationStatus.Offered || p.Status == ParticipationStatus.Waitlisted))))
                .ToListAsync(ct);
            var reminders = 0;
            foreach (var ev in upcoming)
            {
                foreach (var p in ev.Participants.Where(p => p.ReminderSentAt == null && p.UserId != ev.OrganizerId))
                {
                    var tz = p.User?.TimeZone;
                    var when = TimeZones.FormatWhen(ev.StartDate, tz);
                    string? body = p.Status switch
                    {
                        ParticipationStatus.Registered or ParticipationStatus.Confirmed =>
                            $"\"{ev.Title}\" è {when}, {ev.Address}, {ev.City}." + (ev.EntryFeeEur is > 0 ? $" Quota: {ev.EntryFeeEur:0.##} €, da pagare sul posto." : ""),
                        ParticipationStatus.Offered => $"Hai un posto da confermare per \"{ev.Title}\" ({when}).",
                        ParticipationStatus.Waitlisted => $"Sei in lista d'attesa per \"{ev.Title}\" ({when}), posizione {WaitlistPosition(ev, p.UserId)}. Ti avvisiamo se si libera un posto.",
                        _ => null
                    };
                    if (body == null) continue;
                    p.ReminderSentAt = nowUtc;
                    reminders++;
                    await NotifyAsync(p.UserId, ev, "Domani: " + ev.Title, body);
                }
                if (ev.OrganizerSummarySentAt == null)
                {
                    ev.OrganizerSummarySentAt = nowUtc;
                    var seats = ev.Participants.Count(p => HoldsSeat(p.Status) && p.UserId != ev.OrganizerId);
                    var waiting = ev.Participants.Count(p => p.Status == ParticipationStatus.Waitlisted);
                    await NotifyAsync(ev.OrganizerId, ev, "Riepilogo iscritti: " + ev.Title,
                        $"{seats} iscritti" + (ev.MaxParticipants.HasValue ? $" su {ev.MaxParticipants} posti" : "")
                        + (waiting > 0 ? $", {waiting} in lista d'attesa" : "") + $". Inizio {TimeZones.FormatWhen(ev.StartDate, ev.Organizer?.TimeZone)}.");
                }
            }
            if (upcoming.Count > 0) await _db.SaveChangesAsync(ct);
            return (expired, reminders);
        }

        private Task<Event?> LoadAsync(int eventId) =>
            _db.Events.Include(e => e.Participants).ThenInclude(p => p.User).FirstOrDefaultAsync(e => e.Id == eventId);

        private async Task NotifyAsync(int userId, Event ev, string title, string body)
        {
            try
            {
                await _notifications.SendAsync(userId, NotificationType.EventUpdate, title, body, ev.Id, "Event");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notifica evento {EventId} non inviata a {UserId}", ev.Id, userId);
            }
        }
    }
}
