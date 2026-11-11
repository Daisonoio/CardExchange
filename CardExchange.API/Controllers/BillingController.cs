using CardExchange.API.Authorization;
using CardExchange.API.Configuration;
using CardExchange.API.Services.Billing;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using System.Security.Claims;

namespace CardExchange.API.Controllers
{
    /// <summary>Pagamento con carta degli abbonamenti alla piattaforma (Stripe Checkout).</summary>
    [ApiController]
    [Route("api/billing")]
    [Authorize]
    public class BillingController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IStripeBilling _stripe;
        private readonly EmailSettings _email;
        private readonly ILogger<BillingController> _logger;

        public BillingController(ApplicationDbContext context, IStripeBilling stripe, IOptions<EmailSettings> email,
            ILogger<BillingController> logger)
        {
            _context = context;
            _stripe = stripe;
            _email = email.Value;
            _logger = logger;
        }

        public class CheckoutRequest
        {
            public int PlanId { get; set; }
        }

        [RequirePaidPlans]
        [HttpPost("stripe/checkout")]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request)
        {
            if (!_stripe.IsEnabled) return BadRequest(new { message = "Il pagamento con carta non è disponibile" });
            var userId = CurrentUserId();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return Unauthorized();
            var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == request.PlanId && p.IsActive);
            if (plan == null || plan.Price <= 0) return NotFound(new { message = "Piano non trovato" });

            // Con un rinnovo automatico già attivo si cambia piano dal portale, non con un secondo abbonamento
            var hasAutoRenew = await _context.UserSubscriptions.AnyAsync(s => s.UserId == userId
                && s.PaymentMethod == SubscriptionPaymentMethod.Stripe && s.Status == SubscriptionStatus.Active
                && s.AutoRenew && s.EndDate > DateTime.UtcNow);
            if (hasAutoRenew)
                return Conflict(new { message = "Hai già un abbonamento con rinnovo automatico: gestiscilo dal portale di pagamento." });

            var baseUrl = _email.FrontendBaseUrl.TrimEnd('/');
            try
            {
                var url = await _stripe.CreateCheckoutAsync(user, plan,
                    $"{baseUrl}/premium?checkout=success", $"{baseUrl}/premium?checkout=cancelled");
                return Ok(new { url });
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Creazione checkout Stripe fallita per utente {UserId}", userId);
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "Il servizio di pagamento non risponde, riprova tra poco." });
            }
        }

        [RequirePaidPlans]
        [HttpPost("stripe/portal")]
        public async Task<IActionResult> Portal()
        {
            if (!_stripe.IsEnabled) return BadRequest(new { message = "Il pagamento con carta non è disponibile" });
            var userId = CurrentUserId();
            var customerId = await _context.UserSubscriptions
                .Where(s => s.UserId == userId && s.ExternalCustomerId != null)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => s.ExternalCustomerId)
                .FirstOrDefaultAsync();
            if (customerId == null) return NotFound(new { message = "Nessun pagamento con carta registrato" });
            try
            {
                var url = await _stripe.CreatePortalAsync(customerId, $"{_email.FrontendBaseUrl.TrimEnd('/')}/premium");
                return Ok(new { url });
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Creazione portale Stripe fallita per utente {UserId}", userId);
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "Il servizio di pagamento non risponde, riprova tra poco." });
            }
        }

        /// <summary>Notifiche di Stripe. Autenticate dalla firma Stripe-Signature, non dal token utente.</summary>
        [HttpPost("stripe/webhook")]
        [AllowAnonymous]
        [RequestSizeLimit(256 * 1024)]
        public async Task<IActionResult> Webhook()
        {
            if (!_stripe.IsEnabled) return NotFound();
            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync();
            var result = await _stripe.HandleWebhookAsync(json, Request.Headers["Stripe-Signature"]);
            // Rispondere 200 anche agli eventi ignorati o duplicati, altrimenti Stripe continua a riprovare
            return result == WebhookResult.InvalidSignature ? BadRequest() : Ok(new { result = result.ToString() });
        }

        private int CurrentUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
    }
}
