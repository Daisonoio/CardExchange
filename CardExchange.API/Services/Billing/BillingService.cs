using System.Security.Cryptography;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Billing
{
    public record ActivationRequest(
        int UserId,
        SubscriptionPlan Plan,
        SubscriptionPaymentMethod Method,
        decimal AmountPaid,
        string? PaymentReference = null,
        DateTime? EndDate = null,
        int? AdminId = null,
        string? AdminNote = null,
        string? ExternalCustomerId = null,
        string? ExternalSubscriptionId = null,
        bool AutoRenew = false);

    public interface IBillingService
    {
        /// <summary>Attiva un abbonamento. Senza rinnovo automatico si accoda a quello già attivo (non si perdono giorni).</summary>
        Task<UserSubscription> ActivateAsync(ActivationRequest request);
        /// <summary>Conferma una richiesta di bonifico in attesa.</summary>
        Task<UserSubscription> ConfirmPendingAsync(UserSubscription pending, int adminId, decimal? amountPaid, string? note);
        Task RevokeAsync(UserSubscription subscription, int adminId, string? note);
        /// <summary>Allinea il ruolo PremiumUser agli abbonamenti attivi.</summary>
        Task SyncPremiumRoleAsync(int userId);
        /// <summary>Scadenze, promemoria e pulizia delle richieste di bonifico vecchie (lavoro notturno).</summary>
        Task<(int Expired, int Reminded, int StaleRequests)> RunMaintenanceAsync(DateTime nowUtc);
        string NewBankTransferReference(int userId);
    }

    public class BillingService : IBillingService
    {
        public static readonly TimeSpan ReminderBefore = TimeSpan.FromDays(7);
        public static readonly TimeSpan PendingRequestLifetime = TimeSpan.FromDays(30);

        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notifications;
        private readonly ILogger<BillingService> _logger;

        public BillingService(ApplicationDbContext context, INotificationService notifications, ILogger<BillingService> logger)
        {
            _context = context;
            _notifications = notifications;
            _logger = logger;
        }

        public static DateTime AddCycle(DateTime start, BillingCycle cycle) =>
            cycle == BillingCycle.Annual ? start.AddYears(1) : start.AddMonths(1);

        public async Task<UserSubscription> ActivateAsync(ActivationRequest r)
        {
            var now = DateTime.UtcNow;
            var start = now;
            if (!r.AutoRenew)
            {
                // I giorni già pagati non si perdono: il nuovo periodo parte dalla fine di quello attivo
                // Si accoda solo a un piano dello stesso tipo: un passaggio a un livello diverso parte subito
                var currentEnd = await _context.UserSubscriptions
                    .Where(s => s.UserId == r.UserId && s.Status == SubscriptionStatus.Active && s.EndDate > now && s.Plan.Tier == r.Plan.Tier)
                    .Select(s => (DateTime?)s.EndDate)
                    .MaxAsync();
                if (currentEnd > start) start = currentEnd.Value;
            }

            var subscription = new UserSubscription
            {
                UserId = r.UserId,
                PlanId = r.Plan.Id,
                Status = SubscriptionStatus.Active,
                StartDate = start,
                EndDate = r.EndDate ?? AddCycle(start, r.Plan.Cycle),
                AutoRenew = r.AutoRenew,
                PaymentMethod = r.Method,
                PaymentReference = r.PaymentReference,
                AmountPaid = r.AmountPaid,
                Currency = r.Plan.Currency,
                ActivatedById = r.AdminId,
                AdminNote = r.AdminNote,
                ExternalCustomerId = r.ExternalCustomerId,
                ExternalSubscriptionId = r.ExternalSubscriptionId
            };
            _context.UserSubscriptions.Add(subscription);
            await _context.SaveChangesAsync();
            await SyncPremiumRoleAsync(r.UserId);
            await NotifyActivatedAsync(subscription, r.Plan);

            _logger.LogInformation("Abbonamento {SubscriptionId} ({Plan}) attivato per utente {UserId} via {Method} fino al {End:d}",
                subscription.Id, r.Plan.Name, r.UserId, r.Method, subscription.EndDate);
            return subscription;
        }

        public async Task<UserSubscription> ConfirmPendingAsync(UserSubscription pending, int adminId, decimal? amountPaid, string? note)
        {
            if (pending.Status != SubscriptionStatus.PendingPayment)
                throw new InvalidOperationException("La richiesta non è in attesa di pagamento");
            var plan = pending.Plan ?? await _context.SubscriptionPlans.FirstAsync(p => p.Id == pending.PlanId);

            var now = DateTime.UtcNow;
            var currentEnd = await _context.UserSubscriptions
                .Where(s => s.UserId == pending.UserId && s.Status == SubscriptionStatus.Active && s.EndDate > now && s.Plan.Tier == plan.Tier)
                .Select(s => (DateTime?)s.EndDate)
                .MaxAsync();
            var start = currentEnd > now ? currentEnd.Value : now;

            pending.Status = SubscriptionStatus.Active;
            pending.StartDate = start;
            pending.EndDate = AddCycle(start, plan.Cycle);
            pending.AmountPaid = amountPaid ?? plan.Price;
            pending.ActivatedById = adminId;
            pending.AdminNote = note;
            pending.AutoRenew = false;
            await _context.SaveChangesAsync();
            await SyncPremiumRoleAsync(pending.UserId);
            await NotifyActivatedAsync(pending, plan);
            return pending;
        }

        public async Task RevokeAsync(UserSubscription subscription, int adminId, string? note)
        {
            subscription.Status = SubscriptionStatus.Cancelled;
            subscription.CancelledAt = DateTime.UtcNow;
            if (subscription.EndDate > DateTime.UtcNow) subscription.EndDate = DateTime.UtcNow;
            subscription.AutoRenew = false;
            subscription.AdminNote = string.Join(" | ", new[] { subscription.AdminNote, $"Revocato: {note}" }.Where(x => !string.IsNullOrWhiteSpace(x)));
            await _context.SaveChangesAsync();
            await SyncPremiumRoleAsync(subscription.UserId);
            _logger.LogWarning("Abbonamento {SubscriptionId} revocato da {AdminId}", subscription.Id, adminId);
        }

        public async Task SyncPremiumRoleAsync(int userId)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "PremiumUser");
            if (role == null) return;
            var now = DateTime.UtcNow;
            var hasActive = await _context.UserSubscriptions.AnyAsync(s =>
                s.UserId == userId && s.Status == SubscriptionStatus.Active && s.StartDate <= now && s.EndDate > now);
            var assignment = await _context.UserRoles.FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == role.Id);

            if (hasActive && assignment == null)
                _context.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id, AssignedAt = now });
            else if (!hasActive && assignment != null)
                _context.UserRoles.Remove(assignment);
            await _context.SaveChangesAsync();
        }

        public async Task<(int Expired, int Reminded, int StaleRequests)> RunMaintenanceAsync(DateTime nowUtc)
        {
            var expired = await _context.UserSubscriptions
                .Where(s => s.Status == SubscriptionStatus.Active && s.EndDate <= nowUtc)
                .ToListAsync();
            foreach (var s in expired) s.Status = SubscriptionStatus.Expired;

            // Promemoria solo per chi non ha il rinnovo automatico (bonifico o attivazione manuale)
            var expiring = await _context.UserSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.Status == SubscriptionStatus.Active && !s.AutoRenew && s.ExpiryReminderSentAt == null
                            && s.EndDate > nowUtc && s.EndDate <= nowUtc + ReminderBefore)
                .ToListAsync();
            var stale = await _context.UserSubscriptions
                .Where(s => s.Status == SubscriptionStatus.PendingPayment && s.CreatedAt < nowUtc - PendingRequestLifetime)
                .ToListAsync();
            foreach (var s in stale) s.Status = SubscriptionStatus.Cancelled;
            await _context.SaveChangesAsync();

            foreach (var s in expiring)
            {
                // Se c'è già un rinnovo accodato non serve il promemoria
                var renewed = await _context.UserSubscriptions.AnyAsync(o => o.UserId == s.UserId && o.Id != s.Id
                    && o.Status == SubscriptionStatus.Active && o.EndDate > s.EndDate);
                if (!renewed)
                {
                    await TryNotifyAsync(s.UserId, NotificationType.SubscriptionExpiring, $"Il tuo piano {s.Plan.Name} scade presto",
                        $"Scade il {s.EndDate:dd/MM/yyyy}. Puoi rinnovarlo dalla pagina dei piani.");
                }
                s.ExpiryReminderSentAt = nowUtc;
            }
            await _context.SaveChangesAsync();

            foreach (var userId in expired.Select(s => s.UserId).Distinct())
            {
                await SyncPremiumRoleAsync(userId);
                await TryNotifyAsync(userId, NotificationType.SubscriptionExpired, "Abbonamento scaduto",
                    "Il tuo abbonamento è scaduto. Puoi rinnovarlo quando vuoi dalla pagina dei piani.");
            }
            return (expired.Count, expiring.Count, stale.Count);
        }

        public string NewBankTransferReference(int userId)
        {
            const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
            var suffix = string.Create(6, 0, (span, _) =>
            {
                for (int i = 0; i < span.Length; i++) span[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            });
            return $"CX-{userId}-{suffix}";
        }

        private Task NotifyActivatedAsync(UserSubscription s, SubscriptionPlan plan) =>
            TryNotifyAsync(s.UserId, NotificationType.SystemAnnouncement, $"Piano {plan.Name} attivo",
                $"Il tuo piano è attivo fino al {s.EndDate:dd/MM/yyyy}. Grazie per il supporto!");

        private async Task TryNotifyAsync(int userId, NotificationType type, string title, string body)
        {
            try
            {
                await _notifications.SendAsync(userId, type, title, body, null, "Subscription");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notifica abbonamento non inviata a {UserId}", userId);
            }
        }
    }
}
