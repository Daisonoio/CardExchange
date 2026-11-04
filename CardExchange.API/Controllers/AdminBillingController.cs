using CardExchange.API.Services.Billing;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CardExchange.API.Controllers
{
    /// <summary>
    /// Gestione amministrativa degli abbonamenti alla piattaforma: conferma dei bonifici ricevuti,
    /// attivazioni manuali (es. negozi con fattura) e revoche.
    /// </summary>
    [ApiController]
    [Route("api/admin/billing")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class AdminBillingController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IBillingService _billing;

        public AdminBillingController(ApplicationDbContext context, IBillingService billing)
        {
            _context = context;
            _billing = billing;
        }

        /// <summary>Elenco abbonamenti. status: Pending, Active, Expired, Cancelled; q cerca per utente o causale.</summary>
        [HttpGet("subscriptions")]
        public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? q, [FromQuery] int page = 1)
        {
            var query = _context.UserSubscriptions.AsNoTracking().Include(s => s.Plan).Include(s => s.User).AsQueryable();
            if (!string.IsNullOrWhiteSpace(status))
            {
                var parsed = status.Equals("Pending", StringComparison.OrdinalIgnoreCase)
                    ? SubscriptionStatus.PendingPayment
                    : Enum.TryParse<SubscriptionStatus>(status, true, out var st) ? st : (SubscriptionStatus?)null;
                if (parsed == null) return BadRequest(new { message = "Stato non valido" });
                query = query.Where(s => s.Status == parsed);
            }
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(s => s.User.Username.Contains(term) || s.User.Email.Contains(term)
                                         || (s.PaymentReference != null && s.PaymentReference.Contains(term)));
            }

            page = Math.Max(1, page);
            const int pageSize = 50;
            var total = await query.CountAsync();
            var items = await query.OrderByDescending(s => s.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return Ok(new
            {
                total,
                page,
                items = items.Select(s => new
                {
                    s.Id,
                    s.UserId,
                    username = s.User.Username,
                    email = s.User.Email,
                    planId = s.PlanId,
                    planName = s.Plan.Name,
                    tier = s.Plan.Tier.ToString(),
                    price = s.Plan.Price,
                    status = s.Status.ToString(),
                    paymentMethod = s.PaymentMethod.ToString(),
                    s.PaymentReference,
                    s.AmountPaid,
                    s.Currency,
                    s.StartDate,
                    s.EndDate,
                    s.AutoRenew,
                    s.AdminNote,
                    s.CreatedAt
                })
            });
        }

        public class ConfirmRequest
        {
            /// <summary>Importo effettivamente ricevuto; se assente si usa il prezzo del piano.</summary>
            public decimal? AmountPaid { get; set; }
            public string? Note { get; set; }
        }

        /// <summary>Registra il bonifico ricevuto e attiva il piano (in coda a un eventuale periodo già attivo).</summary>
        [HttpPost("subscriptions/{id:int}/confirm")]
        public async Task<IActionResult> Confirm(int id, [FromBody] ConfirmRequest? request)
        {
            var pending = await _context.UserSubscriptions.Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == id);
            if (pending == null) return NotFound(new { message = "Richiesta non trovata" });
            if (pending.Status != SubscriptionStatus.PendingPayment)
                return BadRequest(new { message = "La richiesta non è in attesa di pagamento" });
            if (request?.AmountPaid is < 0) return BadRequest(new { message = "Importo non valido" });

            var sub = await _billing.ConfirmPendingAsync(pending, CurrentUserId(), request?.AmountPaid, Trim(request?.Note));
            return Ok(new { message = "Pagamento registrato, piano attivo", sub.Id, sub.StartDate, sub.EndDate });
        }

        public class GrantRequest
        {
            /// <summary>Username o email dell'utente.</summary>
            public string User { get; set; } = string.Empty;
            public int PlanId { get; set; }
            /// <summary>Scadenza personalizzata (es. periodo di prova); se assente si usa la durata del piano.</summary>
            public DateTime? EndDate { get; set; }
            public decimal AmountPaid { get; set; }
            public string? PaymentReference { get; set; }
            public string? Note { get; set; }
        }

        /// <summary>Attivazione manuale: pagamento ricevuto fuori dall'app (fattura, contanti in fiera) o omaggio.</summary>
        [HttpPost("grant")]
        public async Task<IActionResult> Grant([FromBody] GrantRequest request)
        {
            var key = request.User?.Trim() ?? string.Empty;
            if (key.Length == 0) return BadRequest(new { message = "Indica username o email" });
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == key || u.Email == key);
            if (user == null) return NotFound(new { message = "Utente non trovato" });
            var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == request.PlanId);
            if (plan == null) return NotFound(new { message = "Piano non trovato" });
            if (request.AmountPaid < 0) return BadRequest(new { message = "Importo non valido" });
            if (request.EndDate.HasValue && request.EndDate.Value <= DateTime.UtcNow)
                return BadRequest(new { message = "La scadenza deve essere futura" });

            var sub = await _billing.ActivateAsync(new ActivationRequest(
                user.Id, plan, SubscriptionPaymentMethod.Manual, request.AmountPaid,
                Trim(request.PaymentReference), request.EndDate?.ToUniversalTime(), CurrentUserId(), Trim(request.Note)));
            return Ok(new { message = $"{plan.Name} attivato per {user.Username}", sub.Id, sub.StartDate, sub.EndDate });
        }

        public class RevokeRequest
        {
            public string? Note { get; set; }
        }

        /// <summary>Revoca immediata (es. rimborso, errore di attivazione) o rifiuto di una richiesta di bonifico.</summary>
        [HttpPost("subscriptions/{id:int}/revoke")]
        public async Task<IActionResult> Revoke(int id, [FromBody] RevokeRequest? request)
        {
            var sub = await _context.UserSubscriptions.FirstOrDefaultAsync(s => s.Id == id);
            if (sub == null) return NotFound(new { message = "Abbonamento non trovato" });
            if (sub.Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
                return BadRequest(new { message = "L'abbonamento non è attivo" });

            await _billing.RevokeAsync(sub, CurrentUserId(), Trim(request?.Note));
            return Ok(new { message = "Abbonamento revocato" });
        }

        private int CurrentUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        private static string? Trim(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            return s.Length > 450 ? s[..450] : s;
        }
    }
}
