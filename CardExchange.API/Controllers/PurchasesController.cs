using System.Security.Claims;
using CardExchange.API.Configuration;
using CardExchange.API.Services.Billing;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Acquisti una tantum verso CardExchange (crediti promozione, promozioni, pacchetti). Mai pagamenti tra utenti.
    /// La vendita si accende con Purchases:Enabled quando i prodotti sono utilizzabili.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/purchases")]
    public class PurchasesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly PurchaseService _purchases;
        private readonly IStripeBilling _stripe;
        private readonly IConfiguration _configuration;
        private readonly EmailSettings _email;

        public PurchasesController(ApplicationDbContext context, PurchaseService purchases, IStripeBilling stripe,
            IConfiguration configuration, IOptions<EmailSettings> email)
        {
            _context = context;
            _purchases = purchases;
            _stripe = stripe;
            _configuration = configuration;
            _email = email.Value;
        }

        private bool Enabled => _configuration.GetValue("Purchases:Enabled", false) && _stripe.IsEnabled;

        public record CheckoutRequest(string ProductCode, int? RefId);

        [HttpGet("products")]
        [AllowAnonymous]
        public IActionResult Products() => Ok(new
        {
            enabled = Enabled,
            products = PurchaseCatalog.Products.Select(p => new { p.Code, type = p.Type.ToString(), p.Name, p.Quantity, p.PriceEur })
        });

        /// <summary>Crea l'acquisto (in attesa) e la sessione Stripe; il prodotto si attiva solo dal webhook.</summary>
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request)
        {
            if (!Enabled) return BadRequest(new { message = "Gli acquisti non sono ancora disponibili" });
            var product = PurchaseCatalog.Find(request.ProductCode);
            if (product == null) return BadRequest(new { message = "Prodotto non trovato" });
            var user = await _context.Users.FirstAsync(u => u.Id == CurrentUserId());
            var purchase = await _purchases.CreatePendingAsync(user.Id, product.Code, request.RefId);
            var baseUrl = _email.FrontendBaseUrl.TrimEnd('/');
            try
            {
                var url = await _stripe.CreatePaymentCheckoutAsync(user, purchase, product.Name,
                    $"{baseUrl}/purchases?checkout=success", $"{baseUrl}/purchases?checkout=cancelled");
                return Ok(new { url, purchaseId = purchase.Id });
            }
            catch (Stripe.StripeException)
            {
                await _purchases.MarkExpiredAsync(purchase.Id);
                return StatusCode(502, new { message = "Il servizio di pagamento non risponde, riprova tra poco." });
            }
        }

        /// <summary>I miei acquisti e il saldo dei crediti promozione.</summary>
        [HttpGet("mine")]
        public async Task<IActionResult> Mine()
        {
            var me = CurrentUserId();
            var list = await _context.Purchases.AsNoTracking().Where(p => p.UserId == me)
                .OrderByDescending(p => p.CreatedAt).Take(50)
                .Select(p => new { p.Id, p.ProductCode, type = p.ProductType.ToString(), p.Quantity, p.AmountEur, status = p.Status.ToString(), p.CreatedAt, p.PaidAt, p.RefundedAt })
                .ToListAsync();
            return Ok(new { credits = await _purchases.CreditBalanceAsync(me), purchases = list });
        }

        private int CurrentUserId() => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
    }
}
