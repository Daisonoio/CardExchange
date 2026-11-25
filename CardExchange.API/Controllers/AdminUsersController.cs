using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Gestione degli utenti per gli amministratori: ricerca, scheda, tipo di profilo, account e chat.
    /// Ogni modifica finisce nel registro (chi, quando, cosa, perché). La data di nascita non si mostra:
    /// basta la fascia d'età.
    /// </summary>
    [ApiController]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Route("api/admin/users")]
    public class AdminUsersController : ControllerBase
    {
        public const int MaxPageSize = 50;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminUsersController> _logger;

        public AdminUsersController(ApplicationDbContext context, ILogger<AdminUsersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public record ProfileTypeRequest(ProfileType ProfileType, [property: MaxLength(500)] string? Note = null);
        public record NoteRequest([property: MaxLength(500)] string? Note = null);
        public record ActiveRequest(bool Active, [property: MaxLength(500)] string? Note = null);

        private int Me => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        public static string AgeGroup(DateOnly? birthDate, DateOnly today) =>
            birthDate is not { } bd ? "unknown" : AgeRules.IsMinor(bd, today) ? "minor" : "adult";

        private void Audit(int targetUserId, string action, string? detail, string? note) =>
            _context.AdminAuditEntries.Add(new AdminAuditEntry
            {
                AdminId = Me, TargetUserId = targetUserId, Action = action, Detail = detail,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(), CreatedAt = DateTime.UtcNow
            });

        /// <summary>Ricerca per nome utente o email, con filtri per tipo di profilo e stato.</summary>
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] ProfileType? profileType, [FromQuery] string? status,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
            var now = DateTime.UtcNow;
            var query = _context.Users.AsNoTracking().IgnoreQueryFilters().Where(u => !u.Email.EndsWith("@removed.local"));
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                query = query.Where(u => u.Username.ToLower().Contains(term) || u.Email.ToLower().Contains(term));
            }
            if (profileType.HasValue) query = query.Where(u => u.ProfileType == profileType.Value);
            query = status switch
            {
                "active" => query.Where(u => u.IsActive),
                "inactive" => query.Where(u => !u.IsActive),
                "chatSuspended" => query.Where(u => u.MessagingSuspendedUntil > now),
                _ => query
            };
            var total = await query.CountAsync();
            var users = await query.OrderByDescending(u => u.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(u => new
                {
                    u.Id, u.Username, u.Email, city = u.Location != null ? u.Location.City : null, profileType = u.ProfileType.ToString(),
                    u.CreatedAt, u.IsActive, chatSuspended = u.MessagingSuspendedUntil > now
                }).ToListAsync();
            var ids = users.Select(u => u.Id).ToList();
            var stores = (await _context.Stores.AsNoTracking().Where(s => ids.Contains(s.OwnerId)).Select(s => new { s.OwnerId, s.Name }).ToListAsync())
                .GroupBy(s => s.OwnerId).ToDictionary(g => g.Key, g => g.First().Name);
            return Ok(new
            {
                total, page, pageSize,
                items = users.Select(u => new { u.Id, u.Username, u.Email, u.city, u.profileType, u.CreatedAt, u.IsActive, u.chatSuspended, storeName = stores.GetValueOrDefault(u.Id) })
            });
        }

        /// <summary>Scheda dell'utente: profilo, fascia d'età, sicurezza, negozi, registro delle modifiche.</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var user = await _context.Users.AsNoTracking().IgnoreQueryFilters().Include(u => u.Location).FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound(new { message = "Utente non trovato" });
            var now = DateTime.UtcNow;
            var since = now.AddDays(-30);
            var stores = await _context.Stores.AsNoTracking().Where(s => s.OwnerId == id)
                .Select(s => new { s.Id, s.Name, s.City, status = s.Status.ToString() }).ToListAsync();
            var flags30 = await _context.SafetyFlags.AsNoTracking().CountAsync(f => f.SenderId == id && f.CreatedAt >= since);
            var openFlags = await _context.SafetyFlags.AsNoTracking().CountAsync(f => f.SenderId == id && f.Status == SafetyFlagStatus.Open);
            var audit = await _context.AdminAuditEntries.AsNoTracking().Where(a => a.TargetUserId == id)
                .OrderByDescending(a => a.CreatedAt).Take(30).ToListAsync();
            var adminIds = audit.Where(a => a.AdminId != null).Select(a => a.AdminId!.Value).Distinct().ToList();
            var adminNames = await _context.Users.AsNoTracking().IgnoreQueryFilters().Where(u => adminIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Username);

            return Ok(new
            {
                user.Id, user.Username, user.Email, city = user.Location?.City, user.CreatedAt, user.LastLoginAt,
                profileType = user.ProfileType.ToString(),
                ageGroup = AgeGroup(user.BirthDate, DateOnly.FromDateTime(now)),
                user.IsActive,
                chatSuspendedUntil = user.MessagingSuspendedUntil > now ? user.MessagingSuspendedUntil : null,
                safety = new { flags30Days = flags30, openFlags },
                activity = new
                {
                    cards = await _context.Cards.CountAsync(c => c.UserId == id),
                    tradesCompleted = user.TotalTradesCompleted,
                    reputation = user.ReputationScore
                },
                stores,
                audit = audit.Select(a => new
                {
                    a.Id, a.Action, a.Detail, a.Note, a.CreatedAt,
                    admin = a.AdminId == null ? null : adminNames.GetValueOrDefault(a.AdminId.Value)
                })
            });
        }

        /// <summary>Cambia il tipo di profilo (Collezionista, Venditore, Negozio, Organizzatore): decide le funzioni disponibili.</summary>
        [HttpPut("{id:int}/profile-type")]
        public async Task<IActionResult> SetProfileType(int id, [FromBody] ProfileTypeRequest request)
        {
            if (!Enum.IsDefined(request.ProfileType)) return BadRequest(new { message = "Tipo di profilo non valido" });
            var user = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound(new { message = "Utente non trovato" });
            if (user.ProfileType == request.ProfileType) return Ok(new { id, profileType = user.ProfileType.ToString(), message = "Tipo di profilo aggiornato" });
            var before = user.ProfileType;
            user.ProfileType = request.ProfileType;
            Audit(id, "ProfileType", $"{before} → {request.ProfileType}", request.Note);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Profilo dell'utente {UserId} cambiato da {Before} a {Profile} da {AdminId}", id, before, request.ProfileType, Me);
            return Ok(new { id, profileType = user.ProfileType.ToString(), message = "Tipo di profilo aggiornato" });
        }

        /// <summary>Riattiva la chat sospesa dalle segnalazioni di sicurezza.</summary>
        [HttpPost("{id:int}/chat/lift")]
        public async Task<IActionResult> LiftChat(int id, [FromBody] NoteRequest? request)
        {
            var user = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound(new { message = "Utente non trovato" });
            if (user.MessagingSuspendedUntil == null || user.MessagingSuspendedUntil <= DateTime.UtcNow)
                return BadRequest(new { message = "La chat di questo utente non è sospesa" });
            user.MessagingSuspendedUntil = null;
            Audit(id, "ChatLifted", null, request?.Note);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Chat riattivata" });
        }

        /// <summary>Disattiva o riattiva l'account (serve il permesso USERS.BAN).</summary>
        [HttpPost("{id:int}/active")]
        public async Task<IActionResult> SetActive(int id, [FromBody] ActiveRequest request)
        {
            if (!User.HasClaim("Permission", "USERS.BAN"))
                return StatusCode(403, new { message = "Serve il permesso USERS.BAN per sospendere un account" });
            if (id == Me) return BadRequest(new { message = "Non puoi disattivare il tuo account da qui" });
            var user = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound(new { message = "Utente non trovato" });
            if (user.IsActive == request.Active) return Ok(new { message = request.Active ? "Account riattivato" : "Account disattivato" });
            user.IsActive = request.Active;
            if (!request.Active)
            {
                // Esce subito da tutte le sessioni: non può più rinnovare l'accesso
                user.RefreshToken = null;
                user.RefreshTokenExpiryTime = null;
            }
            Audit(id, request.Active ? "Reactivate" : "Deactivate", null, request.Note);
            await _context.SaveChangesAsync();
            _logger.LogWarning("Account {UserId} {State} da {AdminId}", id, request.Active ? "riattivato" : "disattivato", Me);
            return Ok(new { message = request.Active ? "Account riattivato" : "Account disattivato" });
        }
    }
}
