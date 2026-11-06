using CardExchange.API.Services.Entitlements;
using CardExchange.API.Services.Stores;
using CardExchange.API.Services;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
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

            /// <summary>Negozio scelto come punto di scambio: nome, indirizzo e posizione vengono dalla sua scheda.</summary>
            public int? StoreId { get; set; }
        }

        public class MeetupSpotDto
        {
            public int StoreId { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Address { get; set; } = string.Empty;
            public string City { get; set; } = string.Empty;
            public decimal Latitude { get; set; }
            public decimal Longitude { get; set; }
            public string? OpeningHours { get; set; }
            /// <summary>Negozio partner (piano Negozio attivo).</summary>
            public bool IsPartner { get; set; }
            public int? DistanceFromMeKm { get; set; }
            public int? DistanceFromOtherKm { get; set; }
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
            public int? StoreId { get; set; }
        }

        /// <summary>
        /// Negozi da proporre come punto di scambio sicuro: partner per primi, poi per distanza dal punto medio
        /// tra i due partecipanti (entro 30 km). Senza le posizioni di entrambi si usa quella disponibile.
        /// </summary>
        [HttpGet("suggestions")]
        public async Task<ActionResult<IEnumerable<MeetupSpotDto>>> Suggestions(int tradeOfferId)
        {
            var (offer, error) = await LoadOfferForParticipantAsync(tradeOfferId);
            if (offer == null) return error!;
            var userId = CurrentUserId();
            var otherId = userId == offer.SenderId ? offer.ReceiverId : offer.SenderId;

            var locations = await _context.UserLocations.AsNoTracking()
                .Where(l => (l.UserId == userId || l.UserId == otherId) && l.Latitude != null && l.Longitude != null)
                .Select(l => new { l.UserId, Lat = (double)l.Latitude!.Value, Lon = (double)l.Longitude!.Value })
                .ToListAsync();
            var mine = locations.FirstOrDefault(l => l.UserId == userId);
            var other = locations.FirstOrDefault(l => l.UserId == otherId);
            if (mine == null && other == null) return Ok(Array.Empty<MeetupSpotDto>());
            var (midLat, midLon) = mine != null && other != null
                ? MeetupSpots.Midpoint(mine.Lat, mine.Lon, other.Lat, other.Lon)
                : ((mine ?? other)!.Lat, (mine ?? other)!.Lon);

            var stores = await _context.Stores.AsNoTracking()
                .Where(s => s.Status == StoreStatus.Verified && s.IsSafeMeetupSpot)
                .ToListAsync();
            var ownerIds = stores.Select(s => s.OwnerId).Distinct().ToList();
            var partners = await this.Entitlements(_context).HoldersAsync(ownerIds, Capability.FeaturedStore);

            var ranked = MeetupSpots.Rank(
                stores.Select(s => new MeetupSpots.Candidate(s.Id, (double)s.Latitude, (double)s.Longitude, partners.Contains(s.OwnerId))),
                midLat, midLon);
            var byId = stores.ToDictionary(s => s.Id);
            int? Km(double? lat, double? lon, Store s) => lat.HasValue && lon.HasValue
                ? Math.Max(1, (int)Math.Round(Geo.DistanceKm(lat.Value, lon.Value, (double)s.Latitude, (double)s.Longitude)))
                : null;

            return Ok(ranked.Select(r =>
            {
                var s = byId[r.StoreId];
                return new MeetupSpotDto
                {
                    StoreId = s.Id,
                    Name = s.Name,
                    Address = s.Address,
                    City = s.City,
                    Latitude = s.Latitude,
                    Longitude = s.Longitude,
                    OpeningHours = s.OpeningHours,
                    IsPartner = r.IsPartner,
                    DistanceFromMeKm = Km(mine?.Lat, mine?.Lon, s),
                    DistanceFromOtherKm = Km(other?.Lat, other?.Lon, s)
                };
            }));
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
            // Negozio come punto di scambio: luogo e posizione vengono dalla sua scheda
            Store? store = null;
            if (request.StoreId.HasValue)
            {
                store = await _context.Stores.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == request.StoreId.Value && s.Status == StoreStatus.Verified);
                if (store == null) return BadRequest(new { message = "Il negozio scelto non è disponibile" });
                request.LocationName = store.Name;
                request.Address = $"{store.Address}, {store.City}";
                request.Latitude = store.Latitude;
                request.Longitude = store.Longitude;
            }
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
                Notes = request.Notes?.Trim(),
                StoreId = store?.Id
            };
            _context.TradeMeetups.Add(meetup);
            await _context.SaveChangesAsync();

            var me = userId == offer.SenderId ? offer.Sender : offer.Receiver;
            var otherId = userId == offer.SenderId ? offer.ReceiverId : offer.SenderId;
            await NotifyAsync(otherId, offer.Id,
                active.Count > 0 ? "Nuova proposta di appuntamento" : "Proposta di appuntamento",
                $"{me.Username} propone di incontrarvi {FormatWhen(scheduledUtc, TimeZoneOf(offer, otherId))} a {meetup.LocationName}.");

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
                $"{me.Username} ha annullato l'incontro di {FormatWhen(meetup.ScheduledAt, TimeZoneOf(offer, otherId))}.");
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
            if (accept && meetup.StoreId != null)
                this.StoreActivity()?.Record(meetup.StoreId.Value, StoreActivityType.MeetupScheduled, refId: meetup.Id);

            var me = userId == offer.SenderId ? offer.Sender : offer.Receiver;
            var tz = TimeZoneOf(offer, meetup.ProposedById);
            await NotifyAsync(meetup.ProposedById, offer.Id,
                accept ? "Appuntamento confermato" : "Appuntamento rifiutato",
                accept
                    ? $"{me.Username} ha confermato l'incontro {FormatWhen(meetup.ScheduledAt, tz)} a {meetup.LocationName}."
                    : $"{me.Username} non può {FormatWhen(meetup.ScheduledAt, tz)}: proponete un altro orario.");
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

        /// <summary>Data leggibile nel fuso di chi la legge, es. "sabato 8 novembre alle 15:30".</summary>
        public static string FormatWhen(DateTime utc, string? timeZone = null) => TimeZones.FormatWhen(utc, timeZone);

        private static string? TimeZoneOf(TradeOffer offer, int userId) =>
            userId == offer.SenderId ? offer.Sender?.TimeZone : offer.Receiver?.TimeZone;

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
            RespondedAt = m.RespondedAt,
            StoreId = m.StoreId
        };

        private int CurrentUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
    }
}
