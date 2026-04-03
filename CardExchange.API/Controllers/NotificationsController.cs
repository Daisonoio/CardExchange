using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CardExchange.API.Controllers
{
    public class UpdatePreferenceItem
    {
        public int TypeId { get; set; }
        public bool IsEnabled { get; set; }
    }

    public class UpdatePreferencesRequest
    {
        public List<UpdatePreferenceItem> Preferences { get; set; } = new();
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public NotificationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Ottiene le notifiche dell'utente corrente
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetNotifications(
            [FromQuery] bool unreadOnly = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var query = _context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId);

            if (unreadOnly)
                query = query.Where(n => !n.IsRead);

            var totalCount = await query.CountAsync();
            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new
                {
                    n.Id,
                    type = n.Type.ToString(),
                    n.Title,
                    n.Body,
                    n.IsRead,
                    n.ReadAt,
                    n.ReferenceId,
                    n.ReferenceType,
                    createdAt = n.CreatedAt
                })
                .ToListAsync();

            return Ok(new { totalCount, page, pageSize, notifications });
        }

        /// <summary>
        /// Segna una notifica come letta
        /// </summary>
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

            if (notification == null)
                return NotFound();

            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Notifica segnata come letta" });
        }

        /// <summary>
        /// Segna tutte le notifiche come lette
        /// </summary>
        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var unread = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            var now = DateTime.UtcNow;
            foreach (var n in unread)
            {
                n.IsRead = true;
                n.ReadAt = now;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = $"{unread.Count} notifiche segnate come lette" });
        }

        /// <summary>
        /// Conteggio notifiche non lette
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var count = await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);

            return Ok(new { unreadCount = count });
        }

        /// <summary>
        /// Elimina una notifica
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

            if (notification == null)
                return NotFound();

            notification.IsDeleted = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Notifica eliminata" });
        }

        /// <summary>
        /// Ottiene le preferenze di notifica dell'utente corrente
        /// </summary>
        [HttpGet("preferences")]
        public async Task<IActionResult> GetPreferences()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var saved = await _context.NotificationPreferences
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .ToListAsync();

            var allTypes = Enum.GetValues<NotificationType>();
            var preferences = allTypes.Select(type =>
            {
                var pref = saved.FirstOrDefault(p => p.Type == type);
                return new
                {
                    type = type.ToString(),
                    typeId = (int)type,
                    isEnabled = pref?.IsEnabled ?? true
                };
            });

            return Ok(new { preferences });
        }

        /// <summary>
        /// Aggiorna le preferenze di notifica dell'utente
        /// </summary>
        [HttpPut("preferences")]
        public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            if (request.Preferences == null || request.Preferences.Count == 0)
                return BadRequest(new { message = "Nessuna preferenza fornita" });

            foreach (var pref in request.Preferences)
            {
                if (!Enum.IsDefined(typeof(NotificationType), pref.TypeId))
                    continue;

                var type = (NotificationType)pref.TypeId;
                var existing = await _context.NotificationPreferences
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.Type == type);

                if (existing != null)
                {
                    existing.IsEnabled = pref.IsEnabled;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.NotificationPreferences.Add(new NotificationPreference
                    {
                        UserId = userId,
                        Type = type,
                        IsEnabled = pref.IsEnabled
                    });
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Preferenze aggiornate" });
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
        }
    }
}
