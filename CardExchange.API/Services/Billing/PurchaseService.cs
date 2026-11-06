using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Billing
{
    public record PurchaseProduct(string Code, PurchaseProductType Type, string Name, int Quantity, decimal PriceEur);

    /// <summary>Listino degli acquisti una tantum.</summary>
    public static class PurchaseCatalog
    {
        public static readonly IReadOnlyList<PurchaseProduct> Products = new[]
        {
            new PurchaseProduct("credits-5", PurchaseProductType.PromotionCredits, "5 crediti promozione", 5, 20m),
            new PurchaseProduct("credits-15", PurchaseProductType.PromotionCredits, "15 crediti promozione", 15, 50m),
        };

        public static PurchaseProduct? Find(string? code) => Products.FirstOrDefault(p => p.Code == code);
    }

    public class PurchaseException : Exception
    {
        public PurchaseException(string message) : base(message) { }
    }

    /// <summary>
    /// Acquisti una tantum: creazione (in attesa), attivazione dal webhook (idempotente), rimborso.
    /// Crediti promozione in un registro di movimenti: il saldo è la somma.
    /// </summary>
    public class PurchaseService
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;
        private readonly ILogger<PurchaseService> _logger;

        public PurchaseService(ApplicationDbContext db, INotificationService notifications, ILogger<PurchaseService> logger)
        {
            _db = db;
            _notifications = notifications;
            _logger = logger;
        }

        public Task<int> CreditBalanceAsync(int userId) =>
            _db.PromotionCreditEntries.Where(e => e.UserId == userId).SumAsync(e => (int?)e.Delta).ContinueWith(t => t.Result ?? 0);

        public async Task<Purchase> CreatePendingAsync(int userId, string productCode, int? refId = null)
        {
            var product = PurchaseCatalog.Find(productCode) ?? throw new PurchaseException("Prodotto non trovato");
            var purchase = new Purchase
            {
                UserId = userId, ProductType = product.Type, ProductCode = product.Code, Quantity = product.Quantity,
                RefId = refId, AmountEur = product.PriceEur, Status = PurchaseStatus.Pending
            };
            _db.Purchases.Add(purchase);
            await _db.SaveChangesAsync();
            return purchase;
        }

        /// <summary>Pagamento confermato da Stripe: attiva il prodotto una sola volta, anche se l'evento arriva due volte.</summary>
        public async Task<bool> MarkPaidAsync(int purchaseId, string? sessionId, string? paymentIntentId, decimal? amountEur, string? invoiceNumber)
        {
            var purchase = await _db.Purchases.FirstOrDefaultAsync(p => p.Id == purchaseId);
            if (purchase == null)
            {
                _logger.LogError("Pagamento Stripe per un acquisto inesistente ({PurchaseId})", purchaseId);
                return false;
            }
            if (purchase.Status == PurchaseStatus.Paid || purchase.Status == PurchaseStatus.Refunded) return true;
            purchase.Status = PurchaseStatus.Paid;
            purchase.PaidAt = DateTime.UtcNow;
            purchase.StripeSessionId ??= sessionId;
            purchase.StripePaymentIntentId ??= paymentIntentId;
            purchase.InvoiceNumber ??= invoiceNumber;
            if (amountEur.HasValue && amountEur.Value != purchase.AmountEur)
                _logger.LogWarning("Importo pagato {Paid} diverso dal listino {Price} per l'acquisto {Id}", amountEur, purchase.AmountEur, purchase.Id);

            switch (purchase.ProductType)
            {
                case PurchaseProductType.PromotionCredits:
                    _db.PromotionCreditEntries.Add(new PromotionCreditEntry
                    {
                        UserId = purchase.UserId, Delta = purchase.Quantity, Reason = $"Acquisto {purchase.ProductCode}",
                        PurchaseId = purchase.Id, CreatedAt = DateTime.UtcNow
                    });
                    break;
                // Promozioni (F12) e pacchetti organizzatori (F14) si attivano qui quando esisteranno
            }
            await _db.SaveChangesAsync();
            await NotifyAsync(purchase.UserId, "Pagamento ricevuto",
                purchase.ProductType == PurchaseProductType.PromotionCredits
                    ? $"Abbiamo aggiunto {purchase.Quantity} crediti promozione al tuo account. La ricevuta arriva via email."
                    : "Il tuo acquisto è attivo. La ricevuta arriva via email.");
            return true;
        }

        /// <summary>Rimborso (totale o parziale): l'acquisto si ferma e i crediti non usati vengono tolti, in proporzione.</summary>
        public async Task<bool> MarkRefundedAsync(string paymentIntentId, long amountRefundedCents, bool fullyRefunded)
        {
            var purchase = await _db.Purchases.FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId);
            if (purchase == null) return false;
            if (purchase.Status == PurchaseStatus.Refunded) return true;
            if (purchase.ProductType == PurchaseProductType.PromotionCredits)
            {
                var share = fullyRefunded ? 1m : Math.Min(1m, amountRefundedCents / 100m / Math.Max(0.01m, purchase.AmountEur));
                var toRemove = (int)Math.Ceiling(purchase.Quantity * share);
                // Già tolti per rimborsi parziali precedenti di questo acquisto
                var removed = -(await _db.PromotionCreditEntries.Where(e => e.PurchaseId == purchase.Id && e.Delta < 0).SumAsync(e => (int?)e.Delta) ?? 0);
                var balance = await CreditBalanceAsync(purchase.UserId);
                var delta = Math.Min(Math.Max(0, toRemove - removed), balance);
                if (delta > 0)
                    _db.PromotionCreditEntries.Add(new PromotionCreditEntry
                    {
                        UserId = purchase.UserId, Delta = -delta, Reason = $"Rimborso {purchase.ProductCode}",
                        PurchaseId = purchase.Id, CreatedAt = DateTime.UtcNow
                    });
            }
            if (fullyRefunded)
            {
                purchase.Status = PurchaseStatus.Refunded;
                purchase.RefundedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task MarkExpiredAsync(int purchaseId)
        {
            var purchase = await _db.Purchases.FirstOrDefaultAsync(p => p.Id == purchaseId && p.Status == PurchaseStatus.Pending);
            if (purchase == null) return;
            purchase.Status = PurchaseStatus.Expired;
            await _db.SaveChangesAsync();
        }

        private async Task NotifyAsync(int userId, string title, string body)
        {
            try { await _notifications.SendAsync(userId, NotificationType.SystemAnnouncement, title, body, null, "Purchase"); }
            catch (Exception ex) { _logger.LogError(ex, "Notifica acquisto non inviata a {UserId}", userId); }
        }
    }
}
