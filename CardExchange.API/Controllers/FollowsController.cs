using CardExchange.API.Services.Stores;
using CardExchange.API.Services.Moderation;
using CardExchange.API.Services.Notifications;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// "Segui": un utente segue altri utenti o negozi per ricevere i loro avvisi "Sono qui".
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/follows")]
    public class FollowsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserBlockService _blocks;
        private readonly INotificationDispatcher _dispatcher;

        public FollowsController(ApplicationDbContext context, IUserBlockService blocks, INotificationDispatcher dispatcher)
        {
            _context = context;
            _blocks = blocks;
            _dispatcher = dispatcher;
        }

        // ---------------- Utenti ----------------

        [HttpGet("users/{id:int}")]
        public async Task<IActionResult> GetUserStatus(int id)
        {
            var me = CurrentUserId();
            return Ok(new
            {
                following = await _context.Follows.AnyAsync(f => f.FollowerId == me && f.FollowedUserId == id),
                followers = await _context.Follows.CountAsync(f => f.FollowedUserId == id)
            });
        }

        [HttpPost("users/{id:int}")]
        public async Task<IActionResult> FollowUser(int id)
        {
            var me = CurrentUserId();
            if (id == me) return BadRequest(new { message = "Non puoi seguire te stesso" });
            var target = await _context.Users.AsNoTracking().Where(u => u.Id == id && u.IsActive).Select(u => new { u.Id }).FirstOrDefaultAsync();
            if (target == null) return NotFound(new { message = "Utente non trovato" });
            if (await _blocks.IsBlockedAsync(me, id)) return StatusCode(403, new { message = "Non puoi seguire questo utente" });

            if (!await _context.Follows.AnyAsync(f => f.FollowerId == me && f.FollowedUserId == id))
            {
                _context.Follows.Add(new Follow { FollowerId = me, FollowedUserId = id });
                await _context.SaveChangesAsync();

                var myName = await _context.Users.Where(u => u.Id == me).Select(u => u.Username).FirstAsync();
                await _dispatcher.DeliverManyAsync(new[]
                {
                    new NotificationPayload
                    {
                        UserId = id,
                        Type = NotificationType.NewFollower,
                        Title = $"@{myName} ha iniziato a seguirti",
                        Body = "Riceverà i tuoi avvisi \"Sono qui\" quando sei a un mercatino o a una fiera.",
                        ReferenceId = me,
                        ReferenceType = "Follower"
                    }
                });
            }
            return await GetUserStatus(id);
        }

        [HttpDelete("users/{id:int}")]
        public async Task<IActionResult> UnfollowUser(int id)
        {
            var me = CurrentUserId();
            var rows = await _context.Follows.Where(f => f.FollowerId == me && f.FollowedUserId == id).ToListAsync();
            if (rows.Count > 0)
            {
                _context.Follows.RemoveRange(rows);
                await _context.SaveChangesAsync();
            }
            return await GetUserStatus(id);
        }

        // ---------------- Negozi ----------------

        [HttpGet("stores/{id:int}")]
        public async Task<IActionResult> GetStoreStatus(int id)
        {
            var me = CurrentUserId();
            return Ok(new
            {
                following = await _context.Follows.AnyAsync(f => f.FollowerId == me && f.FollowedStoreId == id),
                followers = await _context.Follows.CountAsync(f => f.FollowedStoreId == id)
            });
        }

        [HttpPost("stores/{id:int}")]
        public async Task<IActionResult> FollowStore(int id)
        {
            var me = CurrentUserId();
            var store = await _context.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id && s.Status == StoreStatus.Verified);
            if (store == null) return NotFound(new { message = "Negozio non trovato" });
            if (store.OwnerId == me) return BadRequest(new { message = "Non puoi seguire il tuo negozio" });

            if (!await _context.Follows.AnyAsync(f => f.FollowerId == me && f.FollowedStoreId == id))
            {
                _context.Follows.Add(new Follow { FollowerId = me, FollowedStoreId = id });
                await _context.SaveChangesAsync();
                this.StoreActivity()?.Record(id, StoreActivityType.Follow, me);
            }
            return await GetStoreStatus(id);
        }

        [HttpDelete("stores/{id:int}")]
        public async Task<IActionResult> UnfollowStore(int id)
        {
            var me = CurrentUserId();
            var rows = await _context.Follows.Where(f => f.FollowerId == me && f.FollowedStoreId == id).ToListAsync();
            if (rows.Count > 0)
            {
                _context.Follows.RemoveRange(rows);
                await _context.SaveChangesAsync();
            }
            return await GetStoreStatus(id);
        }

        // ---------------- Elenchi ----------------

        /// <summary>Profili che seguo, con l'eventuale avviso "Sono qui" in corso.</summary>
        [HttpGet]
        public async Task<IActionResult> Following()
        {
            var me = CurrentUserId();
            var now = DateTime.UtcNow;
            var follows = await _context.Follows.AsNoTracking()
                .Where(f => f.FollowerId == me)
                .Select(f => new { f.FollowedUserId, f.FollowedStoreId, f.CreatedAt })
                .ToListAsync();

            var userIds = follows.Where(f => f.FollowedUserId.HasValue).Select(f => f.FollowedUserId!.Value).ToList();
            var storeIds = follows.Where(f => f.FollowedStoreId.HasValue).Select(f => f.FollowedStoreId!.Value).ToList();

            var live = await _context.LocationBroadcasts.AsNoTracking()
                .Where(b => b.EndedAt == null && b.EndsAt > now && (userIds.Contains(b.AuthorId) || (b.StoreId != null && storeIds.Contains(b.StoreId.Value))))
                .Select(b => new { b.Id, b.AuthorId, b.StoreId, b.PlaceName })
                .ToListAsync();

            var users = await _context.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id) && u.IsActive)
                .Select(u => new { u.Id, u.Username, u.AvatarUrl, City = u.Location != null ? u.Location.City : null })
                .ToListAsync();
            var stores = await _context.Stores.AsNoTracking()
                .Where(s => storeIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Name, s.City })
                .ToListAsync();

            return Ok(new
            {
                users = users.Select(u => new
                {
                    u.Id, u.Username, u.AvatarUrl, u.City,
                    live = live.Where(b => b.AuthorId == u.Id && b.StoreId == null).Select(b => new { b.Id, b.PlaceName }).FirstOrDefault()
                }),
                stores = stores.Select(s => new
                {
                    s.Id, s.Name, s.City,
                    live = live.Where(b => b.StoreId == s.Id).Select(b => new { b.Id, b.PlaceName }).FirstOrDefault()
                }),
                followers = await _context.Follows.CountAsync(f => f.FollowedUserId == me)
            });
        }

        private int CurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
        }
    }
}
