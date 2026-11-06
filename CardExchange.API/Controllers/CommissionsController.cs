using System.Security.Claims;
using CardExchange.API.Services.Billing;
using CardExchange.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Controllers
{
    /// <summary>Guadagni del negozio dalle commissioni sul Premium dei clienti portati, e liquidazione mensile.</summary>
    [ApiController]
    [Authorize]
    public class CommissionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly StoreCommissions _commissions;

        public CommissionsController(ApplicationDbContext context, StoreCommissions commissions)
        {
            _context = context;
            _commissions = commissions;
        }

        /// <summary>Maturate, disponibili, accreditate: solo importi e date, mai chi si è abbonato.</summary>
        [HttpGet("api/stores/{storeId:int}/commissions")]
        public async Task<IActionResult> ForStore(int storeId)
        {
            var store = await _context.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == storeId);
            if (store == null) return NotFound(new { message = "Negozio non trovato" });
            var me = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
            if (store.OwnerId != me && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin")) return Forbid();
            var s = await _commissions.SummaryAsync(storeId, DateTime.UtcNow);
            return Ok(new
            {
                rate = StoreCommissions.Rate,
                maturingEur = s.MaturingEur,
                availableEur = s.AvailableEur,
                creditedEur = s.CreditedEur,
                thisMonthEur = s.ThisMonthEur,
                thisMonthCount = s.ThisMonthCount,
                recent = s.Recent.Select(r => new { paidAt = r.PaidAt, amountEur = r.AmountEur, status = r.Status.ToString() }),
                payouts = s.Payouts.Select(p => new { p.Id, month = p.PeriodMonth.ToString("yyyy-MM"), p.AmountEur, method = p.Method.ToString(), status = p.Status.ToString() })
            });
        }

        /// <summary>Liquidazione mensile manuale (di solito parte da sola il primo del mese).</summary>
        [HttpPost("api/admin/payouts/run")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Run([FromServices] IStripeBilling stripe, CancellationToken ct)
        {
            var payouts = await _commissions.PayoutAsync(DateTime.UtcNow, stripe, ct);
            return Ok(new { count = payouts.Count, totalEur = payouts.Sum(p => p.AmountEur), pending = payouts.Count(p => p.Status == Core.Entities.PayoutStatus.Pending) });
        }
    }
}
