using CardExchange.API.Services.Entitlements;
using CardExchange.API.Services.Moderation;
using CardExchange.API.Services.Notifications;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Social
{
    public class CreateBroadcastRequest
    {
        /// <summary>Pubblica a nome di un proprio negozio verificato (facoltativo).</summary>
        public int? StoreId { get; set; }
        public int? EventId { get; set; }
        public string PlaceName { get; set; } = string.Empty;
        public string? SpotDetail { get; set; }
        public string? Message { get; set; }
        /// <summary>Se mancano si usano quelle del negozio o dell'evento.</summary>
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public int DurationMinutes { get; set; } = 240;
    }

    public record BroadcastResult(LocationBroadcast? Broadcast, string? Error, int StatusCode = 400);

    /// <summary>
    /// Avvisi "Sono qui": validazione, salvataggio e recapito a chi segue l'autore (o il suo negozio).
    /// Ogni follower riceve la propria distanza dal luogo, calcolata dalla posizione del suo profilo.
    /// </summary>
    public class BroadcastService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationDispatcher _dispatcher;
        private readonly IUserBlockService _blocks;
        private readonly ILogger<BroadcastService> _logger;

        private readonly IEntitlementService _entitlements;

        public BroadcastService(ApplicationDbContext context, INotificationDispatcher dispatcher, IUserBlockService blocks, ILogger<BroadcastService> logger,
            IEntitlementService? entitlements = null)
        {
            _entitlements = entitlements.OrDefault(context);
            _context = context;
            _dispatcher = dispatcher;
            _blocks = blocks;
            _logger = logger;
        }

        public async Task<BroadcastResult> CreateAsync(int authorId, CreateBroadcastRequest request)
        {
            var placeName = request.PlaceName?.Trim() ?? "";
            if (placeName.Length < 2 || placeName.Length > 100)
                return new(null, "Indica il nome del posto (da 2 a 100 caratteri)");
            var spot = string.IsNullOrWhiteSpace(request.SpotDetail) ? null : request.SpotDetail.Trim();
            if (spot?.Length > 120) return new(null, "Il punto preciso può avere al massimo 120 caratteri");
            var message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim();
            if (message?.Length > 280) return new(null, "Il messaggio può avere al massimo 280 caratteri");

            var author = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == authorId);
            if (author == null || !author.IsActive) return new(null, "Account non attivo", 403);

            decimal? lat = request.Latitude, lon = request.Longitude;

            Store? store = null;
            if (request.StoreId.HasValue)
            {
                store = await _context.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.StoreId.Value);
                if (store == null || store.OwnerId != authorId) return new(null, "Puoi pubblicare solo a nome dei tuoi negozi", 403);
                if (store.Status != StoreStatus.Verified) return new(null, "Il negozio deve essere verificato prima di pubblicare avvisi", 403);
            }

            Event? ev = null;
            if (request.EventId.HasValue)
            {
                ev = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.EventId.Value);
                if (ev == null || ev.Status == EventStatus.Cancelled || (!ev.IsPublic && ev.OrganizerId != authorId))
                    return new(null, "Evento non disponibile");
                if (lat == null && ev.Latitude.HasValue && ev.Longitude.HasValue) { lat = ev.Latitude; lon = ev.Longitude; }
            }
            if (lat == null && store != null) { lat = store.Latitude; lon = store.Longitude; }

            // Espositore di una fiera con pacchetto organizzatori: stand già compilato e nessun limite giornaliero
            var exhibitor = ev == null ? null
                : await CardExchange.API.Services.Events.EventPackages.ExhibitorForBroadcastAsync(_context, ev, authorId, store?.Id, DateTime.UtcNow);
            if (exhibitor != null) spot ??= exhibitor.BoothCode;

            if (lat == null || lon == null || !BroadcastRules.IsValidCoordinate(lat.Value, lon.Value))
                return new(null, "Indica dove ti trovi: usa la tua posizione o scegli il punto sulla mappa");

            // Limite giornaliero anti-spam (giorno UTC)
            var since = DateTime.UtcNow.Date;
            var today = await _context.LocationBroadcasts.CountAsync(b => b.AuthorId == authorId && b.CreatedAt >= since && b.ExhibitorId == null);
            var maxPerDay = await _entitlements.LimitAsync(authorId, PlanLimit.BroadcastsPerDay);
            if (exhibitor == null && today >= maxPerDay)
                return new(null, $"Puoi inviare al massimo {maxPerDay} avvisi al giorno", 429);

            var now = DateTime.UtcNow;

            // Un solo avviso attivo per autore: il nuovo sostituisce il precedente (ci si è spostati)
            var previous = await _context.LocationBroadcasts
                .Where(b => b.AuthorId == authorId && b.EndedAt == null && b.EndsAt > now)
                .ToListAsync();
            foreach (var p in previous) p.EndedAt = now;

            var broadcast = new LocationBroadcast
            {
                AuthorId = authorId,
                StoreId = store?.Id,
                EventId = ev?.Id,
                ExhibitorId = exhibitor?.Id,
                PlaceName = placeName,
                SpotDetail = spot,
                Message = message,
                Latitude = Math.Round(lat.Value, 6),
                Longitude = Math.Round(lon.Value, 6),
                StartsAt = now,
                EndsAt = now + BroadcastRules.ClampDuration(request.DurationMinutes)
            };
            _context.LocationBroadcasts.Add(broadcast);
            await _context.SaveChangesAsync();

            try
            {
                broadcast.RecipientCount = await NotifyFollowersAsync(broadcast, author, store);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // L'avviso resta visibile nella sezione "Live" anche se il recapito non è riuscito
                _logger.LogError(ex, "Recapito dell'avviso {BroadcastId} non riuscito", broadcast.Id);
            }

            return new(broadcast, null);
        }

        /// <summary>Follower dell'autore e, se pubblicato come negozio, del negozio; senza doppioni né utenti bloccati.</summary>
        private async Task<int> NotifyFollowersAsync(LocationBroadcast broadcast, User author, Store? store)
        {
            var followerIds = await _context.Follows.AsNoTracking()
                .Where(f => f.FollowedUserId == author.Id || (store != null && f.FollowedStoreId == store.Id))
                .Select(f => f.FollowerId)
                .Distinct()
                .ToListAsync();

            var blocked = await _blocks.GetBlockedUserIdsAsync(author.Id);
            followerIds = followerIds.Where(id => id != author.Id && !blocked.Contains(id)).ToList();
            if (followerIds.Count == 0) return 0;

            var recipients = await _context.Users.AsNoTracking()
                .Where(u => followerIds.Contains(u.Id) && u.IsActive)
                .Select(u => new
                {
                    u.Id,
                    Lat = u.Location != null ? u.Location.Latitude : null,
                    Lon = u.Location != null ? u.Location.Longitude : null,
                    u.TimeZone
                })
                .ToListAsync();

            var name = store != null ? store.Name : "@" + author.Username;

            var payloads = recipients.Select(r =>
            {
                double? km = r.Lat.HasValue && r.Lon.HasValue
                    ? Geo.DistanceKm(r.Lat.Value, r.Lon.Value, broadcast.Latitude, broadcast.Longitude)
                    : null;
                return new NotificationPayload
                {
                    UserId = r.Id,
                    Type = NotificationType.FollowedNearby,
                    Title = BroadcastRules.Title(name, BroadcastRules.DisplayDistanceKm(km)),
                    Body = BroadcastRules.Body(broadcast.PlaceName, broadcast.SpotDetail, broadcast.EndsAt, broadcast.Message, r.TimeZone),
                    ReferenceId = broadcast.Id,
                    ReferenceType = "Broadcast"
                };
            }).ToList();

            return await _dispatcher.DeliverManyAsync(payloads);
        }
    }
}
