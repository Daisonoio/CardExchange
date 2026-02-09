using CardExchange.API.Authorization;
using CardExchange.API.DTOs.Requests;
using CardExchange.API.DTOs.Responses;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CardExchange.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SubscriptionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<SubscriptionsController> _logger;

        public SubscriptionsController(
            ApplicationDbContext context,
            ISubscriptionService subscriptionService,
            ILogger<SubscriptionsController> logger)
        {
            _context = context;
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        /// <summary>
        /// Ottiene tutti i piani di abbonamento disponibili
        /// </summary>
        [HttpGet("plans")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<SubscriptionPlanDto>>> GetPlans()
        {
            var plans = await _context.SubscriptionPlans
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.Price)
                .ToListAsync();

            return Ok(plans.Select(MapToPlanDto));
        }

        /// <summary>
        /// Ottiene l'abbonamento attivo dell'utente corrente
        /// </summary>
        [HttpGet("current")]
        public async Task<ActionResult<UserSubscriptionDto>> GetCurrentSubscription()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var subscription = await _context.UserSubscriptions
                .AsNoTracking()
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active && s.EndDate > DateTime.UtcNow)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync();

            if (subscription == null)
            {
                return Ok(new { tier = "Free", message = "Nessun abbonamento attivo" });
            }

            return Ok(MapToSubscriptionDto(subscription));
        }

        /// <summary>
        /// Ottiene i limiti correnti dell'utente (usato/disponibile)
        /// </summary>
        [HttpGet("limits")]
        public async Task<ActionResult<UserLimitsDto>> GetMyLimits()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var plan = await _subscriptionService.GetUserActivePlanAsync(userId);

            var cardsCount = await _context.Cards.CountAsync(c => c.UserId == userId);
            var wishlistCount = await _context.WishlistItems.CountAsync(w => w.UserId == userId);
            var activeTradesCount = await _context.TradeOffers.CountAsync(t =>
                (t.SenderId == userId || t.ReceiverId == userId) && t.Status == TradeOfferStatus.Pending);
            var todayMessagesCount = await _context.Messages.CountAsync(m =>
                m.SenderId == userId && m.CreatedAt >= DateTime.UtcNow.Date);

            var activeSubscription = await _context.UserSubscriptions
                .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active && s.EndDate > DateTime.UtcNow)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync();

            return Ok(new UserLimitsDto
            {
                Tier = plan?.Tier.ToString() ?? "Free",
                CardsUsed = cardsCount,
                CardsLimit = plan?.MaxCards ?? 50,
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
                SubscriptionExpiresAt = activeSubscription?.EndDate
            });
        }

        /// <summary>
        /// Sottoscrivi un piano (simula pagamento per ora)
        /// </summary>
        [HttpPost("subscribe")]
        public async Task<ActionResult<UserSubscriptionDto>> Subscribe([FromBody] CreateSubscriptionRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var plan = await _context.SubscriptionPlans.FindAsync(request.PlanId);
            if (plan == null || !plan.IsActive)
            {
                return NotFound(new { message = "Piano non trovato o non disponibile" });
            }

            // Verifica se ha già un abbonamento attivo
            var existing = await _context.UserSubscriptions
                .AnyAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active && s.EndDate > DateTime.UtcNow);

            if (existing)
            {
                return BadRequest(new { message = "Hai già un abbonamento attivo. Cancellalo prima di sottoscriverne uno nuovo." });
            }

            var duration = plan.Cycle == BillingCycle.Annual
                ? TimeSpan.FromDays(365)
                : TimeSpan.FromDays(30);

            var subscription = new UserSubscription
            {
                UserId = userId,
                PlanId = plan.Id,
                Status = SubscriptionStatus.Active,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.Add(duration),
                AutoRenew = true,
                PaymentReference = request.PaymentReference,
                AmountPaid = plan.Price,
                Currency = plan.Currency
            };

            _context.UserSubscriptions.Add(subscription);

            // Assegna ruolo PremiumUser se non ce l'ha
            var premiumRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "PremiumUser");
            if (premiumRole != null)
            {
                var hasRole = await _context.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == premiumRole.Id);
                if (!hasRole)
                {
                    _context.UserRoles.Add(new UserRole
                    {
                        UserId = userId,
                        RoleId = premiumRole.Id,
                        AssignedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Utente {UserId} ha sottoscritto il piano {PlanName}", userId, plan.Name);

            subscription.Plan = plan;
            return Ok(MapToSubscriptionDto(subscription));
        }

        /// <summary>
        /// Cancella l'abbonamento attivo
        /// </summary>
        [HttpPost("cancel")]
        public async Task<IActionResult> CancelSubscription([FromBody] CancelSubscriptionRequest? request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var subscription = await _context.UserSubscriptions
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active && s.EndDate > DateTime.UtcNow);

            if (subscription == null)
            {
                return NotFound(new { message = "Nessun abbonamento attivo da cancellare" });
            }

            subscription.Status = SubscriptionStatus.Cancelled;
            subscription.CancelledAt = DateTime.UtcNow;
            subscription.AutoRenew = false;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Utente {UserId} ha cancellato l'abbonamento {SubscriptionId}", userId, subscription.Id);

            return Ok(new
            {
                message = "Abbonamento cancellato. Rimarrà attivo fino alla scadenza.",
                activeUntil = subscription.EndDate
            });
        }

        /// <summary>
        /// Storico abbonamenti dell'utente
        /// </summary>
        [HttpGet("history")]
        public async Task<ActionResult<IEnumerable<UserSubscriptionDto>>> GetSubscriptionHistory()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var subscriptions = await _context.UserSubscriptions
                .AsNoTracking()
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.StartDate)
                .ToListAsync();

            return Ok(subscriptions.Select(MapToSubscriptionDto));
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
        }

        private static SubscriptionPlanDto MapToPlanDto(SubscriptionPlan plan) => new()
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
            HasWishlistAlerts = plan.HasWishlistAlerts
        };

        private static UserSubscriptionDto MapToSubscriptionDto(UserSubscription sub) => new()
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
            Plan = sub.Plan != null ? MapToPlanDto(sub.Plan) : null
        };
    }
}
