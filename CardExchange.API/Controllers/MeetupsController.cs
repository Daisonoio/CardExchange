using CardExchange.API.Services;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Appuntamenti per gli scambi di persona: proposta, risposta, annullamento.
    /// </summary>
    [ApiController]
    [Route("api/tradeoffers/{tradeOfferId:int}/meetups")]
    [Authorize]
    public class MeetupsController : ControllerBase
    {
        private static readonly TimeSpan MinLeadTime = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan MaxHorizon = TimeSpan.FromDays(60);
        private static readonly TimeZoneInfo ItalyTime = ResolveItalyTimeZone();

        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notifications;
        private readonly ILogger<MeetupsController> _logger;

        public MeetupsController(ApplicationDbContext context, INotificationService notifications, ILogger<MeetupsController> logger)
        {
            _context = context;
            _notifications = notifications;
            _logger = logger;
        }

        public class ProposeMeetupRequest
        {
            /// <summary>Data e ora con fuso (ISO 8601); viene salvata in UTC.</summary>
            [Required]
            public DateTimeOffset ScheduledAt { get; set; }

            [Required(ErrorMessage = "Indica il luogo dell'incontro")]
            [MaxLength(200)]
            public string LocationName { get; set; } = string.Empty;

            [MaxLength(300)]
            public string? Address { get; set; }

            [Range(-90, 90)]
            public decimal? Latitude { get; set; }

            [Range(-180, 180)]
            public decimal? Longitude { get; set; }

            [MaxLength(500)]
            public string? Notes { get; set; }
        }

        public class MeetupDto
        {
            public int Id { get; set; }
            public int TradeOfferId { get; set; }
            public int ProposedById { get; set; }
            public string ProposedByUsername { get; set; } = string.Empty;
            public DateTime ScheduledAt { get; set; }
            public string LocationName { get; set; } = string.Empty;
            public string? Address { get; set; }
            public decimal? Latitude { get; set; }
            public decimal? Longitude { get; set; }
            public string? Notes { get; set; }
            public string Status { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime? RespondedAt { get; set; }
        }

        /// <summary>Storico degli appuntamenti dello scambio (più recenti prima).</summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MeetupDto>>> List(int tradeOfferId)
        {
            var (offer, error) = await LoadOfferForParticipantAsync(tradeOfferId);
            if (offer == null) return error!;

            var meetups = await _context.TradeMeetups
                .AsNoTracking()
                .Include(m => m.ProposedBy)
                .Where(m => m.TradeOfferId == tradeOfferId)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
            return Ok(meetups.Select(ToDto));
        }

        /// <summary>Propone un appuntamento (sostituisce quello attivo, se presente). Solo per scambi accettati.</summary>
        [HttpPost]
        public async Task<ActionResult<MeetupDto>> Propose(int tradeOfferId, [FromBody] ProposeMeetupRequest request)
        {
            var (offer, error) = await LoadOfferForParticipantAsync(tradeOfferId);
            if (offer == null) return error!;
            var userId = CurrentUserId();

            if (offer.Status != TradeOfferStatus.Accepted)
                return BadRequest(new { message = "Si può fissare un appuntamento solo per uno scambio accettato" });

            var scheduledUtc = request.ScheduledAt.UtcDateTime;
            var now = DateTime.UtcNow;
            if (scheduledUtc < now + MinLeadTime)
                return BadRequest(new { message = "Scegli un orario ad almeno 30 minuti da adesso" });
            if (scheduledUtc > now + MaxHorizon)
                return BadRequest(new { message = "Scegli una data entro i prossimi 60 giorni" });
            if (string.IsNullOrWhiteSpace(request.LocationName))
                return BadRequest(new { message = "Indica il luogo dell'incontro" });

            var active = await _context.TradeMeetups
                .Where(m => m.TradeOfferId == tradeOfferId && (m.Status == MeetupStatus.Proposed || m.Status == MeetupStatus.Accepted))
                .ToListAsync();
            foreach (var m in active) m.Status = MeetupStatus.Superseded;

            var meetup = new TradeMeetup
            {
                TradeOfferId = tradeOfferId,
                ProposedById = userId,
                ScheduledAt = scheduledUtc,
                LocationName = request.LocationName.Trim(),
                Address = request.Address?.Trim(),
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Notes = request.Notes?.Trim()
            };
            _context.TradeMeetups.Add(meetup);
            await _context.SaveChangesAsync();

            var me = userId == offer.SenderId ? offer.Sender : offer.Receiver;
            var otherId = userId == offer.SenderId ? offer.ReceiverId : offer.SenderId;
            await NotifyAsync(otherId, offer.Id,
                active.Count > 0 ? "Nuova proposta di appuntamento" : "Proposta di appuntamento",
                $"{me.Username} propone di incontrarvi {FormatWhen(scheduledUtc)} a {meetup.LocationName}.");

            meetup.ProposedBy = me;
            return Ok(ToDto(meetup));
        }

        [HttpPost("{meetupId:int}/accept")]
        public Task<ActionResult<MeetupDto>> Accept(int tradeOfferId, int meetupId) => RespondAsync(tradeOfferId, meetupId, accept: true);

        [HttpPost("{meetupId:int}/decline")]
        public Task<ActionResult<MeetupDto>> Decline(int tradeOfferId, int meetupId) => RespondAsync(tradeOfferId, meetupId, accept: false);

        /// <summary>Annulla un appuntamento proposto o confermato (entrambi i partecipanti).</summary>
        [HttpPost("{meetupId:int}/cancel")]
        public async Task<ActionResult<MeetupDto>> Cancel(int tradeOfferId, int meetupId)
        {
            var (offer, error) = await LoadOfferForParticipantAsync(tradeOfferId);
            if (offer == null) return error!;
            var userId = CurrentUserId();

            var meetup = await _context.TradeMeetups.Include(m => m.ProposedBy)
                .FirstOrDefaultAsync(m => m.Id == meetupId && m.TradeOfferId == tradeOfferId);
            if (meetup == null) return NotFound(new { message = "Appuntamento non trovato" });
            if (meetup.Status != MeetupStatus.Proposed && meetup.Status != MeetupStatus.Accepted)
                return BadRequest(new { message = "Questo appuntamento non è più attivo" });

            meetup.Status = MeetupStatus.Cancelled;
            meetup.RespondedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var me = userId == offer.SenderId ? offer.Sender : offer.Receiver;
            var otherId = userId == offer.SenderId ? offer.ReceiverId : offer.SenderId;
            await NotifyAsync(otherId, offer.Id, "Appuntamento annullato",
                $"{me.Username} ha annullato l'incontro di {FormatWhen(meetup.ScheduledAt)}.");
            return Ok(ToDto(meetup));
        }

        private async Task<ActionResult<MeetupDto>> RespondAsync(int tradeOfferId, int meetupId, bool accept)
        {
            var (offer, error) = await LoadOfferForParticipantAsync(tradeOfferId);
            if (offer == null) return error!;
            var userId = CurrentUserId();

            var meetup = await _context.TradeMeetups.Include(m => m.ProposedBy)
                .FirstOrDefaultAsync(m => m.Id == meetupId && m.TradeOfferId == tradeOfferId);
            if (meetup == null) return NotFound(new { message = "Appuntamento non trovato" });
            if (meetup.ProposedById == userId)
                return BadRequest(new { message = "Deve rispondere l'altro partecipante" });
            if (meetup.Status != MeetupStatus.Proposed)
                return BadRequest(new { message = "Questa proposta non è più in attesa di risposta" });
            if (accept && meetup.ScheduledAt < DateTime.UtcNow)
                return BadRequest(new { message = "L'orario proposto è già passato: proponine uno nuovo" });

            meetup.Status = accept ? MeetupStatus.Accepted : MeetupStatus.Declined;
            meetup.RespondedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var me = userId == offer.SenderId ? offer.Sender : offer.Receiver;
            await NotifyAsync(meetup.ProposedById, offer.Id,
                accept ? "Appuntamento confermato" : "Appuntamento rifiutato",
                accept
                    ? $"{me.Username} ha confermato l'incontro {FormatWhen(meetup.ScheduledAt)} a {meetup.LocationName}."
                    : $"{me.Username} non può {FormatWhen(meetup.ScheduledAt)}: proponete un altro orario.");
            return Ok(ToDto(meetup));
        }

        private async Task<(TradeOffer? Offer, ActionResult? Error)> LoadOfferForParticipantAsync(int tradeOfferId)
        {
            var userId = CurrentUserId();
            if (userId == 0) return (null, Unauthorized());

            var offer = await _context.TradeOffers
                .Include(o => o.Sender)
                .Include(o => o.Receiver)
                .FirstOrDefaultAsync(o => o.Id == tradeOfferId);
            if (offer == null) return (null, NotFound(new { message = "Scambio non trovato" }));
            if (offer.SenderId != userId && offer.ReceiverId != userId) return (null, Forbid());
            return (offer, null);
        }

        private async Task NotifyAsync(int userId, int tradeOfferId, string title, string body)
        {
            try
            {
                await _notifications.SendAsync(userId, NotificationType.MeetupUpdate, title, body, tradeOfferId, "TradeOffer");
            }
            catch (Exception ex)
            {
                // L'appuntamento è già salvato: un problema di notifica non deve far fallire la richiesta
                _logger.LogError(ex, "Notifica appuntamento non inviata a {UserId}", userId);
            }
        }

        /// <summary>Data leggibile in ora italiana, es. "sabato 8 novembre alle 15:30".</summary>
        public static string FormatWhen(DateTime utc)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ItalyTime);
            return local.ToString("dddd d MMMM 'alle' HH:mm", new CultureInfo("it-IT"));
        }

        private static TimeZoneInfo ResolveItalyTimeZone()
        {
            foreach (var id in new[] { "Europe/Rome", "W. Europe Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (TimeZoneNotFoundException) { }
            }
            return TimeZoneInfo.Utc;
        }

        private static MeetupDto ToDto(TradeMeetup m) => new()
        {
            Id = m.Id,
            TradeOfferId = m.TradeOfferId,
            ProposedById = m.ProposedById,
            ProposedByUsername = m.ProposedBy?.Username ?? string.Empty,
            ScheduledAt = DateTime.SpecifyKind(m.ScheduledAt, DateTimeKind.Utc),
            LocationName = m.LocationName,
            Address = m.Address,
            Latitude = m.Latitude,
            Longitude = m.Longitude,
            Notes = m.Notes,
            Status = m.Status.ToString(),
            CreatedAt = m.CreatedAt,
            RespondedAt = m.RespondedAt
        };

        private int CurrentUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
    }
}
