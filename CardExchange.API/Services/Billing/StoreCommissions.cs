using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Billing
{
    public record CommissionSummary(decimal MaturingEur, decimal AvailableEur, decimal CreditedEur, decimal ThisMonthEur, int ThisMonthCount,
        IReadOnlyList<(DateTime PaidAt, decimal AmountEur, CommissionStatus Status)> Recent, IReadOnlyList<StorePayout> Payouts);

    /// <summary>
    /// Commissione ai negozi sul Premium: il 25% (al netto dell'IVA) dei pagamenti Premium degli utenti iscritti
    /// dal QR del negozio, per 12 mesi dall'iscrizione. Matura dopo 30 giorni (rimborsi), si liquida ogni mese
    /// come sconto sul piano del negozio.
    /// </summary>
    public class StoreCommissions
    {
        public const decimal Rate = 0.25m;
        public static readonly TimeSpan HoldPeriod = TimeSpan.FromDays(30);
        public const int AttributionMonths = 12;
        /// <summary>IVA italiana, per scorporare gli importi incassati con bonifico.</summary>
        public const decimal VatRate = 0.22m;

        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;
        private readonly ILogger<StoreCommissions> _logger;

        public StoreCommissions(ApplicationDbContext db, INotificationService notifications, ILogger<StoreCommissions> logger)
        {
            _db = db;
            _notifications = notifications;
            _logger = logger;
        }

        /// <summary>Un pagamento Premium è stato incassato: crea la commissione se l'utente è stato portato da un negozio.</summary>
        public async Task<StoreCommission?> OnPremiumPaidAsync(int userSubscriptionId, string paymentReference, string? paymentIntentId,
            decimal grossEurExVat, DateTime paidAtUtc)
        {
            if (grossEurExVat <= 0 || string.IsNullOrWhiteSpace(paymentReference)) return null;
            if (await _db.StoreCommissions.AnyAsync(c => c.PaymentReference == paymentReference)) return null;
            var sub = await _db.UserSubscriptions.AsNoTracking().Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == userSubscriptionId);
            // Solo il Premium dei collezionisti (non i piani dei negozi o i premi inviti)
            if (sub == null || sub.Plan.Tier != SubscriptionTier.Premium || sub.PaymentMethod == SubscriptionPaymentMethod.Reward) return null;
            var attribution = await _db.StoreAttributions.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == sub.UserId);
            // Le attribuzioni manuali non sono approvate automaticamente
            if (attribution == null || attribution.Source != AttributionSource.Qr) return null;
            if (paidAtUtc > attribution.CreatedAt.AddMonths(AttributionMonths)) return null;
            var store = await _db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == attribution.StoreId);
            if (store == null || store.OwnerId == sub.UserId) return null;

            var commission = new StoreCommission
            {
                StoreId = store.Id, UserSubscriptionId = sub.Id, PaymentReference = paymentReference, PaymentIntentId = paymentIntentId,
                GrossEur = grossEurExVat, Rate = Rate, AmountEur = Math.Round(grossEurExVat * Rate, 2),
                Status = CommissionStatus.Pending, PaidAt = paidAtUtc, AvailableAt = paidAtUtc + HoldPeriod
            };
            _db.StoreCommissions.Add(commission);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return null; // stesso pagamento registrato in parallelo
            }
            return commission;
        }

        /// <summary>Rimborso o storno del pagamento: la commissione non ancora liquidata viene annullata.</summary>
        public async Task<bool> ReverseAsync(string paymentIntentId)
        {
            var list = await _db.StoreCommissions
                .Where(c => c.PaymentIntentId == paymentIntentId && (c.Status == CommissionStatus.Pending || c.Status == CommissionStatus.Approved))
                .ToListAsync();
            foreach (var c in list) c.Status = CommissionStatus.Reversed;
            if (list.Count > 0) await _db.SaveChangesAsync();
            return list.Count > 0;
        }

        /// <summary>Ogni notte: le commissioni con più di 30 giorni diventano disponibili.</summary>
        public async Task<int> ApproveAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var due = await _db.StoreCommissions.Where(c => c.Status == CommissionStatus.Pending && c.AvailableAt <= nowUtc).ToListAsync(ct);
            foreach (var c in due) c.Status = CommissionStatus.Approved;
            if (due.Count > 0) await _db.SaveChangesAsync(ct);
            return due.Count;
        }

        /// <summary>
        /// Liquidazione mensile: per ogni negozio le commissioni disponibili diventano uno sconto sul piano
        /// (saldo a credito sul conto Stripe del proprietario). Senza conto Stripe resta da applicare a mano.
        /// </summary>
        public async Task<List<StorePayout>> PayoutAsync(DateTime nowUtc, IStripeBilling? stripe, CancellationToken ct = default)
        {
            await ApproveAsync(nowUtc, ct);
            var month = DateOnly.FromDateTime(TimeZones.ToLocal(nowUtc, null)) is var d ? new DateOnly(d.Year, d.Month, 1) : default;
            var approved = await _db.StoreCommissions.Where(c => c.Status == CommissionStatus.Approved).ToListAsync(ct);
            var payouts = new List<StorePayout>();
            foreach (var group in approved.GroupBy(c => c.StoreId))
            {
                var amount = group.Sum(c => c.AmountEur);
                if (amount <= 0) continue;
                var store = await _db.Stores.AsNoTracking().FirstAsync(s => s.Id == group.Key, ct);
                var payout = new StorePayout { StoreId = store.Id, Method = PayoutMethod.PlanCredit, AmountEur = amount, PeriodMonth = month };
                _db.StorePayouts.Add(payout);
                await _db.SaveChangesAsync(ct);
                foreach (var c in group) { c.Status = CommissionStatus.Credited; c.PayoutId = payout.Id; }

                var customerId = await _db.UserSubscriptions.Where(s => s.UserId == store.OwnerId && s.ExternalCustomerId != null)
                    .OrderByDescending(s => s.CreatedAt).Select(s => s.ExternalCustomerId).FirstOrDefaultAsync(ct);
                if (stripe is { IsEnabled: true } && customerId != null)
                {
                    try
                    {
                        payout.Reference = await stripe.CreditCustomerAsync(customerId, amount, $"Commissioni CardExchange {month:yyyy-MM}");
                        payout.Status = PayoutStatus.Done;
                    }
                    catch (Exception ex) when (ex is Stripe.StripeException or HttpRequestException)
                    {
                        _logger.LogError(ex, "Sconto commissioni non registrato su Stripe per il negozio {StoreId}", store.Id);
                    }
                }
                await _db.SaveChangesAsync(ct);
                payouts.Add(payout);
                var count = group.Count();
                try
                {
                    await _notifications.SendAsync(store.OwnerId, NotificationType.StoreActivity, "Commissioni del mese",
                        $"Hai maturato {amount:0.00} € da {count} {(count == 1 ? "pagamento" : "pagamenti")} Premium dei clienti portati da te: li scaliamo dalla prossima fattura del tuo piano.",
                        store.Id, "StoreEarnings");
                }
                catch (Exception ex) { _logger.LogError(ex, "Notifica commissioni non inviata al negozio {StoreId}", store.Id); }
            }
            return payouts;
        }

        public async Task<CommissionSummary> SummaryAsync(int storeId, DateTime nowUtc)
        {
            var list = await _db.StoreCommissions.AsNoTracking().Where(c => c.StoreId == storeId).OrderByDescending(c => c.PaidAt).ToListAsync();
            var monthStart = DateTime.SpecifyKind(new DateTime(nowUtc.Year, nowUtc.Month, 1), DateTimeKind.Utc);
            var thisMonth = list.Where(c => c.PaidAt >= monthStart && c.Status != CommissionStatus.Reversed).ToList();
            var payouts = await _db.StorePayouts.AsNoTracking().Where(p => p.StoreId == storeId).OrderByDescending(p => p.CreatedAt).Take(12).ToListAsync();
            return new CommissionSummary(
                list.Where(c => c.Status == CommissionStatus.Pending).Sum(c => c.AmountEur),
                list.Where(c => c.Status == CommissionStatus.Approved).Sum(c => c.AmountEur),
                list.Where(c => c.Status is CommissionStatus.Credited or CommissionStatus.Paid).Sum(c => c.AmountEur),
                thisMonth.Sum(c => c.AmountEur), thisMonth.Count,
                list.Take(50).Select(c => (c.PaidAt, c.AmountEur, c.Status)).ToList(), payouts);
        }
    }
}
