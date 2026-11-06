using CardExchange.API.Services.Stores;
using CardExchange.API.Services.Events;
using CardExchange.API.Authorization;
using CardExchange.API.DTOs.Requests;
using CardExchange.API.DTOs.Responses;
using CardExchange.API.Services;
using CardExchange.Core.Entities;
using CardExchange.Core.Interfaces;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Gestione eventi locali di scambio carte
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EventsController : ControllerBase
    {
        private readonly IEventRepository _eventRepository;
        private readonly IUserRepository _userRepository;
        private readonly INotificationService _notificationService;
        private readonly ApplicationDbContext _context;
        private readonly IMatchmakingService _matchmaking;
        private readonly CardExchange.API.Services.Moderation.IUserBlockService _blocks;
        private readonly ILogger<EventsController> _logger;

        public EventsController(
            IEventRepository eventRepository,
            IUserRepository userRepository,
            INotificationService notificationService,
            ApplicationDbContext context,
            IMatchmakingService matchmaking,
            CardExchange.API.Services.Moderation.IUserBlockService blocks,
            ILogger<EventsController> logger)
        {
            _eventRepository = eventRepository;
            _userRepository = userRepository;
            _notificationService = notificationService;
            _context = context;
            _matchmaking = matchmaking;
            _blocks = blocks;
            _logger = logger;
        }

        /// <summary>
        /// Ottiene gli eventi futuri pubblicati (paginato)
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> GetUpcomingEvents([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                pageSize = Math.Clamp(pageSize, 1, 50);
                var (events, totalCount) = await _eventRepository.GetUpcomingEventsAsync(page, pageSize);
                var currentUserId = GetCurrentUserId();

                // Prendi le partecipazioni dell'utente corrente
                var userEventIds = new HashSet<int>();
                if (currentUserId > 0)
                {
                    var userEvents = await _eventRepository.GetUserEventsAsync(currentUserId);
                    userEventIds = userEvents.Select(e => e.Id).ToHashSet();
                }

                var dtos = events.Select(e => MapToDto(e, userEventIds.Contains(e.Id)));

                return Ok(new
                {
                    items = dtos,
                    totalCount,
                    page,
                    pageSize,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero degli eventi");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Cerca eventi per città
        /// </summary>
        [HttpGet("city/{city}")]
        [AllowAnonymous]
        public async Task<ActionResult> GetEventsByCity(string city, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                pageSize = Math.Clamp(pageSize, 1, 50);
                var (events, totalCount) = await _eventRepository.GetEventsByCityAsync(city, page, pageSize);
                var dtos = events.Select(e => MapToDto(e, false));

                return Ok(new
                {
                    items = dtos,
                    totalCount,
                    page,
                    pageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la ricerca eventi per città: {City}", city);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Cerca eventi nelle vicinanze (geo-search)
        /// </summary>
        [HttpGet("nearby")]
        [RequirePermission("SEARCH.GEOGRAPHIC")]
        public async Task<ActionResult> GetNearbyEvents(
            [FromQuery] decimal? latitude,
            [FromQuery] decimal? longitude,
            [FromQuery] int radiusKm = 50,
            [FromQuery] int limit = 20)
        {
            try
            {
                radiusKm = Math.Clamp(radiusKm, 1, 500);
                limit = Math.Clamp(limit, 1, 50);
                var currentUserId = GetCurrentUserId();

                // Senza coordinate si usa la posizione salvata nel profilo
                if (latitude == null || longitude == null)
                {
                    var loc = await _context.UserLocations.AsNoTracking().FirstOrDefaultAsync(l => l.UserId == currentUserId);
                    if (loc?.Latitude == null || loc.Longitude == null)
                        return BadRequest(new { message = "Imposta la tua posizione nel profilo per vedere gli eventi vicini" });
                    latitude = loc.Latitude;
                    longitude = loc.Longitude;
                }

                var events = await _eventRepository.GetNearbyEventsAsync(latitude.Value, longitude.Value, radiusKm, limit);
                var mine = currentUserId > 0
                    ? (await _eventRepository.GetUserEventsAsync(currentUserId)).Select(e => e.Id).ToHashSet()
                    : new HashSet<int>();
                var dtos = events.Select(e => MapToDto(e, mine.Contains(e.Id)));

                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la ricerca eventi nelle vicinanze");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Eventi futuri pubblicati di un negozio.
        /// </summary>
        [HttpGet("store/{storeId:int}")]
        [AllowAnonymous]
        public async Task<ActionResult> GetStoreEvents(int storeId)
        {
            var now = DateTime.UtcNow;
            var events = await _context.Events
                .AsNoTracking()
                .Include(e => e.Organizer)
                .Include(e => e.Store)
                .Where(e => e.StoreId == storeId && e.Status == EventStatus.Published && e.IsPublic
                            && (e.EndDate ?? e.StartDate.AddHours(6)) >= now)
                .OrderBy(e => e.StartDate)
                .Take(20)
                .ToListAsync();
            var currentUserId = GetCurrentUserId();
            var mine = currentUserId > 0
                ? (await _eventRepository.GetUserEventsAsync(currentUserId)).Select(e => e.Id).ToHashSet()
                : new HashSet<int>();
            return Ok(events.Select(e => MapToDto(e, mine.Contains(e.Id))));
        }

        // ================================================================
        // Serata di scambio: check-in e match tra i presenti
        // ================================================================

        /// <summary>Il check-in è aperto da 30 minuti prima dell'inizio fino alla fine (o 6 ore dopo l'inizio).</summary>
        public static (DateTime Opens, DateTime Closes) CheckInWindow(Event e) =>
            (e.StartDate.AddMinutes(-30), e.EndDate ?? e.StartDate.AddHours(6));

        /// <summary>Segna l'utente come presente. Se non era iscritto viene iscritto (se ci sono posti).</summary>
        [HttpPost("{id}/checkin")]
        public async Task<IActionResult> CheckIn(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var ev = await _context.Events.Include(e => e.Participants).FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null) return NotFound(new { message = "Evento non trovato" });
            if (ev.Status != EventStatus.Published) return BadRequest(new { message = "L'evento non è attivo" });

            var (opens, closes) = CheckInWindow(ev);
            var now = DateTime.UtcNow;
            if (now < opens) return BadRequest(new { message = "Il check-in apre 30 minuti prima dell'inizio" });
            if (now > closes) return BadRequest(new { message = "L'evento è terminato" });

            var participant = ev.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant == null || !EventRegistrations.HoldsSeat(participant.Status))
            {
                // Senza posto (non iscritto o in lista d'attesa): entra solo se ci sono posti liberi
                if (ev.MaxParticipants.HasValue && EventRegistrations.SeatsTaken(ev) >= ev.MaxParticipants.Value)
                    return BadRequest(new { message = participant?.Status == ParticipationStatus.Waitlisted
                        ? "Sei in lista d'attesa: l'evento è ancora pieno" : "Posti esauriti" });
                if (participant == null)
                {
                    participant = new EventParticipant { EventId = id, UserId = userId };
                    ev.Participants.Add(participant);
                }
            }

            participant.Status = ParticipationStatus.Attended;
            participant.CheckedInAt = now;
            participant.OfferExpiresAt = null;
            ev.ParticipantCount = EventRegistrations.SeatsTaken(ev);
            await _context.SaveChangesAsync();
            if (ev.StoreId != null)
            {
                this.StoreActivity()?.Record(ev.StoreId.Value, StoreActivityType.EventCheckIn, participant.UserId, ev.Id);
                await StampLoyaltyAsync(ev, participant.UserId);
            }
            return Ok(new { message = "Sei presente! Ora vedi chi c'è e con chi puoi scambiare." });
        }

        /// <summary>Timbro della tessera fedeltà al check-in di un evento del negozio (uno al giorno, se la tessera è attiva).</summary>
        private async Task StampLoyaltyAsync(Event ev, int userId)
        {
            var loyalty = HttpContext?.RequestServices.GetService<CardExchange.API.Services.Stores.LoyaltyService>();
            if (loyalty == null) return;
            try { await loyalty.StampForEventAsync(ev, userId, DateTime.UtcNow); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Timbro fedeltà non assegnato per l'evento {EventId}", ev.Id); }
        }

        /// <summary>Esce dalla serata: non si è più visibili agli altri presenti.</summary>
        [HttpPost("{id}/checkout")]
        public async Task<IActionResult> CheckOut(int id)
        {
            var userId = GetCurrentUserId();
            var participant = await _context.EventParticipants.FirstOrDefaultAsync(p => p.EventId == id && p.UserId == userId);
            if (participant?.CheckedInAt != null)
            {
                participant.CheckedInAt = null;
                await _context.SaveChangesAsync();
            }
            return Ok(new { message = "Sei uscito dalla serata" });
        }

        /// <summary>
        /// Vista live della serata: presenti e match con ciascuno. Visibile solo a chi ha fatto check-in
        /// (chi non è presente vede solo il numero dei presenti).
        /// </summary>
        [HttpGet("{id}/live")]
        public async Task<IActionResult> Live(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var ev = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null) return NotFound(new { message = "Evento non trovato" });

            var (opens, closes) = CheckInWindow(ev);
            var now = DateTime.UtcNow;
            var isOpen = ev.Status == EventStatus.Published && now >= opens && now <= closes;

            var blocked = await _blocks.GetBlockedUserIdsAsync(userId);
            var present = await _context.EventParticipants
                .AsNoTracking()
                .Where(p => p.EventId == id && p.CheckedInAt != null && p.Status != ParticipationStatus.Cancelled)
                .Select(p => new { p.UserId, p.User.Username, p.User.ReputationScore, p.CheckedInAt })
                .ToListAsync();
            var meCheckedIn = present.Any(p => p.UserId == userId);
            var others = present.Where(p => p.UserId != userId && !blocked.Contains(p.UserId)).ToList();

            if (!meCheckedIn || !isOpen)
                return Ok(new { isOpen, checkedIn = meCheckedIn, opensAt = opens, closesAt = closes, presentCount = present.Count, attendees = Array.Empty<object>() });

            var matches = (await _matchmaking.FindMatchesAsync(userId, onlyUserIds: others.Select(o => o.UserId).ToList()))
                .ToDictionary(m => m.UserId);

            var attendees = others
                .Select(o =>
                {
                    matches.TryGetValue(o.UserId, out var m);
                    return new
                    {
                        o.UserId,
                        o.Username,
                        o.ReputationScore,
                        o.CheckedInAt,
                        isMutual = m?.IsMutual ?? false,
                        score = m?.Score ?? 0,
                        theyHave = m?.TheyHaveIWant.Select(c => new { c.CardId, c.Name, c.Condition, c.Language, c.Finish, c.PriceEur, c.AskingPrice }).Take(5) ?? Enumerable.Empty<object>(),
                        theyHaveCount = m?.TheyHaveIWant.Count ?? 0,
                        iHave = m?.IHaveTheyWant.Select(c => new { c.CardId, c.Name, c.Condition, c.Language, c.Finish, c.PriceEur, c.AskingPrice }).Take(5) ?? Enumerable.Empty<object>(),
                        iHaveCount = m?.IHaveTheyWant.Count ?? 0
                    };
                })
                .OrderByDescending(a => a.isMutual)
                .ThenByDescending(a => a.score)
                .ThenBy(a => a.Username)
                .ToList();

            return Ok(new { isOpen, checkedIn = true, opensAt = opens, closesAt = closes, presentCount = present.Count, attendees });
        }

        /// <summary>
        /// Ottiene il dettaglio di un evento con la lista dei partecipanti
        /// </summary>
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<EventDetailDto>> GetEvent(int id)
        {
            try
            {
                var eventEntity = await _eventRepository.GetEventWithParticipantsAsync(id);
                if (eventEntity == null)
                    return NotFound(new { message = "Evento non trovato" });

                var currentUserId = GetCurrentUserId();
                var blocked = await _blocks.GetBlockedUserIdsAsync(currentUserId);
                var mine = eventEntity.Participants.FirstOrDefault(p => p.UserId == currentUserId && p.Status != ParticipationStatus.Cancelled);
                var isParticipating = mine != null && EventRegistrations.HoldsSeat(mine.Status);
                var seats = EventRegistrations.SeatsTaken(eventEntity);

                var dto = new EventDetailDto
                {
                    Id = eventEntity.Id,
                    Title = eventEntity.Title,
                    Description = eventEntity.Description,
                    Type = eventEntity.Type.ToString(),
                    Status = eventEntity.Status.ToString(),
                    StartDate = eventEntity.StartDate,
                    EndDate = eventEntity.EndDate,
                    Address = eventEntity.Address,
                    City = eventEntity.City,
                    Province = eventEntity.Province,
                    Country = eventEntity.Country,
                    Latitude = eventEntity.Latitude,
                    Longitude = eventEntity.Longitude,
                    MaxParticipants = eventEntity.MaxParticipants,
                    IsPublic = eventEntity.IsPublic,
                    ImageUrl = eventEntity.ImageUrl,
                    SupportedGameIds = eventEntity.SupportedGameIds,
                    ParticipantCount = seats,
                    EntryFeeEur = eventEntity.EntryFeeEur,
                    RegistrationDeadline = eventEntity.RegistrationDeadline,
                    Format = eventEntity.Format,
                    GameId = eventEntity.GameId,
                    RegistrationOpen = EventRegistrations.RegistrationOpen(eventEntity, DateTime.UtcNow),
                    SpotsLeft = eventEntity.MaxParticipants.HasValue ? Math.Max(0, eventEntity.MaxParticipants.Value - seats) : null,
                    WaitlistCount = eventEntity.Participants.Count(p => p.Status == ParticipationStatus.Waitlisted),
                    MyStatus = mine?.Status.ToString(),
                    MyWaitlistPosition = mine?.Status == ParticipationStatus.Waitlisted ? EventRegistrations.WaitlistPosition(eventEntity, currentUserId) : null,
                    MyOfferExpiresAt = mine?.Status == ParticipationStatus.Offered ? mine.OfferExpiresAt : null,
                    StoreId = eventEntity.StoreId,
                    StoreName = eventEntity.Store?.Name,
                    CreatedAt = eventEntity.CreatedAt,
                    IsParticipating = isParticipating,
                    Organizer = new OrganizerDto
                    {
                        Id = eventEntity.Organizer.Id,
                        Username = eventEntity.Organizer.Username,
                        AvatarUrl = eventEntity.Organizer.AvatarUrl,
                        ReputationScore = eventEntity.Organizer.ReputationScore
                    },
                    // Gli utenti bloccati (in entrambe le direzioni) non compaiono nell'elenco
                    Participants = eventEntity.Participants
                        .Where(p => EventRegistrations.HoldsSeat(p.Status) && !blocked.Contains(p.UserId))
                        .Select(p => new EventParticipantDto
                        {
                            UserId = p.UserId,
                            Username = p.User?.Username ?? "N/A",
                            AvatarUrl = p.User?.AvatarUrl,
                            Status = p.Status.ToString(),
                            JoinedAt = p.CreatedAt
                        })
                };

                var content = HttpContext.RequestServices.GetService<CardExchange.API.Services.Billing.SponsoredContent>();
                if (content != null)
                    dto.Sponsors = (await content.ForEventAsync(eventEntity, DateTime.UtcNow))
                        .Select(x => (object)new { id = x.SponsorId, name = x.Name, logoUrl = x.LogoUrl, placement = x.Placement.ToString() }).ToList();
                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero dell'evento {EventId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Crea un nuovo evento di scambio
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<EventDto>> CreateEvent([FromBody] CreateEventRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized();

                if (request.StartDate <= DateTime.UtcNow)
                    return BadRequest(new { message = "La data di inizio deve essere nel futuro" });

                if (request.EndDate.HasValue && request.EndDate <= request.StartDate)
                    return BadRequest(new { message = "La data di fine deve essere successiva alla data di inizio" });

                if (request.RegistrationDeadline.HasValue && request.RegistrationDeadline > request.StartDate)
                    return BadRequest(new { message = "La chiusura delle iscrizioni deve precedere l'inizio" });

                // Evento ospitato da un negozio: solo il proprietario, solo se verificato; il luogo è quello del negozio
                Store? store = null;
                if (request.StoreId.HasValue)
                {
                    store = await _context.Stores.FirstOrDefaultAsync(s => s.Id == request.StoreId.Value);
                    if (store == null || store.OwnerId != userId)
                        return BadRequest(new { message = "Puoi organizzare eventi solo nei negozi che gestisci" });
                    if (store.Status != StoreStatus.Verified)
                        return BadRequest(new { message = "Il negozio deve essere verificato prima di pubblicare eventi" });
                    request.Address = store.Address;
                    request.City = store.City;
                    request.Province = store.Province;
                    request.PostalCode = store.PostalCode;
                    request.Country = store.Country;
                    request.Latitude = store.Latitude;
                    request.Longitude = store.Longitude;
                }
                else if (request.Latitude == null || request.Longitude == null)
                {
                    return BadRequest(new { message = "Indica il luogo dell'evento sulla mappa" });
                }

                var eventEntity = new Event
                {
                    OrganizerId = userId,
                    Title = request.Title,
                    Description = request.Description,
                    Type = (EventType)request.Type,
                    Status = EventStatus.Published,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    Address = request.Address,
                    City = request.City,
                    Province = request.Province,
                    Country = request.Country,
                    PostalCode = request.PostalCode,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    MaxParticipants = request.MaxParticipants,
                    IsPublic = request.IsPublic,
                    ImageUrl = request.ImageUrl,
                    SupportedGameIds = request.SupportedGameIds,
                    StoreId = store?.Id,
                    EntryFeeEur = request.EntryFeeEur,
                    RegistrationDeadline = request.RegistrationDeadline,
                    Format = string.IsNullOrWhiteSpace(request.Format) ? null : request.Format.Trim(),
                    GameId = request.GameId
                };

                // Aggiungi organizzatore come primo partecipante
                var participant = new EventParticipant
                {
                    UserId = userId,
                    Status = ParticipationStatus.Confirmed
                };
                eventEntity.Participants.Add(participant);
                eventEntity.ParticipantCount = 0;

                await _eventRepository.AddAsync(eventEntity);
                await _eventRepository.SaveChangesAsync();

                _logger.LogInformation("Evento {EventId} creato dall'utente {UserId}", eventEntity.Id, userId);

                return CreatedAtAction(nameof(GetEvent), new { id = eventEntity.Id }, MapToDto(eventEntity, true));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la creazione dell'evento");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Aggiorna un evento (solo l'organizzatore)
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateEvent(int id, [FromBody] UpdateEventRequest request,
            [FromServices] EventRegistrations registrations)
        {
            try
            {
                var userId = GetCurrentUserId();
                var eventEntity = await _context.Events.Include(e => e.Participants).ThenInclude(p => p.User)
                    .FirstOrDefaultAsync(e => e.Id == id);

                if (eventEntity == null)
                    return NotFound(new { message = "Evento non trovato" });

                if (eventEntity.OrganizerId != userId)
                    return Forbid();

                if (request.Title != null) eventEntity.Title = request.Title;
                if (request.Description != null) eventEntity.Description = request.Description;
                if (request.Type.HasValue) eventEntity.Type = (EventType)request.Type.Value;
                if (request.StartDate.HasValue) eventEntity.StartDate = request.StartDate.Value;
                if (request.EndDate.HasValue) eventEntity.EndDate = request.EndDate.Value;
                if (request.Address != null) eventEntity.Address = request.Address;
                if (request.City != null) eventEntity.City = request.City;
                if (request.Province != null) eventEntity.Province = request.Province;
                if (request.Country != null) eventEntity.Country = request.Country;
                if (request.PostalCode != null) eventEntity.PostalCode = request.PostalCode;
                if (request.Latitude.HasValue) eventEntity.Latitude = request.Latitude;
                if (request.Longitude.HasValue) eventEntity.Longitude = request.Longitude;
                if (request.MaxParticipants.HasValue) eventEntity.MaxParticipants = request.MaxParticipants;
                if (request.IsPublic.HasValue) eventEntity.IsPublic = request.IsPublic.Value;
                if (request.ImageUrl != null) eventEntity.ImageUrl = request.ImageUrl;
                if (request.SupportedGameIds != null) eventEntity.SupportedGameIds = request.SupportedGameIds;
                if (request.EntryFeeEur.HasValue) eventEntity.EntryFeeEur = request.EntryFeeEur;
                if (request.RegistrationDeadline.HasValue) eventEntity.RegistrationDeadline = request.RegistrationDeadline;
                if (request.Format != null) eventEntity.Format = string.IsNullOrWhiteSpace(request.Format) ? null : request.Format.Trim();
                if (request.GameId.HasValue) eventEntity.GameId = request.GameId;
                if (eventEntity.RegistrationDeadline > eventEntity.StartDate)
                    return BadRequest(new { message = "La chiusura delle iscrizioni deve precedere l'inizio" });

                // Più posti: entrano i primi della lista d'attesa
                await registrations.PromoteAsync(eventEntity, DateTime.UtcNow);
                eventEntity.ParticipantCount = EventRegistrations.SeatsTaken(eventEntity);
                await _context.SaveChangesAsync();

                return Ok(MapToDto(eventEntity, true));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'aggiornamento dell'evento {EventId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Cancella un evento (solo l'organizzatore)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> CancelEvent(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var eventEntity = await _eventRepository.GetEventWithParticipantsAsync(id);

                if (eventEntity == null)
                    return NotFound(new { message = "Evento non trovato" });

                if (eventEntity.OrganizerId != userId)
                    return Forbid();

                eventEntity.Status = EventStatus.Cancelled;
                _eventRepository.Update(eventEntity);
                await _eventRepository.SaveChangesAsync();

                // Notifica tutti i partecipanti
                foreach (var participant in eventEntity.Participants.Where(p => p.Status != ParticipationStatus.Cancelled))
                {
                    await _notificationService.SendAsync(
                        participant.UserId,
                        NotificationType.SystemAnnouncement,
                        "Evento cancellato",
                        $"L'evento \"{eventEntity.Title}\" del {eventEntity.StartDate:dd/MM/yyyy} è stato cancellato dall'organizzatore.",
                        eventEntity.Id,
                        "Event");
                }

                return Ok(new { message = "Evento cancellato con successo" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la cancellazione dell'evento {EventId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Partecipa a un evento (RSVP)
        /// </summary>
        [HttpPost("{id}/join")]
        public async Task<ActionResult> JoinEvent(int id, [FromServices] EventRegistrations registrations)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();
            JoinResult result;
            try
            {
                result = await registrations.JoinAsync(id, userId, DateTime.UtcNow);
            }
            catch (EventRegistrationException ex)
            {
                return ex.Message == "Evento non trovato" ? NotFound(new { message = ex.Message }) : BadRequest(new { message = ex.Message });
            }

            var eventEntity = await _context.Events.AsNoTracking().FirstAsync(e => e.Id == id);
            if (result.Outcome == JoinOutcome.Waitlisted)
                return Ok(new { status = "Waitlisted", waitlistPosition = result.WaitlistPosition,
                    message = $"L'evento è pieno: sei in lista d'attesa (posizione {result.WaitlistPosition}). Ti avvisiamo se si libera un posto." });

            if (eventEntity.StoreId != null)
                this.StoreActivity()?.Record(eventEntity.StoreId.Value, StoreActivityType.EventRegistration, userId, eventEntity.Id);
            var user = await _userRepository.GetByIdAsync(userId);
            await _notificationService.SendAsync(
                eventEntity.OrganizerId,
                NotificationType.EventUpdate,
                "Nuovo partecipante",
                $"{user?.Username ?? "Un utente"} si è iscritto al tuo evento \"{eventEntity.Title}\".",
                eventEntity.Id,
                "Event");
            return Ok(new { status = "Registered", message = "Iscrizione avvenuta con successo" });
        }

        /// <summary>Annulla l'iscrizione o esce dalla lista d'attesa; il posto passa al primo in lista.</summary>
        [HttpPost("{id}/leave")]
        public async Task<ActionResult> LeaveEvent(int id, [FromServices] EventRegistrations registrations)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();
            try
            {
                await registrations.LeaveAsync(id, userId, DateTime.UtcNow);
                return Ok(new { message = "Iscrizione annullata con successo" });
            }
            catch (EventRegistrationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Conferma il posto che si è liberato dalla lista d'attesa.</summary>
        [HttpPost("{id}/confirm-spot")]
        public async Task<ActionResult> ConfirmSpot(int id, [FromServices] EventRegistrations registrations)
        {
            try
            {
                await registrations.ConfirmOfferAsync(id, GetCurrentUserId(), DateTime.UtcNow);
                return Ok(new { message = "Posto confermato: sei iscritto" });
            }
            catch (EventRegistrationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Iscritti, presenze e lista d'attesa (solo l'organizzatore).</summary>
        [HttpGet("{id}/attendees")]
        public async Task<ActionResult> Attendees(int id)
        {
            var ev = await _context.Events.AsNoTracking().Include(e => e.Participants).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null) return NotFound(new { message = "Evento non trovato" });
            if (ev.OrganizerId != GetCurrentUserId()) return Forbid();
            var waitlist = EventRegistrations.Waitlist(ev).Select(p => p.UserId).ToList();
            var list = ev.Participants
                .Where(p => p.UserId != ev.OrganizerId && p.Status != ParticipationStatus.Cancelled)
                .Select(p => new EventAttendeeDto
                {
                    UserId = p.UserId,
                    Username = p.User?.Username ?? "N/A",
                    Status = p.Status.ToString(),
                    JoinedAt = p.CreatedAt,
                    CheckedInAt = p.CheckedInAt,
                    WaitlistPosition = waitlist.IndexOf(p.UserId) is var i and >= 0 ? i + 1 : null
                })
                .OrderBy(a => a.WaitlistPosition.HasValue).ThenBy(a => a.WaitlistPosition).ThenBy(a => a.Username)
                .ToList();
            return Ok(list);
        }

        /// <summary>L'organizzatore segna (o toglie) la presenza di un iscritto con un tocco.</summary>
        [HttpPost("{id}/attendees/{userId:int}/checkin")]
        public async Task<ActionResult> OrganizerCheckIn(int id, int userId, [FromQuery] bool present = true)
        {
            var ev = await _context.Events.Include(e => e.Participants).FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null) return NotFound(new { message = "Evento non trovato" });
            if (ev.OrganizerId != GetCurrentUserId()) return Forbid();
            var p = ev.Participants.FirstOrDefault(x => x.UserId == userId && EventRegistrations.HoldsSeat(x.Status));
            if (p == null) return BadRequest(new { message = "Questa persona non ha un posto all'evento" });
            var wasPresent = p.CheckedInAt != null;
            if (present)
            {
                p.Status = ParticipationStatus.Attended;
                p.CheckedInAt ??= DateTime.UtcNow;
                p.OfferExpiresAt = null;
            }
            else
            {
                p.Status = ParticipationStatus.Registered;
                p.CheckedInAt = null;
            }
            await _context.SaveChangesAsync();
            if (present && !wasPresent && ev.StoreId != null)
            {
                this.StoreActivity()?.Record(ev.StoreId.Value, StoreActivityType.EventCheckIn, userId, ev.Id);
                await StampLoyaltyAsync(ev, userId);
            }
            return Ok(new { userId, checkedInAt = p.CheckedInAt });
        }

        /// <summary>
        /// Ottiene gli eventi a cui l'utente è iscritto
        /// </summary>
        [HttpGet("my-events")]
        public async Task<ActionResult> GetMyEvents()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized();

                var events = await _eventRepository.GetUserEventsAsync(userId);
                var dtos = events.Select(e => MapToDto(e, true));

                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero dei miei eventi");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Ottiene gli eventi creati dall'utente corrente
        /// </summary>
        [HttpGet("organized")]
        public async Task<ActionResult> GetOrganizedEvents([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized();

                pageSize = Math.Clamp(pageSize, 1, 50);
                var (events, totalCount) = await _eventRepository.GetEventsByOrganizerAsync(userId, page, pageSize);
                var dtos = events.Select(e => MapToDto(e, true));

                return Ok(new { items = dtos, totalCount, page, pageSize });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero degli eventi organizzati");
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        private static EventDto MapToDto(Event e, bool isParticipating)
        {
            return new EventDto
            {
                Id = e.Id,
                Title = e.Title,
                Description = e.Description,
                Type = e.Type.ToString(),
                Status = e.Status.ToString(),
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                Address = e.Address,
                City = e.City,
                Province = e.Province,
                Country = e.Country,
                Latitude = e.Latitude,
                Longitude = e.Longitude,
                MaxParticipants = e.MaxParticipants,
                IsPublic = e.IsPublic,
                ImageUrl = e.ImageUrl,
                SupportedGameIds = e.SupportedGameIds,
                ParticipantCount = e.ParticipantCount,
                StoreId = e.StoreId,
                StoreName = e.Store?.Name,
                IsParticipating = isParticipating,
                CreatedAt = e.CreatedAt,
                EntryFeeEur = e.EntryFeeEur,
                RegistrationDeadline = e.RegistrationDeadline,
                Format = e.Format,
                GameId = e.GameId,
                RegistrationOpen = EventRegistrations.RegistrationOpen(e, DateTime.UtcNow),
                SpotsLeft = e.MaxParticipants.HasValue ? Math.Max(0, e.MaxParticipants.Value - e.ParticipantCount) : null,
                Organizer = e.Organizer != null ? new OrganizerDto
                {
                    Id = e.Organizer.Id,
                    Username = e.Organizer.Username,
                    AvatarUrl = e.Organizer.AvatarUrl,
                    ReputationScore = e.Organizer.ReputationScore
                } : new OrganizerDto()
            };
        }
    }
}
