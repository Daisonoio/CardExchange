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
            var active = ev.Participants.Count(p => p.Status != ParticipationStatus.Cancelled);
            if (participant == null || participant.Status == ParticipationStatus.Cancelled)
            {
                if (ev.MaxParticipants.HasValue && active >= ev.MaxParticipants.Value)
                    return BadRequest(new { message = "Posti esauriti" });
                if (participant == null)
                {
                    participant = new EventParticipant { EventId = id, UserId = userId };
                    ev.Participants.Add(participant);
                }
                ev.ParticipantCount = active + 1;
            }

            participant.Status = ParticipationStatus.Attended;
            participant.CheckedInAt = now;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Sei presente! Ora vedi chi c'è e con chi puoi scambiare." });
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
                var isParticipating = eventEntity.Participants
                    .Any(p => p.UserId == currentUserId && p.Status != ParticipationStatus.Cancelled);

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
                    ParticipantCount = eventEntity.Participants.Count(p => p.Status != ParticipationStatus.Cancelled),
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
                        .Where(p => p.Status != ParticipationStatus.Cancelled && !blocked.Contains(p.UserId))
                        .Select(p => new EventParticipantDto
                        {
                            UserId = p.UserId,
                            Username = p.User?.Username ?? "N/A",
                            AvatarUrl = p.User?.AvatarUrl,
                            Status = p.Status.ToString(),
                            JoinedAt = p.CreatedAt
                        })
                };

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
                    StoreId = store?.Id
                };

                // Aggiungi organizzatore come primo partecipante
                var participant = new EventParticipant
                {
                    UserId = userId,
                    Status = ParticipationStatus.Confirmed
                };
                eventEntity.Participants.Add(participant);
                eventEntity.ParticipantCount = 1;

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
        public async Task<ActionResult> UpdateEvent(int id, [FromBody] UpdateEventRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var eventEntity = await _eventRepository.GetByIdAsync(id);

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

                _eventRepository.Update(eventEntity);
                await _eventRepository.SaveChangesAsync();

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
        public async Task<ActionResult> JoinEvent(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized();

                var eventEntity = await _eventRepository.GetEventWithParticipantsAsync(id);
                if (eventEntity == null)
                    return NotFound(new { message = "Evento non trovato" });

                if (eventEntity.Status != EventStatus.Published)
                    return BadRequest(new { message = "L'evento non è attivo" });

                if (eventEntity.StartDate <= DateTime.UtcNow)
                    return BadRequest(new { message = "L'evento è già iniziato o terminato" });

                // Controlla se già iscritto
                var existing = await _eventRepository.GetParticipantAsync(id, userId);
                if (existing != null && existing.Status != ParticipationStatus.Cancelled)
                    return BadRequest(new { message = "Sei già iscritto a questo evento" });

                // Controlla capacità massima
                if (eventEntity.MaxParticipants.HasValue)
                {
                    var currentCount = eventEntity.Participants.Count(p => p.Status != ParticipationStatus.Cancelled);
                    if (currentCount >= eventEntity.MaxParticipants.Value)
                        return BadRequest(new { message = "L'evento ha raggiunto il numero massimo di partecipanti" });
                }

                if (existing != null && existing.Status == ParticipationStatus.Cancelled)
                {
                    // Ri-iscrizione
                    existing.Status = ParticipationStatus.Registered;
                    existing.IsDeleted = false;
                }
                else
                {
                    var participant = new EventParticipant
                    {
                        EventId = id,
                        UserId = userId,
                        Status = ParticipationStatus.Registered
                    };
                    eventEntity.Participants.Add(participant);
                }

                eventEntity.ParticipantCount = eventEntity.Participants.Count(p => p.Status != ParticipationStatus.Cancelled);
                await _eventRepository.SaveChangesAsync();

                // Notifica organizzatore
                var user = await _userRepository.GetByIdAsync(userId);
                await _notificationService.SendAsync(
                    eventEntity.OrganizerId,
                    NotificationType.SystemAnnouncement,
                    "Nuovo partecipante",
                    $"{user?.Username ?? "Un utente"} si è iscritto al tuo evento \"{eventEntity.Title}\".",
                    eventEntity.Id,
                    "Event");

                return Ok(new { message = "Iscrizione avvenuta con successo" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'iscrizione all'evento {EventId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
        }

        /// <summary>
        /// Annulla la partecipazione a un evento
        /// </summary>
        [HttpPost("{id}/leave")]
        public async Task<ActionResult> LeaveEvent(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized();

                var participant = await _eventRepository.GetParticipantAsync(id, userId);
                if (participant == null || participant.Status == ParticipationStatus.Cancelled)
                    return BadRequest(new { message = "Non sei iscritto a questo evento" });

                participant.Status = ParticipationStatus.Cancelled;

                // Aggiorna contatore
                var eventEntity = await _eventRepository.GetByIdAsync(id);
                if (eventEntity != null)
                {
                    eventEntity.ParticipantCount = await _eventRepository.GetParticipantCountAsync(id) - 1;
                }

                await _eventRepository.SaveChangesAsync();

                return Ok(new { message = "Iscrizione annullata con successo" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'annullamento iscrizione all'evento {EventId}", id);
                return StatusCode(500, new { message = "Errore interno del server" });
            }
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
