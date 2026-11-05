using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services
{
    public record ReferralProgress(int Qualified, int TowardsNext, int PerReward, int MonthsEarned, int MonthsThisYear, int MaxMonthsPerYear);

    /// <summary>
    /// Inviti con premio: un invitato diventa "attivo" con email confermata, almeno 10 carte tra collezione e wishlist
    /// e l'app usata in almeno 3 giorni diversi entro 30 giorni dall'iscrizione. Ogni 3 attivi: 1 mese Premium,
    /// al massimo 6 in 12 mesi. Email usa e getta escluse; più iscrizioni dallo stesso posto nello stesso giorno contano una volta.
    /// </summary>
    public class ReferralRewards
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;
        private readonly ILogger<ReferralRewards> _logger;

        public ReferralRewards(ApplicationDbContext db, INotificationService notifications, ILogger<ReferralRewards> logger)
        {
            _db = db;
            _notifications = notifications;
            _logger = logger;
        }

        /// <summary>Invitati attivi di un utente, contando una volta quelli iscritti dallo stesso posto nello stesso giorno.</summary>
        public async Task<int> CountQualifiedAsync(int referrerId, CancellationToken ct = default)
        {
            var qualified = await _db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(u => u.ReferredByUserId == referrerId && u.ReferralQualifiedAt != null)
                .Select(u => new { u.Id, u.SignupFingerprint })
                .ToListAsync(ct);
            return qualified.Select(u => u.SignupFingerprint ?? $"user:{u.Id}").Distinct().Count();
        }

        public async Task<ReferralProgress> ProgressAsync(int userId, DateTime nowUtc, CancellationToken ct = default)
        {
            var qualified = await CountQualifiedAsync(userId, ct);
            var granted = await _db.UserSubscriptions.AsNoTracking()
                .Where(s => s.UserId == userId && s.PaymentMethod == SubscriptionPaymentMethod.Reward)
                .Select(s => s.CreatedAt).ToListAsync(ct);
            return new ReferralProgress(qualified, qualified % ReferralRules.PerReward, ReferralRules.PerReward, granted.Count,
                granted.Count(d => d >= nowUtc.AddDays(-365)), ReferralRules.MaxMonthsPerYear);
        }

        public static bool IsQualifyingNow(User u, int cards, int activeDays) =>
            u.EmailConfirmed && !ReferralRules.IsDisposableEmail(u.Email)
            && cards >= ReferralRules.MinCards && activeDays >= ReferralRules.MinActiveDays;

        /// <summary>Controllo periodico: segna i nuovi invitati attivi e assegna i mesi Premium guadagnati.</summary>
        public async Task<(int Qualified, int MonthsGranted)> RunAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var since = nowUtc.AddDays(-ReferralRules.QualifyWithinDays - 1);
            var candidates = await _db.Users
                .Where(u => u.ReferredByUserId != null && u.ReferralQualifiedAt == null && u.CreatedAt >= since && u.EmailConfirmed)
                .ToListAsync(ct);

            var newlyQualified = new List<User>();
            foreach (var u in candidates)
            {
                var deadline = u.CreatedAt.AddDays(ReferralRules.QualifyWithinDays);
                if (nowUtc > deadline) continue;
                var cards = await _db.Cards.Where(c => c.UserId == u.Id).SumAsync(c => (int?)c.Quantity, ct) ?? 0;
                cards += await _db.WishlistItems.CountAsync(w => w.UserId == u.Id, ct);
                var lastDay = DateOnly.FromDateTime(deadline);
                var activeDays = await _db.UserActiveDays.CountAsync(d => d.UserId == u.Id && d.Day <= lastDay, ct);
                if (!IsQualifyingNow(u, cards, activeDays)) continue;
                u.ReferralQualifiedAt = nowUtc;
                newlyQualified.Add(u);
            }
            if (newlyQualified.Count == 0) return (0, 0);
            await _db.SaveChangesAsync(ct);

            var plan = await _db.SubscriptionPlans
                .Where(p => p.IsActive && p.Tier == SubscriptionTier.Premium)
                .OrderBy(p => p.Cycle == BillingCycle.Monthly ? 0 : 1).ThenBy(p => p.Price)
                .FirstOrDefaultAsync(ct);

            var months = 0;
            foreach (var group in newlyQualified.GroupBy(u => u.ReferredByUserId!.Value))
            {
                var referrerId = group.Key;
                var progress = await ProgressAsync(referrerId, nowUtc, ct);
                var earned = progress.Qualified / ReferralRules.PerReward;
                var toGrant = Math.Min(earned - progress.MonthsEarned, ReferralRules.MaxMonthsPerYear - progress.MonthsThisYear);
                var name = group.Count() == 1 ? "@" + group.First().Username : $"{group.Count()} tuoi invitati";

                if (toGrant > 0 && plan != null)
                {
                    for (var i = 0; i < toGrant; i++) await GrantMonthAsync(referrerId, plan, nowUtc, ct);
                    months += toGrant;
                    await NotifyAsync(referrerId, "Hai guadagnato un mese Premium!",
                        $"{name} {(group.Count() == 1 ? "è diventato attivo" : "sono diventati attivi")}: ecco {(toGrant == 1 ? "un mese" : $"{toGrant} mesi")} di Premium in regalo. Grazie per far crescere CardExchange nella tua zona.");
                }
                else
                {
                    var missing = ReferralRules.PerReward - progress.TowardsNext;
                    var capped = progress.MonthsThisYear >= ReferralRules.MaxMonthsPerYear;
                    await NotifyAsync(referrerId, "Un tuo invitato è diventato attivo",
                        capped
                            ? $"{name} ora usa CardExchange. Hai già ricevuto i {ReferralRules.MaxMonthsPerYear} mesi Premium di quest'anno: grazie!"
                            : $"{name} ora usa CardExchange: ancora {missing} {(missing == 1 ? "invito" : "inviti")} per il tuo mese Premium gratuito.");
                }
            }
            await _db.SaveChangesAsync(ct);
            return (newlyQualified.Count, months);
        }

        /// <summary>Un mese Premium che parte dopo l'ultimo abbonamento a pagamento o premio già attivo.</summary>
        private async Task GrantMonthAsync(int userId, SubscriptionPlan plan, DateTime nowUtc, CancellationToken ct)
        {
            var latestEnd = await _db.UserSubscriptions
                .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active && s.EndDate > nowUtc && s.PlanId == plan.Id)
                .MaxAsync(s => (DateTime?)s.EndDate, ct);
            // Le sottoscrizioni locali appena aggiunte non sono ancora salvate: si guardano anche quelle
            var pending = _db.ChangeTracker.Entries<UserSubscription>()
                .Where(e => e.State == EntityState.Added && e.Entity.UserId == userId && e.Entity.PlanId == plan.Id)
                .Select(e => (DateTime?)e.Entity.EndDate).Max();
            var start = new[] { nowUtc, latestEnd ?? nowUtc, pending ?? nowUtc }.Max();
            _db.UserSubscriptions.Add(new UserSubscription
            {
                UserId = userId,
                PlanId = plan.Id,
                Status = SubscriptionStatus.Active,
                StartDate = start,
                EndDate = start.AddMonths(1),
                AutoRenew = false,
                AmountPaid = 0,
                Currency = "EUR",
                PaymentMethod = SubscriptionPaymentMethod.Reward,
                AdminNote = "Premio inviti"
            });
        }

        private async Task NotifyAsync(int userId, string title, string body)
        {
            try
            {
                await _notifications.SendAsync(userId, NotificationType.SystemAnnouncement, title, body, null, "Referral");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notifica premio inviti non inviata a {UserId}", userId);
            }
        }
    }
}
