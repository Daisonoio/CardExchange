using CardExchange.API.Authorization;
using CardExchange.API.Services;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Negozi di carte: elenco e mappa dei negozi verificati, richiesta di inserimento, gestione della propria scheda,
    /// verifica da parte dei moderatori.
    /// </summary>
    [ApiController]
    [Authorize]
    public class StoresController : ControllerBase
    {
        public const int MaxStoresPerOwner = 3;

        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notifications;
        private readonly ILogger<StoresController> _logger;

        public StoresController(ApplicationDbContext context, INotificationService notifications, ILogger<StoresController> logger)
        {
            _context = context;
            _notifications = notifications;
            _logger = logger;
        }

        // ---------------- DTO ----------------

        public class StoreRequest
        {
            [Required(ErrorMessage = "Il nome è obbligatorio"), MaxLength(150)]
            public string Name { get; set; } = string.Empty;

            [MaxLength(1000)]
            public string? Description { get; set; }

            [Required(ErrorMessage = "L'indirizzo è obbligatorio"), MaxLength(250)]
            public string Address { get; set; } = string.Empty;

            [Required(ErrorMessage = "La città è obbligatoria"), MaxLength(100)]
            public string City { get; set; } = string.Empty;

            [MaxLength(50)] public string? Province { get; set; }
            [MaxLength(10)] public string? PostalCode { get; set; }
            [MaxLength(50)] public string Country { get; set; } = "IT";

            [Range(-90, 90)] public decimal Latitude { get; set; }
            [Range(-180, 180)] public decimal Longitude { get; set; }

            [MaxLength(30)] public string? Phone { get; set; }
            [MaxLength(200)] public string? Website { get; set; }
            [MaxLength(100)] public string? Instagram { get; set; }
            [MaxLength(500)] public string? OpeningHours { get; set; }

            /// <summary>Id dei giochi trattati.</summary>
            public List<int> GameIds { get; set; } = new();

            public bool IsSafeMeetupSpot { get; set; } = true;
        }

        public class StoreDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string Address { get; set; } = string.Empty;
            public string City { get; set; } = string.Empty;
            public string? Province { get; set; }
            public string? PostalCode { get; set; }
            public string Country { get; set; } = string.Empty;
            public decimal Latitude { get; set; }
            public decimal Longitude { get; set; }
            public string? Phone { get; set; }
            public string? Website { get; set; }
            public string? Instagram { get; set; }
            public string? OpeningHours { get; set; }
            public List<int> GameIds { get; set; } = new();
            public bool IsSafeMeetupSpot { get; set; }
            public string Status { get; set; } = string.Empty;
            public string? ReviewNote { get; set; }
            public bool IsOwner { get; set; }
            public double? DistanceKm { get; set; }
            public int UpcomingEventsCount { get; set; }
            /// <summary>Il proprietario ha il piano Negozio attivo: scheda in evidenza in cima all'elenco.</summary>
            public bool IsFeatured { get; set; }
            /// <summary>Identificativo per link e QR (/s/{slug}).</summary>
            public string? Slug { get; set; }
        }

        public class StorePublicDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string City { get; set; } = string.Empty;
            public string Slug { get; set; } = string.Empty;
        }

        // ---------------- Pubblico ----------------

        /// <summary>
        /// Negozi verificati, ordinati per distanza dalle coordinate indicate (o dalla posizione del profilo).
        /// Senza posizione restituisce l'elenco alfabetico.
        /// </summary>
        [HttpGet("api/stores")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<StoreDto>>> List(
            [FromQuery] double? latitude, [FromQuery] double? longitude, [FromQuery] int radiusKm = 50,
            [FromQuery] int? gameId = null, [FromQuery] bool meetupOnly = false, [FromQuery] string? q = null)
        {
            radiusKm = Math.Clamp(radiusKm, 1, 500);
            var userId = CurrentUserId();
            if ((latitude == null || longitude == null) && userId > 0)
            {
                var loc = await _context.UserLocations.AsNoTracking().FirstOrDefaultAsync(l => l.UserId == userId);
                if (loc?.Latitude != null && loc.Longitude != null)
                {
                    latitude = (double)loc.Latitude.Value;
                    longitude = (double)loc.Longitude.Value;
                }
            }

            var query = _context.Stores.AsNoTracking().Where(s => s.Status == StoreStatus.Verified);
            if (meetupOnly) query = query.Where(s => s.IsSafeMeetupSpot);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                query = query.Where(s => s.Name.ToLower().Contains(term) || s.City.ToLower().Contains(term));
            }

            var stores = await query.ToListAsync();
            if (gameId.HasValue)
                stores = stores.Where(s => ParseGameIds(s.SupportedGameIds).Contains(gameId.Value)).ToList();

            var eventCounts = await UpcomingEventCountsAsync(stores.Select(s => s.Id).ToList());
            var featured = await FeaturedOwnerIdsAsync(stores.Select(s => s.OwnerId).Distinct().ToList());

            // I negozi con piano Negozio attivo vengono prima (sempre entro il raggio), poi distanza o nome
            IEnumerable<StoreDto> result;
            if (latitude.HasValue && longitude.HasValue)
            {
                result = stores
                    .Select(s => ToDto(s, userId, eventCounts, Geo.DistanceKm(latitude.Value, longitude.Value, (double)s.Latitude, (double)s.Longitude), featured))
                    .Where(d => d.DistanceKm <= radiusKm)
                    .OrderByDescending(d => d.IsFeatured)
                    .ThenBy(d => d.DistanceKm);
            }
            else
            {
                result = stores.Select(s => ToDto(s, userId, eventCounts, null, featured))
                    .OrderByDescending(d => d.IsFeatured)
                    .ThenBy(d => d.Name);
            }
            return Ok(result.Take(100));
        }

        [HttpGet("api/stores/{id:int}")]
        [AllowAnonymous]
        public async Task<ActionResult<StoreDto>> Get(int id)
        {
            var store = await _context.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
            var userId = CurrentUserId();
            // Le schede non ancora verificate sono visibili solo al proprietario e ai moderatori
            if (store == null || (store.Status != StoreStatus.Verified && store.OwnerId != userId && !IsModerator()))
                return NotFound(new { message = "Negozio non trovato" });

            var counts = await UpcomingEventCountsAsync(new List<int> { id });
            var featured = await FeaturedOwnerIdsAsync(new List<int> { store.OwnerId });
            return Ok(ToDto(store, userId, counts, null, featured));
        }

        // ---------------- Gestione della propria scheda ----------------

        [HttpGet("api/stores/mine")]
        public async Task<ActionResult<IEnumerable<StoreDto>>> Mine()
        {
            var userId = CurrentUserId();
            var stores = await _context.Stores.AsNoTracking().Where(s => s.OwnerId == userId).OrderBy(s => s.Name).ToListAsync();
            var counts = await UpcomingEventCountsAsync(stores.Select(s => s.Id).ToList());
            return Ok(stores.Select(s => ToDto(s, userId, counts, null)));
        }

        /// <summary>Richiede l'inserimento di un negozio: resta in attesa finché un moderatore non lo verifica.</summary>
        [HttpPost("api/stores")]
        public async Task<ActionResult<StoreDto>> Create([FromBody] StoreRequest request)
        {
            var userId = CurrentUserId();
            if (userId == 0) return Unauthorized();
            if (request.Latitude == 0 && request.Longitude == 0)
                return BadRequest(new { message = "Indica la posizione del negozio sulla mappa" });

            var owned = await _context.Stores.CountAsync(s => s.OwnerId == userId && s.Status != StoreStatus.Rejected);
            if (owned >= MaxStoresPerOwner)
                return BadRequest(new { message = $"Puoi gestire al massimo {MaxStoresPerOwner} negozi" });

            var store = new Store { OwnerId = userId };
            Apply(store, request);
            store.Slug = await UniqueSlugAsync(store.Name, store.City);
            _context.Stores.Add(store);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Negozio {StoreId} '{Name}' inserito da {UserId}, in attesa di verifica", store.Id, store.Name, userId);
            return Ok(ToDto(store, userId, new Dictionary<int, int>(), null));
        }

        /// <summary>Modifica della scheda (solo il proprietario). Le modifiche a un negozio rifiutato lo rimettono in verifica.</summary>
        [HttpPut("api/stores/{id:int}")]
        public async Task<ActionResult<StoreDto>> Update(int id, [FromBody] StoreRequest request)
        {
            var userId = CurrentUserId();
            var store = await _context.Stores.FirstOrDefaultAsync(s => s.Id == id);
            if (store == null) return NotFound(new { message = "Negozio non trovato" });
            if (store.OwnerId != userId) return Forbid();
            if (store.Status == StoreStatus.Suspended)
                return BadRequest(new { message = "Il negozio è sospeso: contatta il supporto" });

            Apply(store, request);
            if (store.Status == StoreStatus.Rejected) store.Status = StoreStatus.Pending;
            await _context.SaveChangesAsync();
            var counts = await UpcomingEventCountsAsync(new List<int> { id });
            return Ok(ToDto(store, userId, counts, null));
        }

        // ---------------- Moderazione ----------------

        [HttpGet("api/moderation/stores")]
        [RequirePermission("ADMIN.REPORTS.MANAGE")]
        public async Task<IActionResult> PendingStores([FromQuery] string status = "pending")
        {
            var wanted = status.ToLowerInvariant() switch
            {
                "verified" => StoreStatus.Verified,
                "rejected" => StoreStatus.Rejected,
                "suspended" => StoreStatus.Suspended,
                _ => StoreStatus.Pending
            };
            var stores = await _context.Stores
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => !s.IsDeleted && s.Status == wanted)
                .OrderBy(s => s.CreatedAt)
                .Select(s => new { store = s, ownerUsername = s.Owner.Username })
                .ToListAsync();
            return Ok(stores.Select(x => new { x.ownerUsername, store = ToDto(x.store, 0, new Dictionary<int, int>(), null), createdAt = x.store.CreatedAt }));
        }

        public class ReviewStoreRequest
        {
            /// <summary>verify, reject o suspend.</summary>
            [Required] public string Decision { get; set; } = "verify";
            [MaxLength(500)] public string? Note { get; set; }
        }

        [HttpPost("api/moderation/stores/{id:int}/review")]
        [RequirePermission("ADMIN.REPORTS.MANAGE")]
        public async Task<IActionResult> Review(int id, [FromBody] ReviewStoreRequest request)
        {
            var store = await _context.Stores.FirstOrDefaultAsync(s => s.Id == id);
            if (store == null) return NotFound(new { message = "Negozio non trovato" });

            var newStatus = request.Decision.ToLowerInvariant() switch
            {
                "verify" => StoreStatus.Verified,
                "reject" => StoreStatus.Rejected,
                "suspend" => StoreStatus.Suspended,
                _ => (StoreStatus?)null
            };
            if (newStatus == null) return BadRequest(new { message = "Decisione non valida (verify, reject, suspend)" });
            if (newStatus != StoreStatus.Verified && string.IsNullOrWhiteSpace(request.Note))
                return BadRequest(new { message = "Indica il motivo, verrà mostrato al proprietario" });

            store.Status = newStatus.Value;
            store.ReviewedAt = DateTime.UtcNow;
            store.ReviewedById = CurrentUserId();
            store.ReviewNote = request.Note?.Trim();
            await _context.SaveChangesAsync();

            var (title, body) = newStatus switch
            {
                StoreStatus.Verified => ("Negozio verificato", $"{store.Name} ora è visibile a tutti su CardExchange."),
                StoreStatus.Rejected => ("Negozio non approvato", $"{store.Name}: {store.ReviewNote}"),
                _ => ("Negozio sospeso", $"{store.Name}: {store.ReviewNote}")
            };
            try
            {
                await _notifications.SendAsync(store.OwnerId, NotificationType.SystemAnnouncement, title, body, store.Id, "Store");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notifica esito verifica negozio {StoreId} non inviata", store.Id);
            }

            _logger.LogInformation("Negozio {StoreId} -> {Status} da {ModeratorId}", id, newStatus, CurrentUserId());
            return Ok(new { message = title });
        }

        // ---------------- Helper ----------------

        private static void Apply(Store store, StoreRequest r)
        {
            store.Name = r.Name.Trim();
            store.Description = r.Description?.Trim();
            store.Address = r.Address.Trim();
            store.City = r.City.Trim();
            store.Province = r.Province?.Trim();
            store.PostalCode = r.PostalCode?.Trim();
            store.Country = string.IsNullOrWhiteSpace(r.Country) ? "IT" : r.Country.Trim();
            store.Latitude = Math.Round(r.Latitude, 6);
            store.Longitude = Math.Round(r.Longitude, 6);
            store.Phone = r.Phone?.Trim();
            store.Website = NormalizeUrl(r.Website);
            store.Instagram = r.Instagram?.Trim().TrimStart('@');
            store.OpeningHours = r.OpeningHours?.Trim();
            store.SupportedGameIds = r.GameIds.Count > 0 ? string.Join(",", r.GameIds.Distinct().Take(20)) : null;
            store.IsSafeMeetupSpot = r.IsSafeMeetupSpot;
        }

        /// <summary>Accetta solo link http(s): evita javascript: e simili nei link mostrati agli utenti.</summary>
        private static string? NormalizeUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var trimmed = url.Trim();
            if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                trimmed = "https://" + trimmed;
            return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? uri.ToString() : null;
        }

        private static List<int> ParseGameIds(string? csv) =>
            string.IsNullOrWhiteSpace(csv) ? new List<int>()
                : csv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var v) ? v : 0).Where(v => v > 0).ToList();

        private async Task<Dictionary<int, int>> UpcomingEventCountsAsync(List<int> storeIds)
        {
            if (storeIds.Count == 0) return new Dictionary<int, int>();
            var now = DateTime.UtcNow;
            return await _context.Events
                .Where(e => e.StoreId != null && storeIds.Contains(e.StoreId.Value) && e.Status == EventStatus.Published && e.StartDate >= now)
                .GroupBy(e => e.StoreId!.Value)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
        }

        /// <summary>Proprietari con un abbonamento di livello Negozio in corso.</summary>
        private async Task<HashSet<int>> FeaturedOwnerIdsAsync(List<int> ownerIds)
        {
            if (ownerIds.Count == 0) return new HashSet<int>();
            var now = DateTime.UtcNow;
            var ids = await _context.UserSubscriptions
                .Where(s => ownerIds.Contains(s.UserId) && s.Status == SubscriptionStatus.Active
                            && s.StartDate <= now && s.EndDate > now && s.Plan.Tier == SubscriptionTier.Store)
                .Select(s => s.UserId)
                .Distinct()
                .ToListAsync();
            return ids.ToHashSet();
        }

        private static StoreDto ToDto(Store s, int currentUserId, Dictionary<int, int> eventCounts, double? distanceKm,
            HashSet<int>? featuredOwners = null) => new()
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            Address = s.Address,
            City = s.City,
            Province = s.Province,
            PostalCode = s.PostalCode,
            Country = s.Country,
            Latitude = s.Latitude,
            Longitude = s.Longitude,
            Phone = s.Phone,
            Website = s.Website,
            Instagram = s.Instagram,
            OpeningHours = s.OpeningHours,
            GameIds = ParseGameIds(s.SupportedGameIds),
            IsSafeMeetupSpot = s.IsSafeMeetupSpot,
            Status = s.Status.ToString(),
            ReviewNote = s.OwnerId == currentUserId ? s.ReviewNote : null,
            IsOwner = currentUserId != 0 && s.OwnerId == currentUserId,
            DistanceKm = distanceKm.HasValue ? Math.Round(distanceKm.Value, 1) : null,
            UpcomingEventsCount = eventCounts.GetValueOrDefault(s.Id),
            IsFeatured = s.Status == StoreStatus.Verified && featuredOwners != null && featuredOwners.Contains(s.OwnerId),
            Slug = s.Slug
        };

        /// <summary>Slug libero a partire da nome e città ("Games Corner", "Milano" -> "games-corner-milano").</summary>
        private async Task<string> UniqueSlugAsync(string name, string city)
        {
            var baseSlug = Slugs.From($"{name} {city}");
            var taken = await _context.Stores.IgnoreQueryFilters()
                .Where(s => s.Slug != null && s.Slug.StartsWith(baseSlug))
                .Select(s => s.Slug!)
                .ToListAsync();
            return Slugs.Unique(baseSlug, taken.Contains);
        }

        // ---------------- Link e QR del negozio ----------------

        /// <summary>Negozio dal link breve /s/{slug} stampato nel QR (anche senza accesso, per la pagina di registrazione).</summary>
        [HttpGet("api/stores/by-slug/{slug}")]
        [AllowAnonymous]
        public async Task<ActionResult<StorePublicDto>> BySlug(string slug)
        {
            var store = await _context.Stores.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Slug == slug.ToLower() && s.Status == StoreStatus.Verified);
            if (store == null) return NotFound(new { message = "Negozio non trovato" });
            return new StorePublicDto { Id = store.Id, Name = store.Name, City = store.City, Slug = store.Slug! };
        }

        /// <summary>
        /// Scambi fissati in negozio nei prossimi giorni (solo il proprietario): data e ora, mai i nomi dei partecipanti.
        /// </summary>
        [HttpGet("api/stores/{id:int}/meetups")]
        public async Task<IActionResult> Meetups(int id, [FromQuery] int days = 7)
        {
            var store = await _context.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
            if (store == null) return NotFound(new { message = "Negozio non trovato" });
            if (store.OwnerId != CurrentUserId()) return Forbid();
            var now = DateTime.UtcNow;
            var until = now.AddDays(Math.Clamp(days, 1, 30));
            var list = await _context.TradeMeetups.AsNoTracking()
                .Where(m => m.StoreId == id && m.Status == MeetupStatus.Accepted && m.ScheduledAt > now && m.ScheduledAt < until)
                .OrderBy(m => m.ScheduledAt)
                .Select(m => new { m.Id, m.ScheduledAt })
                .ToListAsync();
            var since = now.AddDays(-30);
            var completed = await _context.TradeMeetups.AsNoTracking()
                .CountAsync(m => m.StoreId == id && m.Status == MeetupStatus.Accepted && m.ScheduledAt < now && m.ScheduledAt >= since
                                 && m.TradeOffer.Status == TradeOfferStatus.Completed);
            return Ok(new { upcoming = list, completedLast30Days = completed });
        }

        /// <summary>Quanti utenti si sono registrati dal QR del negozio (solo il proprietario, solo il numero).</summary>
        [HttpGet("api/stores/{id:int}/attributions")]
        public async Task<IActionResult> Attributions(int id)
        {
            var store = await _context.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
            if (store == null) return NotFound(new { message = "Negozio non trovato" });
            if (store.OwnerId != CurrentUserId() && !IsModerator()) return Forbid();
            var since = DateTime.UtcNow.AddDays(-30);
            return Ok(new
            {
                total = await _context.StoreAttributions.CountAsync(a => a.StoreId == id),
                last30Days = await _context.StoreAttributions.CountAsync(a => a.StoreId == id && a.CreatedAt >= since)
            });
        }

        private bool IsModerator() => User.HasClaim("Permission", "ADMIN.REPORTS.MANAGE");

        private int CurrentUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
    }
}
