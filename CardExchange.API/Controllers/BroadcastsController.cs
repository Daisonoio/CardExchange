using CardExchange.API.Services.Entitlements;
using CardExchange.API.Services.Moderation;
using CardExchange.API.Services.Social;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Avvisi "Sono qui": chi vende o colleziona fa sapere ai propri follower dove trovarlo
    /// (mercatino, fiera, stand). Nessuna transazione: è solo un'informazione.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/broadcasts")]
    public class BroadcastsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly BroadcastService _service;
        private readonly IUserBlockService _blocks;

        public BroadcastsController(ApplicationDbContext context, BroadcastService service, IUserBlockService blocks)
        {
            _context = context;
            _service = service;
            _blocks = blocks;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateBroadcastRequest request)
        {
            var me = CurrentUserId();
            if (me == 0) return Unauthorized();
            var result = await _service.CreateAsync(me, request);
            if (result.Broadcast == null) return StatusCode(result.StatusCode, new { message = result.Error });
            return Ok(await ToDtoAsync(result.Broadcast.Id, me, null, null));
        }

        /// <summary>Avvisi in corso dei profili che seguo (più il mio), dal più vicino.</summary>
        [HttpGet("live")]
        public async Task<IActionResult> Live([FromQuery] double? latitude, [FromQuery] double? longitude)
        {
            var me = CurrentUserId();
            var now = DateTime.UtcNow;
            var followedUsers = _context.Follows.Where(f => f.FollowerId == me && f.FollowedUserId != null).Select(f => f.FollowedUserId!.Value);
            var followedStores = _context.Follows.Where(f => f.FollowerId == me && f.FollowedStoreId != null).Select(f => f.FollowedStoreId!.Value);

            var ids = await _context.LocationBroadcasts.AsNoTracking()
                .Where(b => b.EndedAt == null && b.EndsAt > now &&
                            (b.AuthorId == me || followedUsers.Contains(b.AuthorId) || (b.StoreId != null && followedStores.Contains(b.StoreId.Value))))
                .Select(b => b.Id)
                .ToListAsync();

            var origin = await OriginAsync(me, latitude, longitude);
            var blocked = await _blocks.GetBlockedUserIdsAsync(me);
            var items = new List<BroadcastDto>();
            foreach (var id in ids)
            {
                var dto = await ToDtoAsync(id, me, origin.lat, origin.lon);
                if (dto != null && !blocked.Contains(dto.Author.Id)) items.Add(dto);
            }
            return Ok(items.OrderBy(b => b.IsMine ? 0 : 1).ThenBy(b => b.DistanceKm ?? int.MaxValue));
        }

        /// <summary>I miei avvisi recenti e quanti posso ancora inviarne oggi.</summary>
        [HttpGet("mine")]
        public async Task<IActionResult> Mine()
        {
            var me = CurrentUserId();
            var since = DateTime.UtcNow.Date;
            var ids = await _context.LocationBroadcasts.AsNoTracking()
                .Where(b => b.AuthorId == me)
                .OrderByDescending(b => b.CreatedAt)
                .Take(10)
                .Select(b => b.Id)
                .ToListAsync();
            var items = new List<BroadcastDto>();
            foreach (var id in ids)
            {
                var dto = await ToDtoAsync(id, me, null, null);
                if (dto != null) items.Add(dto);
            }
            var today = await _context.LocationBroadcasts.CountAsync(b => b.AuthorId == me && b.CreatedAt >= since);
            var maxPerDay = await this.Entitlements(_context).LimitAsync(me, PlanLimit.BroadcastsPerDay);
            return Ok(new
            {
                items,
                remainingToday = Math.Max(0, maxPerDay - today),
                maxPerDay,
                followers = await _context.Follows.CountAsync(f => f.FollowedUserId == me)
            });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id, [FromQuery] double? latitude, [FromQuery] double? longitude)
        {
            var me = CurrentUserId();
            var origin = await OriginAsync(me, latitude, longitude);
            var dto = await ToDtoAsync(id, me, origin.lat, origin.lon);
            if (dto == null || (!dto.IsMine && await _blocks.IsBlockedAsync(me, dto.Author.Id)))
                return NotFound(new { message = "Avviso non trovato" });
            return Ok(dto);
        }

        /// <summary>Chiude l'avviso prima della scadenza (es. il mercatino è finito prima).</summary>
        [HttpPost("{id:int}/end")]
        public async Task<IActionResult> End(int id)
        {
            var me = CurrentUserId();
            var b = await _context.LocationBroadcasts.FirstOrDefaultAsync(x => x.Id == id && x.AuthorId == me);
            if (b == null) return NotFound(new { message = "Avviso non trovato" });
            if (b.EndedAt == null && b.EndsAt > DateTime.UtcNow)
            {
                b.EndedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            return Ok(await ToDtoAsync(id, me, null, null));
        }

        // ---------------------------------------------------------------- helpers

        public class BroadcastAuthorDto { public int Id { get; set; } public string Username { get; set; } = ""; public string? AvatarUrl { get; set; } }
        public class BroadcastStoreDto { public int Id { get; set; } public string Name { get; set; } = ""; }
        public class BroadcastEventDto { public int Id { get; set; } public string Title { get; set; } = ""; }

        public class BroadcastDto
        {
            public int Id { get; set; }
            public BroadcastAuthorDto Author { get; set; } = new();
            public BroadcastStoreDto? Store { get; set; }
            public BroadcastEventDto? Event { get; set; }
            public string PlaceName { get; set; } = "";
            public string? SpotDetail { get; set; }
            public string? Message { get; set; }
            public decimal Latitude { get; set; }
            public decimal Longitude { get; set; }
            public DateTime StartsAt { get; set; }
            public DateTime EndsAt { get; set; }
            public bool IsLive { get; set; }
            public bool IsMine { get; set; }
            public int? DistanceKm { get; set; }
            /// <summary>Solo per l'autore.</summary>
            public int? RecipientCount { get; set; }
        }

        private async Task<BroadcastDto?> ToDtoAsync(int id, int me, double? lat, double? lon)
        {
            var b = await _context.LocationBroadcasts.AsNoTracking()
                .Include(x => x.Author)
                .Include(x => x.Store)
                .Include(x => x.Event)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (b == null) return null;
            var mine = b.AuthorId == me;
            double? km = lat.HasValue && lon.HasValue ? Geo.DistanceKm(lat.Value, lon.Value, (double)b.Latitude, (double)b.Longitude) : null;
            return new BroadcastDto
            {
                Id = b.Id,
                Author = new BroadcastAuthorDto { Id = b.Author.Id, Username = b.Author.Username, AvatarUrl = b.Author.AvatarUrl },
                Store = b.Store == null ? null : new BroadcastStoreDto { Id = b.Store.Id, Name = b.Store.Name },
                Event = b.Event == null ? null : new BroadcastEventDto { Id = b.Event.Id, Title = b.Event.Title },
                PlaceName = b.PlaceName,
                SpotDetail = b.SpotDetail,
                Message = b.Message,
                Latitude = b.Latitude,
                Longitude = b.Longitude,
                StartsAt = b.StartsAt,
                EndsAt = b.EndedAt ?? b.EndsAt,
                IsLive = b.IsLive,
                IsMine = mine,
                DistanceKm = mine ? null : BroadcastRules.DisplayDistanceKm(km),
                RecipientCount = mine ? b.RecipientCount : null
            };
        }

        /// <summary>Punto da cui misurare le distanze: quello indicato dal client o la posizione del profilo.</summary>
        private async Task<(double? lat, double? lon)> OriginAsync(int me, double? lat, double? lon)
        {
            if (lat.HasValue && lon.HasValue) return (lat, lon);
            var loc = await _context.UserLocations.AsNoTracking()
                .Where(l => l.UserId == me)
                .Select(l => new { l.Latitude, l.Longitude })
                .FirstOrDefaultAsync();
            return loc?.Latitude != null && loc.Longitude != null ? ((double)loc.Latitude, (double)loc.Longitude) : (null, null);
        }

        private int CurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
        }
    }
}
