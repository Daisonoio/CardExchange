using System.Security.Claims;
using CardExchange.API.Services.Billing;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Controllers
{
    /// <summary>Spazi sponsorizzati (Home, Esplora, negozi) e sponsor di marca. Mai in chat, scambi o appuntamenti.</summary>
    [ApiController]
    [Authorize]
    public class SponsorsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly SponsoredContent _content;

        public SponsorsController(ApplicationDbContext context, SponsoredContent content)
        {
            _context = context;
            _content = content;
        }

        private int Me => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        // ---------------------------------------------------------------- spazi

        /// <summary>Lo spazio da mostrare in quel punto, già filtrato per zona, gioco, età e limiti di frequenza (204 se non c'è).</summary>
        [HttpGet("api/slots")]
        public async Task<IActionResult> Slot([FromQuery] SlotPlacement placement, [FromQuery] decimal? lat, [FromQuery] decimal? lon, [FromQuery] int? gameId)
        {
            if (!Enum.IsDefined(placement)) return BadRequest(new { message = "Posizione non valida" });
            var view = await _content.PickAsync(Me, placement, lat, lon, gameId, DateTime.UtcNow);
            if (view == null) return NoContent();
            var s = view.Slot;
            return Ok(new
            {
                id = s.Id, title = s.Title, body = s.Body, imageUrl = s.ImageUrl, targetUrl = s.TargetUrl,
                sponsor = view.SponsorName, brand = view.Why.Brand,
                why = new { zone = view.Why.Zone, city = view.Why.City, radiusKm = view.Why.RadiusKm, game = view.Why.Game }
            });
        }

        [HttpPost("api/slots/{id:int}/click")]
        public async Task<IActionResult> SlotClick(int id)
        {
            await _content.ClickAsync(id);
            return NoContent();
        }

        // ---------------------------------------------------------------- sponsor (pubblico)

        [HttpGet("api/sponsors/{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var s = await _context.Sponsors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            if (s == null) return NotFound(new { message = "Sponsor non trovato" });
            return Ok(new { s.Id, s.Name, s.LogoUrl, s.Description, hasWebsite = !string.IsNullOrEmpty(s.WebsiteUrl) });
        }

        /// <summary>Verso il sito dello sponsor: si conta solo il totale dei clic, senza sapere chi ha cliccato.</summary>
        [HttpGet("api/sponsors/{id:int}/go")]
        [AllowAnonymous]
        public async Task<IActionResult> Go(int id)
        {
            var url = await _context.Sponsors.AsNoTracking().Where(x => x.Id == id && x.IsActive).Select(x => x.WebsiteUrl).FirstOrDefaultAsync();
            if (string.IsNullOrEmpty(url)) return NotFound();
            await _context.Sponsors.Where(x => x.Id == id).ExecuteUpdateAsync(u => u.SetProperty(x => x.WebsiteClicks, x => x.WebsiteClicks + 1));
            return Redirect(url);
        }

        // ---------------------------------------------------------------- amministrazione

        public record SponsorRequest(string Name, string? LogoUrl, string? WebsiteUrl, string? ContactEmail, string? Description, bool IsActive = true);
        public record SponsorshipRequest(int SponsorId, SponsorshipScope Scope, string? ScopeRef, SponsorshipPlacement Placement,
            DateTime StartsAt, DateTime EndsAt, SlotPlacement? SlotPlacement, string? SlotTitle, string? SlotBody, int? GameId);

        private static bool HttpsOrEmpty(string? url) =>
            string.IsNullOrWhiteSpace(url) || (Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && url.Length <= 300);

        [HttpGet("api/admin/sponsors")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> AdminList()
        {
            var sponsors = await _context.Sponsors.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
            var ships = await _context.Sponsorships.AsNoTracking().OrderByDescending(s => s.StartsAt).ToListAsync();
            var slotStats = await _context.SponsoredSlots.AsNoTracking().Where(s => s.SponsorshipId != null)
                .ToDictionaryAsync(s => s.SponsorshipId!.Value, s => new { s.Impressions, s.Clicks });
            return Ok(sponsors.Select(s => new
            {
                s.Id, s.Name, s.LogoUrl, s.WebsiteUrl, s.ContactEmail, s.Description, s.IsActive, s.WebsiteClicks,
                sponsorships = ships.Where(x => x.SponsorId == s.Id).Select(x => new
                {
                    x.Id, scope = x.Scope.ToString(), x.ScopeRef, placement = x.Placement.ToString(), x.StartsAt, x.EndsAt,
                    slotPlacement = x.SlotPlacement?.ToString(), x.SlotTitle, x.SlotBody, x.GameId,
                    impressions = slotStats.GetValueOrDefault(x.Id)?.Impressions ?? 0, clicks = slotStats.GetValueOrDefault(x.Id)?.Clicks ?? 0
                })
            }));
        }

        [HttpPost("api/admin/sponsors")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public Task<IActionResult> AdminCreate([FromBody] SponsorRequest r) => SaveSponsor(null, r);

        [HttpPut("api/admin/sponsors/{id:int}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public Task<IActionResult> AdminUpdate(int id, [FromBody] SponsorRequest r) => SaveSponsor(id, r);

        private async Task<IActionResult> SaveSponsor(int? id, SponsorRequest r)
        {
            var name = (r.Name ?? "").Trim();
            if (name.Length is < 2 or > 80) return BadRequest(new { message = "Il nome dello sponsor deve avere da 2 a 80 caratteri" });
            if (!HttpsOrEmpty(r.LogoUrl) || !HttpsOrEmpty(r.WebsiteUrl)) return BadRequest(new { message = "Logo e sito devono essere indirizzi https" });
            Sponsor? s;
            if (id == null) { s = new Sponsor(); _context.Sponsors.Add(s); }
            else if ((s = await _context.Sponsors.FirstOrDefaultAsync(x => x.Id == id)) == null) return NotFound(new { message = "Sponsor non trovato" });
            s.Name = name;
            s.LogoUrl = string.IsNullOrWhiteSpace(r.LogoUrl) ? null : r.LogoUrl.Trim();
            s.WebsiteUrl = string.IsNullOrWhiteSpace(r.WebsiteUrl) ? null : r.WebsiteUrl.Trim();
            s.ContactEmail = r.ContactEmail?.Trim();
            s.Description = r.Description?.Trim();
            s.IsActive = r.IsActive;
            await _context.SaveChangesAsync();
            return Ok(new { s.Id, message = "Sponsor salvato" });
        }

        [HttpPost("api/admin/sponsorships")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public Task<IActionResult> AdminCreateShip([FromBody] SponsorshipRequest r) => SaveShip(null, r);

        [HttpPut("api/admin/sponsorships/{id:int}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public Task<IActionResult> AdminUpdateShip(int id, [FromBody] SponsorshipRequest r) => SaveShip(id, r);

        private async Task<IActionResult> SaveShip(int? id, SponsorshipRequest r)
        {
            if (!await _context.Sponsors.AnyAsync(s => s.Id == r.SponsorId)) return BadRequest(new { message = "Sponsor non trovato" });
            if (r.EndsAt <= r.StartsAt) return BadRequest(new { message = "La fine deve essere dopo l'inizio" });
            if (r.Scope == SponsorshipScope.Event && !int.TryParse(r.ScopeRef, out _)) return BadRequest(new { message = "Indica l'evento sponsorizzato" });
            if (r.Scope == SponsorshipScope.CityTradeNights && string.IsNullOrWhiteSpace(r.ScopeRef)) return BadRequest(new { message = "Indica la città delle serate di scambio" });
            if (r.Placement == SponsorshipPlacement.Slot && (r.SlotPlacement == null || string.IsNullOrWhiteSpace(r.SlotTitle)))
                return BadRequest(new { message = "Per lo spazio servono posizione e titolo" });
            Sponsorship? s;
            if (id == null) { s = new Sponsorship(); _context.Sponsorships.Add(s); }
            else if ((s = await _context.Sponsorships.FirstOrDefaultAsync(x => x.Id == id)) == null) return NotFound(new { message = "Sponsorizzazione non trovata" });
            s.SponsorId = r.SponsorId; s.Scope = r.Scope; s.ScopeRef = r.ScopeRef?.Trim(); s.Placement = r.Placement;
            s.StartsAt = DateTime.SpecifyKind(r.StartsAt, DateTimeKind.Utc); s.EndsAt = DateTime.SpecifyKind(r.EndsAt, DateTimeKind.Utc);
            s.SlotPlacement = r.SlotPlacement; s.SlotTitle = r.SlotTitle?.Trim(); s.SlotBody = r.SlotBody?.Trim(); s.GameId = r.GameId;
            await _context.SaveChangesAsync();
            await _content.SyncBrandSlotAsync(s);
            return Ok(new { s.Id, message = "Sponsorizzazione salvata" });
        }

        [HttpDelete("api/admin/sponsorships/{id:int}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> AdminDeleteShip(int id)
        {
            var s = await _context.Sponsorships.FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound(new { message = "Sponsorizzazione non trovata" });
            await _context.SponsoredSlots.Where(x => x.SponsorshipId == id).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsActive, false));
            _context.Sponsorships.Remove(s);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Sponsorizzazione eliminata" });
        }
    }
}
