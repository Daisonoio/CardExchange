using CardExchange.Core.Rules;
using CardExchange.API.Authorization;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Sicurezza tra utenti: blocchi, segnalazioni e coda di moderazione.
    /// </summary>
    [ApiController]
    [Authorize]
    public class SafetyController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SafetyController> _logger;

        public SafetyController(ApplicationDbContext context, ILogger<SafetyController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ---------------- Blocchi ----------------

        [HttpPost("api/users/{id:int}/block")]
        public async Task<IActionResult> Block(int id)
        {
            var userId = CurrentUserId();
            if (userId == 0) return Unauthorized();
            if (id == userId) return BadRequest(new { message = "Non puoi bloccare te stesso" });
            if (!await _context.Users.AnyAsync(u => u.Id == id)) return NotFound(new { message = "Utente non trovato" });

            if (!await _context.UserBlocks.AnyAsync(b => b.BlockerId == userId && b.BlockedUserId == id))
            {
                _context.UserBlocks.Add(new UserBlock { BlockerId = userId, BlockedUserId = id });
                await _context.SaveChangesAsync();
                _logger.LogInformation("Utente {UserId} ha bloccato {BlockedId}", userId, id);
            }
            return Ok(new { message = "Utente bloccato. Non vi vedrete più nelle ricerche e non potrete scrivervi né proporvi scambi." });
        }

        [HttpDelete("api/users/{id:int}/block")]
        public async Task<IActionResult> Unblock(int id)
        {
            var userId = CurrentUserId();
            if (userId == 0) return Unauthorized();

            var block = await _context.UserBlocks.FirstOrDefaultAsync(b => b.BlockerId == userId && b.BlockedUserId == id);
            if (block != null)
            {
                _context.UserBlocks.Remove(block);
                await _context.SaveChangesAsync();
            }
            return Ok(new { message = "Utente sbloccato" });
        }

        /// <summary>Utenti bloccati dall'utente corrente.</summary>
        [HttpGet("api/users/blocked")]
        public async Task<IActionResult> GetBlocked()
        {
            var userId = CurrentUserId();
            if (userId == 0) return Unauthorized();

            // IgnoreQueryFilters: anche gli utenti sospesi devono restare visibili nell'elenco
            var blocked = await _context.UserBlocks
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(b => b.BlockerId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new { userId = b.BlockedUserId, username = b.BlockedUser.Username, blockedAt = b.CreatedAt })
                .ToListAsync();
            return Ok(blocked);
        }

        // ---------------- Segnalazioni ----------------

        public class CreateReportRequest
        {
            [Required]
            public int ReportedUserId { get; set; }

            [Required]
            [Range(1, 8, ErrorMessage = "Motivo non valido")]
            public int Reason { get; set; }

            [MaxLength(1000)]
            public string? Details { get; set; }

            public int? TradeOfferId { get; set; }
        }

        [HttpPost("api/reports")]
        public async Task<IActionResult> Report([FromBody] CreateReportRequest request)
        {
            var userId = CurrentUserId();
            if (userId == 0) return Unauthorized();
            if (request.ReportedUserId == userId) return BadRequest(new { message = "Non puoi segnalare te stesso" });
            if (!await _context.Users.AnyAsync(u => u.Id == request.ReportedUserId)) return NotFound(new { message = "Utente non trovato" });

            // Evita segnalazioni ripetute identiche mentre la prima è ancora aperta
            var duplicate = await _context.UserReports.AnyAsync(r =>
                r.ReporterId == userId && r.ReportedUserId == request.ReportedUserId && r.Status == ReportStatus.Open);
            if (duplicate)
                return Ok(new { message = "Hai già una segnalazione aperta per questo utente: la stiamo esaminando." });

            if (request.TradeOfferId.HasValue)
            {
                var related = await _context.TradeOffers.AnyAsync(o => o.Id == request.TradeOfferId &&
                    ((o.SenderId == userId && o.ReceiverId == request.ReportedUserId) ||
                     (o.ReceiverId == userId && o.SenderId == request.ReportedUserId)));
                if (!related) request.TradeOfferId = null;
            }

            _context.UserReports.Add(new UserReport
            {
                ReporterId = userId,
                ReportedUserId = request.ReportedUserId,
                Reason = (ReportReason)request.Reason,
                Details = request.Details?.Trim(),
                TradeOfferId = request.TradeOfferId
            });
            await _context.SaveChangesAsync();
            _logger.LogWarning("Segnalazione da {ReporterId} contro {ReportedId} ({Reason})", userId, request.ReportedUserId, (ReportReason)request.Reason);

            // Minori e proposte illegali: anche nella coda di sicurezza, con avviso immediato ai moderatori
            if ((ReportReason)request.Reason is ReportReason.MinorSafety or ReportReason.IllegalActivity
                && HttpContext.RequestServices.GetService<CardExchange.API.Services.Moderation.ChatSafety>() is { } safety)
            {
                var reporterBirth = await _context.Users.Where(u => u.Id == userId).Select(u => u.BirthDate).FirstOrDefaultAsync();
                await safety.FlagAsync(request.ReportedUserId, userId, null, SafetyFlagKind.UserReport,
                    $"[{(ReportReason)request.Reason}] {request.Details}".Trim(),
                    ChatSafetyRules.IsMinorOrUnknown(reporterBirth, DateOnly.FromDateTime(DateTime.UtcNow)), DateTime.UtcNow, blocked: false);
            }

            return Ok(new { message = "Segnalazione inviata. Grazie: un moderatore la esaminerà al più presto." });
        }

        // ---------------- Moderazione ----------------

        [HttpGet("api/moderation/reports")]
        [RequirePermission("ADMIN.REPORTS.MANAGE")]
        public async Task<IActionResult> GetReports([FromQuery] string status = "open", [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(1, page);
            // IgnoreQueryFilters: senza, le segnalazioni su utenti sospesi sparirebbero dalla coda
            var query = _context.UserReports.IgnoreQueryFilters().AsNoTracking();
            query = status.ToLowerInvariant() switch
            {
                "open" => query.Where(r => r.Status == ReportStatus.Open),
                "closed" => query.Where(r => r.Status != ReportStatus.Open),
                _ => query
            };

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    r.Id,
                    reason = r.Reason.ToString(),
                    r.Details,
                    status = r.Status.ToString(),
                    r.TradeOfferId,
                    r.CreatedAt,
                    r.ResolvedAt,
                    r.ResolutionNote,
                    r.UserSuspended,
                    reporter = new { id = r.ReporterId, username = r.Reporter.Username },
                    reportedUser = new
                    {
                        id = r.ReportedUserId,
                        username = r.ReportedUser.Username,
                        isActive = r.ReportedUser.IsActive,
                        reputationScore = r.ReportedUser.ReputationScore,
                        totalTradesCompleted = r.ReportedUser.TotalTradesCompleted,
                        openReports = _context.UserReports.Count(x => x.ReportedUserId == r.ReportedUserId && x.Status == ReportStatus.Open)
                    }
                })
                .ToListAsync();

            return Ok(new { total, page, pageSize, items });
        }

        public class ResolveReportRequest
        {
            /// <summary>"resolved" o "dismissed".</summary>
            [Required]
            public string Outcome { get; set; } = "resolved";

            [MaxLength(1000)]
            public string? Note { get; set; }

            /// <summary>Sospende l'account segnalato (richiede il permesso USERS.BAN).</summary>
            public bool SuspendUser { get; set; }
        }

        [HttpPost("api/moderation/reports/{id:int}/resolve")]
        [RequirePermission("ADMIN.REPORTS.MANAGE")]
        public async Task<IActionResult> Resolve(int id, [FromBody] ResolveReportRequest request)
        {
            var moderatorId = CurrentUserId();
            var report = await _context.UserReports.IgnoreQueryFilters().Include(r => r.ReportedUser).FirstOrDefaultAsync(r => r.Id == id);
            if (report == null) return NotFound(new { message = "Segnalazione non trovata" });
            if (report.Status != ReportStatus.Open) return BadRequest(new { message = "Segnalazione già chiusa" });

            var outcome = request.Outcome.ToLowerInvariant() switch
            {
                "resolved" => ReportStatus.Resolved,
                "dismissed" => ReportStatus.Dismissed,
                _ => (ReportStatus?)null
            };
            if (outcome == null) return BadRequest(new { message = "Esito non valido (resolved o dismissed)" });

            if (request.SuspendUser)
            {
                if (!User.HasClaim("Permission", "USERS.BAN"))
                    return StatusCode(403, new { message = "Serve il permesso USERS.BAN per sospendere un account" });
                if (outcome != ReportStatus.Resolved)
                    return BadRequest(new { message = "Si può sospendere solo risolvendo la segnalazione" });

                // L'utente non può più accedere né rinnovare la sessione
                report.ReportedUser.IsActive = false;
                report.ReportedUser.RefreshToken = null;
                report.ReportedUser.RefreshTokenExpiryTime = null;
                report.UserSuspended = true;
            }

            report.Status = outcome.Value;
            report.ResolvedById = moderatorId;
            report.ResolvedAt = DateTime.UtcNow;
            report.ResolutionNote = request.Note?.Trim();
            await _context.SaveChangesAsync();

            _logger.LogWarning("Segnalazione {ReportId} chiusa da {ModeratorId}: {Outcome}, sospensione={Suspended}",
                id, moderatorId, outcome, request.SuspendUser);
            return Ok(new { message = request.SuspendUser ? "Segnalazione risolta e account sospeso" : "Segnalazione chiusa" });
        }

        // ---------------- Sicurezza della chat ----------------

        public class ResolveSafetyFlagRequest
        {
            /// <summary>"dismiss" (falso allarme: la chat torna attiva), "confirm" (violazione confermata), "lift" (solo riattiva la chat).</summary>
            public string Action { get; set; } = "dismiss";
            /// <summary>Con "confirm": disattiva l'account (serve il permesso USERS.BAN).</summary>
            public bool DeactivateAccount { get; set; }
            [MaxLength(500)] public string? Note { get; set; }
        }

        /// <summary>Segnalazioni di sicurezza (automatiche e degli utenti per minori o proposte illegali), le più gravi prima.</summary>
        [HttpGet("api/moderation/safety-flags")]
        [RequirePermission("ADMIN.REPORTS.MANAGE")]
        public async Task<IActionResult> SafetyFlags([FromQuery] string status = "open")
        {
            var open = status != "all";
            var flags = await _context.SafetyFlags.AsNoTracking().Include(f => f.Sender)
                .Where(f => !open || f.Status == SafetyFlagStatus.Open)
                .OrderByDescending(f => f.InvolvesMinor).ThenByDescending(f => f.CreatedAt).Take(200).ToListAsync();
            var ids = flags.Where(f => f.RecipientId != null).Select(f => f.RecipientId!.Value).Distinct().ToList();
            var names = await _context.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
            var senders = flags.Select(f => f.SenderId).Distinct().ToList();
            var since = DateTime.UtcNow.AddDays(-30);
            var counts = await _context.SafetyFlags.AsNoTracking().Where(f => senders.Contains(f.SenderId) && f.CreatedAt >= since)
                .GroupBy(f => f.SenderId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);
            return Ok(flags.Select(f => new
            {
                f.Id, kind = f.Kind.ToString(), f.Blocked, f.InvolvesMinor, f.Excerpt, f.CreatedAt, status = f.Status.ToString(), f.ReviewNote,
                sender = new { id = f.SenderId, username = f.Sender.Username, f.Sender.IsActive, chatSuspendedUntil = f.Sender.MessagingSuspendedUntil, flags30Days = counts.GetValueOrDefault(f.SenderId) },
                recipient = f.RecipientId == null ? null : new { id = f.RecipientId, username = names.GetValueOrDefault(f.RecipientId.Value) },
                f.ConversationId
            }));
        }

        [HttpPost("api/moderation/safety-flags/{id:int}/resolve")]
        [RequirePermission("ADMIN.REPORTS.MANAGE")]
        public async Task<IActionResult> ResolveSafetyFlag(int id, [FromBody] ResolveSafetyFlagRequest request)
        {
            var flag = await _context.SafetyFlags.Include(f => f.Sender).FirstOrDefaultAsync(f => f.Id == id);
            if (flag == null) return NotFound(new { message = "Segnalazione non trovata" });
            var action = (request.Action ?? "").ToLowerInvariant();
            if (action is not ("dismiss" or "confirm" or "lift")) return BadRequest(new { message = "Azione non valida (dismiss, confirm o lift)" });

            if (action is "dismiss" or "lift") flag.Sender.MessagingSuspendedUntil = null;
            if (action == "confirm" && request.DeactivateAccount)
            {
                if (!User.HasClaim("Permission", "USERS.BAN"))
                    return StatusCode(403, new { message = "Serve il permesso USERS.BAN per sospendere un account" });
                flag.Sender.IsActive = false;
                flag.Sender.RefreshToken = null;
                flag.Sender.RefreshTokenExpiryTime = null;
            }
            if (action != "lift")
            {
                flag.Status = action == "dismiss" ? SafetyFlagStatus.Dismissed : SafetyFlagStatus.ActionTaken;
                flag.ReviewedById = CurrentUserId();
                flag.ReviewedAt = DateTime.UtcNow;
                flag.ReviewNote = request.Note?.Trim();
            }
            await _context.SaveChangesAsync();
            _logger.LogWarning("Segnalazione di sicurezza {FlagId}: {Action} da {ModeratorId}", id, action, CurrentUserId());
            return Ok(new { message = action == "dismiss" ? "Segnalazione archiviata, chat riattivata" : action == "lift" ? "Chat riattivata" : request.DeactivateAccount ? "Violazione confermata e account disattivato" : "Violazione confermata" });
        }

        private int CurrentUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
    }
}
