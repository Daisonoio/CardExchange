using System.Security.Claims;
using CardExchange.API.Configuration;
using CardExchange.API.Services.Billing;
using CardExchange.API.Services.Events;
using CardExchange.API.Services.Moderation;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Pacchetto per organizzatori di fiere e mercatini: acquisto, espositori invitati con un link,
    /// elenco pubblico degli espositori (chi è "live", chi ha le carte che cerchi), piantina e risultati.
    /// </summary>
    [ApiController]
    [Authorize]
    public class EventPackagesController : ControllerBase
    {
        public const int MaxExhibitors = 300;
        private static readonly Dictionary<EventPackageTier, string> Products = new()
        {
            [EventPackageTier.Base] = "event-base", [EventPackageTier.Plus] = "event-plus", [EventPackageTier.Region] = "event-region"
        };

        private readonly ApplicationDbContext _context;

        public EventPackagesController(ApplicationDbContext context) => _context = context;

        private int Me => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        public record BuyRequest(EventPackageTier Tier);
        public record ExhibitorRequest(string Name, string BoothCode, string? Description);
        public record FloorPlanRequest(string? Url);
        public record AcceptRequest(int? StoreId);

        private async Task<(Event? Event, IActionResult? Error)> OwnEventAsync(int eventId)
        {
            var ev = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);
            if (ev == null) return (null, NotFound(new { message = "Evento non trovato" }));
            if (ev.OrganizerId != Me) return (null, Forbid());
            return (ev, null);
        }

        /// <summary>Listino dei pacchetti e stato del pacchetto dell'evento.</summary>
        [HttpGet("api/events/{id:int}/package")]
        public async Task<IActionResult> Get(int id, [FromServices] IStripeBilling stripe, [FromServices] IConfiguration configuration)
        {
            var (ev, error) = await OwnEventAsync(id);
            if (ev == null) return error!;
            var package = await _context.EventPackages.AsNoTracking().FirstOrDefaultAsync(p => p.EventId == id);
            var tiers = Products.Select(kv => new { tier = kv.Key.ToString(), priceEur = PurchaseCatalog.Find(kv.Value)!.PriceEur, featuredRadiusKm = EventPackages.FeaturedRadiusKm(kv.Key) });
            object? results = null;
            if (package != null)
            {
                var exhibitors = await _context.EventExhibitors.AsNoTracking().Where(x => x.EventId == id).ToListAsync();
                var accepted = exhibitors.Where(x => x.AcceptedAt != null).ToList();
                var broadcasts = await _context.LocationBroadcasts.AsNoTracking().CountAsync(b => b.EventId == id && b.ExhibitorId != null);
                var userIds = accepted.Where(x => x.UserId != null).Select(x => x.UserId!.Value).ToList();
                var storeIds = accepted.Where(x => x.StoreId != null).Select(x => x.StoreId!.Value).ToList();
                var from = ev.StartDate.AddDays(-1);
                var to = (ev.EndDate ?? ev.StartDate).AddDays(1);
                var newFollowers = await _context.Follows.AsNoTracking().CountAsync(f => f.CreatedAt >= from && f.CreatedAt <= to
                    && ((f.FollowedUserId != null && userIds.Contains(f.FollowedUserId.Value)) || (f.FollowedStoreId != null && storeIds.Contains(f.FollowedStoreId.Value))));
                results = new { pageViews = package.PageViews, exhibitors = exhibitors.Count, accepted = accepted.Count, broadcasts, newFollowers };
            }
            return Ok(new
            {
                active = package != null,
                tier = package?.Tier.ToString(),
                floorPlanImageUrl = package?.FloorPlanImageUrl,
                canBuyByCard = configuration.GetValue("Purchases:Enabled", false) && stripe.IsEnabled,
                tiers,
                results
            });
        }

        /// <summary>Acquisto con la carta (Stripe): il pacchetto si attiva dalla conferma del pagamento.</summary>
        [HttpPost("api/events/{id:int}/package")]
        public async Task<IActionResult> Buy(int id, [FromBody] BuyRequest request, [FromServices] PurchaseService purchases,
            [FromServices] IStripeBilling stripe, [FromServices] IConfiguration configuration, [FromServices] IOptions<EmailSettings> email)
        {
            var (ev, error) = await OwnEventAsync(id);
            if (ev == null) return error!;
            if (!Products.TryGetValue(request.Tier, out var code)) return BadRequest(new { message = "Pacchetto non valido" });
            if (ev.Status != EventStatus.Published || (ev.EndDate ?? ev.StartDate) < DateTime.UtcNow) return BadRequest(new { message = "L'evento deve essere pubblicato e non ancora concluso" });
            var current = await _context.EventPackages.AsNoTracking().FirstOrDefaultAsync(p => p.EventId == id);
            if (current != null && current.Tier >= request.Tier) return BadRequest(new { message = "Il pacchetto è già attivo" });
            if (!configuration.GetValue("Purchases:Enabled", false) || !stripe.IsEnabled)
                return BadRequest(new { message = "Il pagamento con carta non è ancora disponibile: scrivici per il bonifico" });
            var user = await _context.Users.FirstAsync(u => u.Id == Me);
            var purchase = await purchases.CreatePendingAsync(Me, code, id);
            var baseUrl = email.Value.FrontendBaseUrl.TrimEnd('/');
            try
            {
                var url = await stripe.CreatePaymentCheckoutAsync(user, purchase, PurchaseCatalog.Find(code)!.Name,
                    $"{baseUrl}/events/{id}?package=success", $"{baseUrl}/events/{id}?package=cancelled");
                return Ok(new { url });
            }
            catch (Stripe.StripeException)
            {
                await purchases.MarkExpiredAsync(purchase.Id);
                return StatusCode(502, new { message = "Il servizio di pagamento non risponde, riprova tra poco." });
            }
        }

        /// <summary>Attivazione a mano (bonifico, accordi diretti): solo amministratori.</summary>
        [HttpPost("api/admin/events/{id:int}/package")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> AdminActivate(int id, [FromBody] BuyRequest request)
        {
            var package = await EventPackages.ActivateAsync(_context, id, request.Tier, null, Me, DateTime.UtcNow);
            return package == null ? NotFound(new { message = "Evento non trovato" }) : Ok(new { message = "Pacchetto attivato", tier = package.Tier.ToString() });
        }

        [HttpPut("api/events/{id:int}/package/floorplan")]
        public async Task<IActionResult> FloorPlan(int id, [FromBody] FloorPlanRequest request)
        {
            var (ev, error) = await OwnEventAsync(id);
            if (ev == null) return error!;
            var package = await _context.EventPackages.FirstOrDefaultAsync(p => p.EventId == id);
            if (package == null || !EventPackages.HasFloorPlan(package.Tier)) return StatusCode(402, new { message = "La piantina è inclusa nei pacchetti Plus e Regione" });
            var url = string.IsNullOrWhiteSpace(request.Url) ? null : request.Url.Trim();
            if (url != null && (url.Length > 300 || !Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps))
                return BadRequest(new { message = "La piantina deve essere un indirizzo https" });
            package.FloorPlanImageUrl = url;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Piantina salvata" });
        }

        // ---------------------------------------------------------------- espositori (organizzatore)

        [HttpGet("api/events/{id:int}/exhibitors/manage")]
        public async Task<IActionResult> Manage(int id)
        {
            var (ev, error) = await OwnEventAsync(id);
            if (ev == null) return error!;
            var list = await _context.EventExhibitors.AsNoTracking().Include(x => x.User).Include(x => x.Store)
                .Where(x => x.EventId == id).OrderBy(x => x.BoothCode).ToListAsync();
            return Ok(list.Select(x => new
            {
                x.Id, x.Name, x.BoothCode, x.Description, x.InviteToken, x.AcceptedAt,
                acceptedAs = x.Store?.Name ?? x.User?.Username
            }));
        }

        [HttpPost("api/events/{id:int}/exhibitors")]
        public async Task<IActionResult> Add(int id, [FromBody] ExhibitorRequest request)
        {
            var (ev, error) = await OwnEventAsync(id);
            if (ev == null) return error!;
            if (!await _context.EventPackages.AnyAsync(p => p.EventId == id)) return StatusCode(402, new { message = "Gli espositori sono inclusi nel pacchetto organizzatori" });
            var name = (request.Name ?? "").Trim();
            var booth = (request.BoothCode ?? "").Trim();
            if (name.Length is < 2 or > 80) return BadRequest(new { message = "Indica il nome dell'espositore (da 2 a 80 caratteri)" });
            if (booth.Length is < 1 or > 40) return BadRequest(new { message = "Indica lo stand (al massimo 40 caratteri)" });
            if (await _context.EventExhibitors.CountAsync(x => x.EventId == id) >= MaxExhibitors) return BadRequest(new { message = "Hai raggiunto il numero massimo di espositori" });
            var x = new EventExhibitor { EventId = id, Name = name, BoothCode = booth, Description = request.Description?.Trim() is { Length: > 0 } d ? d[..Math.Min(200, d.Length)] : null, InviteToken = EventPackages.NewToken() };
            _context.EventExhibitors.Add(x);
            await _context.SaveChangesAsync();
            return Ok(new { x.Id, x.Name, x.BoothCode, x.Description, x.InviteToken });
        }

        [HttpDelete("api/events/{id:int}/exhibitors/{exhibitorId:int}")]
        public async Task<IActionResult> Remove(int id, int exhibitorId)
        {
            var (ev, error) = await OwnEventAsync(id);
            if (ev == null) return error!;
            var x = await _context.EventExhibitors.FirstOrDefaultAsync(e => e.Id == exhibitorId && e.EventId == id);
            if (x == null) return NotFound(new { message = "Espositore non trovato" });
            _context.EventExhibitors.Remove(x);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Espositore rimosso" });
        }

        // ---------------------------------------------------------------- invito (espositore)

        [HttpGet("api/exhibitor-invites/{token}")]
        public async Task<IActionResult> Invite(string token)
        {
            var x = await _context.EventExhibitors.AsNoTracking().Include(e => e.Event).FirstOrDefaultAsync(e => e.InviteToken == token);
            if (x == null) return NotFound(new { message = "Invito non valido" });
            var stores = await _context.Stores.AsNoTracking().Where(s => s.OwnerId == Me && s.Status == StoreStatus.Verified)
                .Select(s => new { s.Id, s.Name }).ToListAsync();
            return Ok(new
            {
                eventId = x.EventId, eventTitle = x.Event.Title, startDate = x.Event.StartDate, city = x.Event.City,
                x.Name, x.BoothCode, x.Description, accepted = x.AcceptedAt != null, mine = x.UserId == Me, stores
            });
        }

        [HttpPost("api/exhibitor-invites/{token}/accept")]
        public async Task<IActionResult> Accept(string token, [FromBody] AcceptRequest? request)
        {
            var x = await _context.EventExhibitors.FirstOrDefaultAsync(e => e.InviteToken == token);
            if (x == null) return NotFound(new { message = "Invito non valido" });
            if (x.AcceptedAt != null && x.UserId != Me) return Conflict(new { message = "Questo invito è già stato usato" });
            if (request?.StoreId is { } storeId && !await _context.Stores.AnyAsync(s => s.Id == storeId && s.OwnerId == Me && s.Status == StoreStatus.Verified))
                return BadRequest(new { message = "Puoi partecipare solo a nome dei tuoi negozi verificati" });
            x.UserId = Me;
            x.StoreId = request?.StoreId;
            x.AcceptedAt ??= DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Sei tra gli espositori", eventId = x.EventId });
        }

        // ---------------------------------------------------------------- elenco pubblico

        /// <summary>Espositori dell'evento: prima chi è "live", poi chi ha più carte che cerchi; filtrabile per gioco.</summary>
        [HttpGet("api/events/{id:int}/exhibitors")]
        public async Task<IActionResult> List(int id, [FromQuery] int? gameId, [FromServices] IUserBlockService blocks)
        {
            var package = await _context.EventPackages.AsNoTracking().FirstOrDefaultAsync(p => p.EventId == id);
            if (package == null) return Ok(new { active = false });
            var now = DateTime.UtcNow;
            var blocked = await blocks.GetBlockedUserIdsAsync(Me);
            var list = (await _context.EventExhibitors.AsNoTracking().Include(x => x.User).Include(x => x.Store)
                .Where(x => x.EventId == id && x.AcceptedAt != null && x.UserId != null).ToListAsync())
                .Where(x => !blocked.Contains(x.UserId!.Value)).ToList();
            var owners = list.Select(x => x.UserId!.Value).Distinct().ToList();
            var live = (await _context.LocationBroadcasts.AsNoTracking()
                .Where(b => b.EventId == id && owners.Contains(b.AuthorId) && b.EndedAt == null && b.EndsAt > now)
                .Select(b => new { b.AuthorId, b.Id, b.SpotDetail }).ToListAsync())
                .GroupBy(b => b.AuthorId).ToDictionary(g => g.Key, g => g.First());
            var wanted = _context.WishlistItems.Where(w => w.UserId == Me).Select(w => w.CardInfoId);
            var stock = _context.Cards.AsNoTracking().Where(c => owners.Contains(c.UserId) && c.IsAvailableForTrade && c.Quantity > c.ReservedQuantity);
            if (gameId.HasValue) stock = stock.Where(c => c.CardInfo.CardSet.GameId == gameId.Value);
            var wantedCounts = await stock.Where(c => wanted.Contains(c.CardInfoId))
                .GroupBy(c => c.UserId).Select(g => new { g.Key, Count = g.Select(c => c.CardInfoId).Distinct().Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);
            var withGame = gameId.HasValue ? (await stock.Select(c => c.UserId).Distinct().ToListAsync()).ToHashSet() : null;

            var items = list
                .Where(x => withGame == null || withGame.Contains(x.UserId!.Value))
                .Select(x => new
                {
                    x.Id, name = x.Store?.Name ?? x.User!.Username, userId = x.UserId, storeId = x.StoreId, x.BoothCode, x.Description,
                    liveBroadcastId = live.TryGetValue(x.UserId!.Value, out var b) ? b.Id : (int?)null,
                    wantedCards = wantedCounts.GetValueOrDefault(x.UserId!.Value)
                })
                .OrderByDescending(x => x.liveBroadcastId != null).ThenByDescending(x => x.wantedCards).ThenBy(x => x.BoothCode)
                .ToList();
            return Ok(new { active = true, tier = package.Tier.ToString(), floorPlanImageUrl = package.FloorPlanImageUrl, exhibitors = items });
        }
    }
}
