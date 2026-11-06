using System.Security.Claims;
using CardExchange.API.Configuration;
using CardExchange.API.Services.Billing;
using CardExchange.API.Services.Entitlements;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CardExchange.API.Controllers
{
    /// <summary>Promozioni locali: evento in evidenza e "Sono qui" sponsorizzato. Pagate a CardExchange, mai tra utenti.</summary>
    [ApiController]
    [Authorize]
    [Route("api/promotions")]
    public class PromotionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly PromotionService _promotions;

        public PromotionsController(ApplicationDbContext context, PromotionService promotions)
        {
            _context = context;
            _promotions = promotions;
        }

        public record CreateRequest(PromotionType Type, int TargetId, int RadiusKm, int? Days);
        public record PayRequest(string Method);
        public record ReportRequest(string? Reason);

        private int Me => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
        private IActionResult Fail(PromotionException ex) => StatusCode(ex.Status, new { message = ex.Message, code = ex.Code });

        /// <summary>Tariffe in crediti per tipo e raggio, saldo e crediti mensili del piano.</summary>
        [HttpGet("pricing")]
        public async Task<IActionResult> Pricing()
        {
            var monthly = await this.Entitlements(_context).LimitAsync(Me, PlanLimit.MonthlyPromotionCredits);
            return Ok(new
            {
                creditPriceEur = PromotionRules.CreditPriceEur,
                minRadiusKm = PromotionRules.MinRadiusKm,
                maxRadiusKm = PromotionRules.MaxRadiusKm,
                balance = await _promotions.CreditBalanceAsync(Me),
                monthlyCredits = monthly,
                types = new[] { PromotionType.FeaturedEvent, PromotionType.SponsoredBroadcast }.Select(t => new
                {
                    type = t.ToString(),
                    prices = new[] { 10, 25, 50 }.Select(km => new { upToKm = km, credits = PromotionRules.PriceCredits(t, km), eur = PromotionRules.PriceEur(t, km) })
                })
            });
        }

        /// <summary>Cosa posso promuovere: i miei eventi futuri pubblicati e i miei "Sono qui" in corso.</summary>
        [HttpGet("targets")]
        public async Task<IActionResult> Targets()
        {
            var now = DateTime.UtcNow;
            var events = await _context.Events.AsNoTracking()
                .Where(e => e.OrganizerId == Me && e.Status == EventStatus.Published && e.IsPublic && e.StartDate > now)
                .OrderBy(e => e.StartDate).Take(20)
                .Select(e => new { id = e.Id, title = e.Title, city = e.City, startsAt = e.StartDate, hasLocation = e.Latitude != null })
                .ToListAsync();
            var broadcasts = await _context.LocationBroadcasts.AsNoTracking()
                .Where(b => b.AuthorId == Me && b.EndedAt == null && b.EndsAt > now)
                .OrderByDescending(b => b.CreatedAt).Take(10)
                .Select(b => new { id = b.Id, title = b.PlaceName, endsAt = b.EndsAt })
                .ToListAsync();
            return Ok(new { events, broadcasts });
        }

        [HttpGet("estimate")]
        public async Task<IActionResult> Estimate([FromQuery] PromotionType type, [FromQuery] int targetId, [FromQuery] int radiusKm = 10)
        {
            try { return Ok(await _promotions.EstimateAsync(Me, type, targetId, radiusKm, DateTime.UtcNow)); }
            catch (PromotionException ex) { return Fail(ex); }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateRequest request)
        {
            try
            {
                var p = await _promotions.CreateAsync(Me, request.Type, request.TargetId, request.RadiusKm, request.Days, DateTime.UtcNow);
                return Ok(Dto(p, null));
            }
            catch (PromotionException ex) { return Fail(ex); }
        }

        /// <summary>Paga con i crediti (consegna subito) o con la carta (Stripe; consegna dopo la conferma del pagamento).</summary>
        [HttpPost("{id:int}/pay")]
        public async Task<IActionResult> Pay(int id, [FromBody] PayRequest request,
            [FromServices] PurchaseService purchases, [FromServices] IStripeBilling stripe, [FromServices] IConfiguration configuration,
            [FromServices] IOptions<EmailSettings> email)
        {
            try
            {
                if (string.Equals(request.Method, "credits", StringComparison.OrdinalIgnoreCase))
                {
                    var p = await _promotions.PayWithCreditsAsync(Me, id, DateTime.UtcNow);
                    return Ok(new { message = "Promozione attiva", promotion = Dto(p, null) });
                }
                if (!configuration.GetValue("Purchases:Enabled", false) || !stripe.IsEnabled)
                    return BadRequest(new { message = "Il pagamento con carta non è ancora disponibile: usa i crediti" });
                var promo = await _context.Promotions.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == Me);
                if (promo == null) return NotFound(new { message = "Promozione non trovata" });
                if (promo.Status != PromotionStatus.PendingPayment) return BadRequest(new { message = "Questa promozione non è in attesa di pagamento" });
                var user = await _context.Users.FirstAsync(u => u.Id == Me);
                var purchase = await purchases.CreateForPromotionAsync(Me, promo);
                var baseUrl = email.Value.FrontendBaseUrl.TrimEnd('/');
                try
                {
                    var name = promo.Type == PromotionType.FeaturedEvent ? "Evento in evidenza" : "\"Sono qui\" sponsorizzato";
                    var url = await stripe.CreatePaymentCheckoutAsync(user, purchase, name,
                        $"{baseUrl}/promotions?checkout=success", $"{baseUrl}/promotions?checkout=cancelled");
                    return Ok(new { url });
                }
                catch (Stripe.StripeException)
                {
                    await purchases.MarkExpiredAsync(purchase.Id);
                    return StatusCode(502, new { message = "Il servizio di pagamento non risponde, riprova tra poco." });
                }
            }
            catch (PromotionException ex) { return Fail(ex); }
        }

        [HttpPost("{id:int}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                await _promotions.CancelAsync(Me, id, DateTime.UtcNow);
                return Ok(new { message = "Promozione annullata" });
            }
            catch (PromotionException ex) { return Fail(ex); }
        }

        /// <summary>Le mie promozioni con i risultati.</summary>
        [HttpGet("mine")]
        public async Task<IActionResult> Mine()
        {
            var list = await _context.Promotions.AsNoTracking().Where(p => p.OwnerUserId == Me)
                .OrderByDescending(p => p.CreatedAt).Take(50).ToListAsync();
            var eventIds = list.Where(p => p.Type == PromotionType.FeaturedEvent).Select(p => p.TargetId).ToList();
            var broadcastIds = list.Where(p => p.Type == PromotionType.SponsoredBroadcast).Select(p => p.TargetId).ToList();
            var titles = (await _context.Events.AsNoTracking().Where(e => eventIds.Contains(e.Id)).Select(e => new { e.Id, e.Title }).ToListAsync())
                .ToDictionary(e => (PromotionType.FeaturedEvent, e.Id), e => e.Title);
            foreach (var b in await _context.LocationBroadcasts.AsNoTracking().Where(b => broadcastIds.Contains(b.Id)).Select(b => new { b.Id, b.PlaceName }).ToListAsync())
                titles[(PromotionType.SponsoredBroadcast, b.Id)] = b.PlaceName;
            return Ok(list.Select(p => Dto(p, titles.GetValueOrDefault((p.Type, p.TargetId)))));
        }

        /// <summary>Eventi in evidenza vicino a chi guarda, con l'etichetta "Sponsorizzato".</summary>
        [HttpGet("featured-events")]
        public async Task<IActionResult> FeaturedEvents([FromQuery] decimal? latitude, [FromQuery] decimal? longitude)
        {
            var list = await _promotions.FeaturedEventsAsync(Me, latitude, longitude, DateTime.UtcNow);
            return Ok(list.Select(x => new
            {
                promotionId = x.Promo.Id,
                sponsored = true,
                sponsor = x.Event.Store?.Name ?? x.Event.Organizer.Username,
                id = x.Event.Id,
                title = x.Event.Title,
                type = x.Event.Type.ToString(),
                startDate = x.Event.StartDate,
                city = x.Event.City,
                address = x.Event.Address,
                imageUrl = x.Event.ImageUrl,
                storeId = x.Event.StoreId
            }));
        }

        [HttpPost("{id:int}/click")]
        public async Task<IActionResult> Click(int id)
        {
            await _promotions.ClickAsync(id);
            return NoContent();
        }

        [HttpPost("{id:int}/report")]
        public async Task<IActionResult> Report(int id, [FromBody] ReportRequest? request)
        {
            try
            {
                await _promotions.ReportAsync(Me, id, request?.Reason, DateTime.UtcNow);
                return Ok(new { message = "Segnalazione ricevuta, grazie" });
            }
            catch (PromotionException ex) { return Fail(ex); }
        }

        private static object Dto(Promotion p, string? title) => new
        {
            p.Id, type = p.Type.ToString(), p.TargetId, title, p.RadiusKm, p.StartsAt, p.EndsAt, status = p.Status.ToString(),
            paidWith = p.PaidWith.ToString(), p.PriceCredits, priceEur = p.PriceCredits * PromotionRules.CreditPriceEur,
            p.Reach, p.Impressions, p.Clicks, p.DeliveredAt, p.RejectionReason
        };
    }
}
