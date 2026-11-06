using CardExchange.API.Authorization;
using CardExchange.API.Configuration;
using CardExchange.API.DTOs.Requests;
using CardExchange.API.DTOs.Responses;
using CardExchange.API.Services.Billing;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Abbonamenti alla piattaforma (Premium, Negozio). Pagamento con carta tramite Stripe oppure con bonifico
    /// confermato da un amministratore. Non riguarda gli accordi tra utenti, che restano fuori dall'app.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SubscriptionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IBillingService _billing;
        private readonly IStripeBilling _stripe;
        private readonly BillingOptions _billingOptions;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SubscriptionsController> _logger;

        public SubscriptionsController(
            ApplicationDbContext context,
            ISubscriptionService subscriptionService,
            IBillingService billing,
            IStripeBilling stripe,
            IOptions<BillingOptions> billingOptions,
            IConfiguration configuration,
            ILogger<SubscriptionsController> logger)
        {
            _context = context;
            _subscriptionService = subscriptionService;
            _billing = billing;
            _stripe = stripe;
            _billingOptions = billingOptions.Value;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>Piani disponibili.</summary>
        [HttpGet("plans")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<SubscriptionPlanDto>>> GetPlans()
        {
            var plans = await _context.SubscriptionPlans.AsNoTracking().Where(p => p.IsActive).ToListAsync();
            // Ordinamento in memoria: SQLite (sviluppo) non supporta ORDER BY su decimal
            return Ok(plans.OrderBy(p => p.SortOrder).ThenBy(p => p.Price).Select(MapToPlanDto));
        }

        /// <summary>Metodi di pagamento attivi sul server e dati per il bonifico.</summary>
        [HttpGet("billing-options")]
        [AllowAnonymous]
        public IActionResult GetBillingOptions()
        {
            var bt = _billingOptions.BankTransfer;
            return Ok(new
            {
                cardEnabled = _billingOptions.Stripe.IsConfigured,
                bankTransfer = bt.IsConfigured
                    ? new { enabled = true, accountHolder = bt.AccountHolder, iban = bt.Iban, bic = bt.Bic, bankName = bt.BankName }
                    : null
            });
        }

        /// <summary>
        /// Stato dell'utente: abbonamento in corso, periodi già pagati che partiranno dopo, richieste di bonifico in attesa.
        /// </summary>
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentSubscription()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();
            var now = DateTime.UtcNow;

            var subs = await _context.UserSubscriptions
                .AsNoTracking()
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId &&
                            ((s.Status == SubscriptionStatus.Active && s.EndDate > now) || s.Status == SubscriptionStatus.PendingPayment))
                .OrderBy(s => s.StartDate)
                .ToListAsync();

            var active = subs.Where(s => s.Status == SubscriptionStatus.Active && s.StartDate <= now)
                .OrderByDescending(s => s.Plan.Tier).FirstOrDefault();
            return Ok(new
            {
                tier = active?.Plan.Tier.ToString() ?? "Free",
                active = active != null ? MapToSubscriptionDto(active) : null,
                queued = subs.Where(s => s.Status == SubscriptionStatus.Active && s.StartDate > now).Select(MapToSubscriptionDto),
                pending = subs.Where(s => s.Status == SubscriptionStatus.PendingPayment).Select(MapToSubscriptionDto)
            });
        }

        [HttpGet("limits")]
        public async Task<ActionResult<UserLimitsDto>> GetMyLimits()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var plan = await _subscriptionService.GetUserActivePlanAsync(userId);
            var now = DateTime.UtcNow;

            var cardsCount = await _context.Cards.CountAsync(c => c.UserId == userId);
            var wishlistCount = await _context.WishlistItems.CountAsync(w => w.UserId == userId);
            var activeTradesCount = await _context.TradeOffers.CountAsync(t =>
                (t.SenderId == userId || t.ReceiverId == userId) && t.Status == TradeOfferStatus.Pending);
            var todayMessagesCount = await _context.Messages.CountAsync(m =>
                m.SenderId == userId && m.CreatedAt >= now.Date);

            var expiresAt = await _context.UserSubscriptions
                .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active && s.EndDate > now)
                .Select(s => (DateTime?)s.EndDate)
                .MaxAsync();

            return Ok(new UserLimitsDto
            {
                Tier = plan?.Tier.ToString() ?? "Free",
                CardsUsed = cardsCount,
                // null = nessun limite
                CardsLimit = plan == null || plan.MaxCards == int.MaxValue ? null : plan.MaxCards,
                WishlistUsed = wishlistCount,
                WishlistLimit = plan?.MaxWishlistItems ?? 20,
                ActiveTradesUsed = activeTradesCount,
                ActiveTradesLimit = plan?.MaxActiveTradeOffers ?? 5,
                DailyMessagesUsed = todayMessagesCount,
                DailyMessagesLimit = plan?.MaxDailyMessages ?? 10,
                CanSearchAdvanced = plan?.CanSearchAdvanced ?? false,
                CanSearchGeographic = plan?.CanSearchGeographic ?? false,
                CanExportCollection = plan?.CanExportCollection ?? false,
                CanViewStatistics = plan?.CanViewStatistics ?? false,
                SubscriptionExpiresAt = expiresAt
            });
        }

        /// <summary>
        /// Attivazione immediata senza pagamento: solo in sviluppo (Subscriptions:AllowSimulatedPayments=true).
        /// In produzione si paga con carta (Stripe) o con bonifico.
        /// </summary>
        [HttpPost("subscribe")]
        public async Task<ActionResult<UserSubscriptionDto>> Subscribe([FromBody] CreateSubscriptionRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var plan = await _context.SubscriptionPlans.FindAsync(request.PlanId);
            if (plan == null || !plan.IsActive)
                return NotFound(new { message = "Piano non trovato o non disponibile" });

            if (plan.Price > 0 && !_configuration.GetValue<bool>("Subscriptions:AllowSimulatedPayments"))
            {
                _logger.LogWarning("Tentativo di attivare il piano a pagamento {PlanId} senza pagamento da utente {UserId}", plan.Id, userId);
                return StatusCode(StatusCodes.Status402PaymentRequired, new
                {
                    message = "Per attivare il piano scegli il pagamento con carta o con bonifico."
                });
            }

            var subscription = await _billing.ActivateAsync(new ActivationRequest(
                userId, plan, SubscriptionPaymentMethod.Simulated, plan.Price, request.PaymentReference));
            subscription.Plan = plan;
            return Ok(MapToSubscriptionDto(subscription));
        }

        public class BankTransferRequest
        {
            public int PlanId { get; set; }
        }

        /// <summary>
        /// Richiesta di pagamento con bonifico: restituisce importo, IBAN e causale. Il piano si attiva quando un
        /// amministratore registra il bonifico ricevuto.
        /// </summary>
        [HttpPost("bank-transfer")]
        public async Task<IActionResult> RequestBankTransfer([FromBody] BankTransferRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();
            var bt = _billingOptions.BankTransfer;
            if (!bt.IsConfigured) return BadRequest(new { message = "Il pagamento con bonifico non è disponibile" });

            var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == request.PlanId && p.IsActive);
            if (plan == null || plan.Price <= 0) return NotFound(new { message = "Piano non trovato" });

            // Una richiesta in attesa per piano: se esiste già si riusa la stessa causale
            var pending = await _context.UserSubscriptions.Include(s => s.Plan).FirstOrDefaultAsync(s =>
                s.UserId == userId && s.PlanId == plan.Id && s.Status == SubscriptionStatus.PendingPayment);
            if (pending == null)
            {
                pending = new UserSubscription
                {
                    UserId = userId,
                    PlanId = plan.Id,
                    Plan = plan,
                    Status = SubscriptionStatus.PendingPayment,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow,
                    AutoRenew = false,
                    PaymentMethod = SubscriptionPaymentMethod.BankTransfer,
                    PaymentReference = _billing.NewBankTransferReference(userId),
                    AmountPaid = 0,
                    Currency = plan.Currency
                };
                _context.UserSubscriptions.Add(pending);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Richiesta bonifico {Reference} per piano {Plan} da utente {UserId}", pending.PaymentReference, plan.Name, userId);
            }

            return Ok(new
            {
                request = MapToSubscriptionDto(pending),
                amount = plan.Price,
                currency = plan.Currency,
                accountHolder = bt.AccountHolder,
                iban = bt.Iban,
                bic = bt.Bic,
                bankName = bt.BankName,
                reference = pending.PaymentReference,
                message = "Esegui il bonifico indicando la causale: il piano si attiva appena registriamo il pagamento (di solito entro 2 giorni lavorativi)."
            });
        }

        [HttpDelete("bank-transfer/{id:int}")]
        public async Task<IActionResult> CancelBankTransfer(int id)
        {
            var userId = GetCurrentUserId();
            var pending = await _context.UserSubscriptions.FirstOrDefaultAsync(s =>
                s.Id == id && s.UserId == userId && s.Status == SubscriptionStatus.PendingPayment);
            if (pending == null) return NotFound(new { message = "Richiesta non trovata" });
            pending.Status = SubscriptionStatus.Cancelled;
            pending.CancelledAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Richiesta annullata" });
        }

        /// <summary>
        /// Disdice il rinnovo automatico: il piano resta attivo fino alla scadenza già pagata.
        /// </summary>
        [HttpPost("cancel")]
        public async Task<IActionResult> CancelSubscription([FromBody] CancelSubscriptionRequest? request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var subscription = await _context.UserSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active && s.AutoRenew && s.EndDate > DateTime.UtcNow)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync();
            if (subscription == null)
                return NotFound(new { message = "Nessun rinnovo automatico attivo da disdire" });

            if (subscription.PaymentMethod == SubscriptionPaymentMethod.Stripe && subscription.ExternalSubscriptionId != null)
            {
                if (!_stripe.IsEnabled)
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Il servizio di pagamento non è disponibile, riprova più tardi." });
                try
                {
                    // Stripe conferma poi con customer.subscription.updated
                    await _stripe.CancelAtPeriodEndAsync(subscription.ExternalSubscriptionId);
                }
                catch (Stripe.StripeException ex)
                {
                    _logger.LogError(ex, "Disdetta Stripe fallita per l'abbonamento {SubscriptionId}", subscription.Id);
                    return StatusCode(StatusCodes.Status502BadGateway, new { message = "Il servizio di pagamento non risponde, riprova tra poco." });
                }
            }

            subscription.AutoRenew = false;
            subscription.CancelledAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Utente {UserId} ha disdetto il rinnovo dell'abbonamento {SubscriptionId}", userId, subscription.Id);

            return Ok(new { message = "Rinnovo disdetto. Il piano resta attivo fino alla scadenza.", activeUntil = subscription.EndDate });
        }

        [HttpGet("history")]
        public async Task<ActionResult<IEnumerable<UserSubscriptionDto>>> GetSubscriptionHistory()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var subscriptions = await _context.UserSubscriptions
                .AsNoTracking()
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
            return Ok(subscriptions.Select(MapToSubscriptionDto));
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
        }

        public static SubscriptionPlanDto MapToPlanDto(SubscriptionPlan plan) => new()
        {
            Id = plan.Id,
            Name = plan.Name,
            Description = plan.Description,
            Tier = plan.Tier.ToString(),
            Cycle = plan.Cycle.ToString(),
            Price = plan.Price,
            Currency = plan.Currency,
            MaxCards = plan.MaxCards,
            MaxWishlistItems = plan.MaxWishlistItems,
            MaxActiveTradeOffers = plan.MaxActiveTradeOffers,
            MaxDailyMessages = plan.MaxDailyMessages,
            CanSearchAdvanced = plan.CanSearchAdvanced,
            CanSearchGeographic = plan.CanSearchGeographic,
            CanExportCollection = plan.CanExportCollection,
            CanViewStatistics = plan.CanViewStatistics,
            CanSaveSearches = plan.CanSaveSearches,
            HasPriorityListing = plan.HasPriorityListing,
            HasVerifiedBadge = plan.HasVerifiedBadge,
            HasWishlistAlerts = plan.HasWishlistAlerts,
            SortOrder = plan.SortOrder,
            MaxBroadcastsPerDay = plan.MaxBroadcastsPerDay,
            MaxRadiusKm = plan.MaxRadiusKm,
            MaxActiveReservations = plan.MaxActiveReservations,
            MonthlyPromotionCredits = plan.MonthlyPromotionCredits,
            Capabilities = CardExchange.Core.Rules.UserEntitlements.CapabilitiesOf(plan).Select(c => c.ToString()).ToList()
        };

        public static UserSubscriptionDto MapToSubscriptionDto(UserSubscription sub) => new()
        {
            Id = sub.Id,
            PlanName = sub.Plan?.Name ?? "N/A",
            Tier = sub.Plan?.Tier.ToString() ?? "Free",
            Status = sub.Status.ToString(),
            StartDate = sub.StartDate,
            EndDate = sub.EndDate,
            AutoRenew = sub.AutoRenew,
            AmountPaid = sub.AmountPaid,
            Currency = sub.Currency,
            PaymentMethod = sub.PaymentMethod.ToString(),
            PaymentReference = sub.PaymentReference,
            CreatedAt = sub.CreatedAt,
            Plan = sub.Plan != null ? MapToPlanDto(sub.Plan) : null
        };
    }
}
