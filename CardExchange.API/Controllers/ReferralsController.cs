using CardExchange.API.Configuration;
using CardExchange.API.Services;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace CardExchange.API.Controllers
{
    /// <summary>Inviti: codice personale, link da condividere, amici iscritti.</summary>
    [ApiController]
    [Route("api/referrals")]
    [Authorize]
    public class ReferralsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailSettings _emailSettings;

        public ReferralsController(ApplicationDbContext context, IOptions<EmailSettings> emailSettings)
        {
            _context = context;
            _emailSettings = emailSettings.Value;
        }

        /// <summary>Codice e link di invito dell'utente (generati al primo uso) e amici che si sono iscritti.</summary>
        [HttpGet("me")]
        public async Task<IActionResult> Mine()
        {
            var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return Unauthorized();

            if (user.ReferralCode == null)
            {
                // Collisioni improbabili (31^8 combinazioni), ma si riprova comunque
                for (int attempt = 0; attempt < 5 && user.ReferralCode == null; attempt++)
                {
                    var candidate = ReferralCodes.Generate();
                    if (!await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.ReferralCode == candidate))
                        user.ReferralCode = candidate;
                }
                await _context.SaveChangesAsync();
            }

            var invited = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(u => u.ReferredByUserId == userId)
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new { u.Username, joinedAt = u.CreatedAt, completedTrades = u.TotalTradesCompleted })
                .ToListAsync();

            return Ok(new
            {
                code = user.ReferralCode,
                link = $"{_emailSettings.FrontendBaseUrl.TrimEnd('/')}/register?ref={user.ReferralCode}",
                invitedCount = invited.Count,
                invited = invited.Take(50)
            });
        }

        /// <summary>Chi ha invitato: usato dalla pagina di registrazione per mostrare "Ti ha invitato @nome".</summary>
        [HttpGet("{code}")]
        [AllowAnonymous]
        public async Task<IActionResult> Lookup(string code)
        {
            var normalized = ReferralCodes.Normalize(code);
            if (normalized == null) return NotFound();
            var username = await _context.Users.AsNoTracking()
                .Where(u => u.ReferralCode == normalized)
                .Select(u => u.Username)
                .FirstOrDefaultAsync();
            return username == null ? NotFound() : Ok(new { username });
        }
    }
}
