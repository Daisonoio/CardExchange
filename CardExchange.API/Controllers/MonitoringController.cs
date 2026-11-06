using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using CardExchange.API.Services.Monitoring;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Controllers
{
    /// <summary>Monitoraggio: errori segnalati dall'app e pagina per gli amministratori.</summary>
    [ApiController]
    public class MonitoringController : ControllerBase
    {
        private const int MaxClientReportsPerMinute = 30;
        private static readonly ConcurrentDictionary<string, (DateTime Window, int Count)> RateByIp = new();

        private readonly ApplicationDbContext _context;
        private readonly IErrorTracker _tracker;

        public MonitoringController(ApplicationDbContext context, IErrorTracker tracker)
        {
            _context = context;
            _tracker = tracker;
        }

        public class ClientErrorRequest
        {
            [Required, MaxLength(500)] public string Message { get; set; } = string.Empty;
            [MaxLength(4000)] public string? Stack { get; set; }
            [MaxLength(300)] public string? Path { get; set; }
            /// <summary>Tipo: "error", "unhandledrejection", "react".</summary>
            [MaxLength(40)] public string? Kind { get; set; }
        }

        /// <summary>Errore avvenuto nel browser. Anche senza accesso (es. errore in registrazione), con un limite per IP.</summary>
        [HttpPost("api/monitoring/client-errors")]
        [AllowAnonymous]
        public IActionResult ReportClientError([FromBody] ClientErrorRequest request)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var now = DateTime.UtcNow;
            var entry = RateByIp.AddOrUpdate(ip, _ => (now, 1), (_, e) => now - e.Window > TimeSpan.FromMinutes(1) ? (now, 1) : (e.Window, e.Count + 1));
            if (entry.Count > MaxClientReportsPerMinute) return StatusCode(429);

            _tracker.Report(new ErrorReport(ErrorSource.Client, $"client:{request.Kind ?? "error"}", request.Message, request.Stack, request.Path, now));
            return Accepted();
        }

        /// <summary>Errori aperti e stato dei cataloghi (solo amministratori).</summary>
        [HttpGet("api/admin/monitoring")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Overview([FromServices] CardExchange.API.Services.Catalog.PokemonCatalogMirror mirror, [FromQuery] bool includeResolved = false)
        {
            var query = _context.ErrorGroups.AsNoTracking();
            if (!includeResolved) query = query.Where(e => !e.IsResolved);
            var errors = await query
                .OrderByDescending(e => e.LastSeenAt)
                .Take(200)
                .Select(e => new
                {
                    e.Id,
                    source = e.Source.ToString(),
                    e.Category,
                    e.Message,
                    e.Detail,
                    e.Path,
                    e.Count,
                    e.FirstSeenAt,
                    e.LastSeenAt,
                    e.IsResolved
                })
                .ToListAsync();
            var since = DateTime.UtcNow.AddHours(-24);
            var catalogs = await _context.CatalogStatuses.AsNoTracking().OrderBy(c => c.Key).ToListAsync();
            return Ok(new
            {
                errors,
                openGroups = await _context.ErrorGroups.CountAsync(e => !e.IsResolved),
                seenLast24h = await _context.ErrorGroups.Where(e => e.LastSeenAt >= since).CountAsync(),
                catalogs = catalogs.Select(c => new
                {
                    c.Key,
                    c.LastCheckAt,
                    c.LastOkAt,
                    c.LastFailAt,
                    c.ConsecutiveFailures,
                    c.LastError,
                    isUp = c.ConsecutiveFailures == 0
                }),
                // Copia locale del gioco di punta: carte disponibili anche se il catalogo esterno è fermo
                localCopy = await mirror.StatusAsync()
            });
        }

        /// <summary>Aggiorna subito la copia locale del catalogo Pokémon (di solito avviene ogni notte).</summary>
        [HttpPost("api/admin/monitoring/catalog-mirror/run")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> RunMirror([FromServices] CardExchange.API.Services.Catalog.PokemonCatalogMirror mirror, CancellationToken ct)
        {
            var sets = await mirror.RunAsync(DateTime.UtcNow, ct);
            return Ok(new { sets, status = await mirror.StatusAsync(ct) });
        }

        /// <summary>Segna un errore come risolto: se si ripresenta, gli amministratori vengono avvisati di nuovo.</summary>
        [HttpPost("api/admin/monitoring/errors/{id:int}/resolve")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Resolve(int id)
        {
            var group = await _context.ErrorGroups.FirstOrDefaultAsync(e => e.Id == id);
            if (group == null) return NotFound(new { message = "Errore non trovato" });
            group.IsResolved = true;
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
