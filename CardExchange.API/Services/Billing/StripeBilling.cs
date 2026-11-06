using CardExchange.API.Configuration;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace CardExchange.API.Services.Billing
{
    public enum WebhookResult { Processed, Duplicate, Ignored, InvalidSignature }

    public interface IStripeBilling
    {
        bool IsEnabled { get; }
        /// <summary>Crea una sessione Stripe Checkout per il piano e restituisce l'indirizzo della pagina di pagamento.</summary>
        Task<string> CreateCheckoutAsync(User user, SubscriptionPlan plan, string successUrl, string cancelUrl);
        /// <summary>Portale clienti Stripe: carta, fatture, disdetta.</summary>
        Task<string> CreatePortalAsync(string customerId, string returnUrl);
        /// <summary>Disdice il rinnovo: l'abbonamento resta attivo fino a fine periodo.</summary>
        Task CancelAtPeriodEndAsync(string subscriptionId);
        Task<WebhookResult> HandleWebhookAsync(string json, string? signatureHeader);
        /// <summary>Checkout per un pagamento una tantum (promozioni, crediti, pacchetti), con ricevuta.</summary>
        Task<string> CreatePaymentCheckoutAsync(User user, Purchase purchase, string productName, string successUrl, string cancelUrl);
    }

    /// <summary>
    /// Pagamento degli abbonamenti alla piattaforma con Stripe. I webhook usano solo i dati contenuti negli
    /// eventi (nessuna chiamata all'API Stripe), quindi sono verificabili anche senza rete con eventi firmati.
    /// </summary>
    public class StripeBilling : IStripeBilling
    {
        public const string Provider = "stripe";

        private readonly ApplicationDbContext _context;
        private readonly IBillingService _billing;
        private readonly INotificationService _notifications;
        private readonly StripeOptions _options;
        private readonly ILogger<StripeBilling> _logger;

        private readonly PurchaseService? _purchases;

        public StripeBilling(ApplicationDbContext context, IBillingService billing, INotificationService notifications,
            IOptions<BillingOptions> options, ILogger<StripeBilling> logger, PurchaseService? purchases = null)
        {
            _purchases = purchases;
            _context = context;
            _billing = billing;
            _notifications = notifications;
            _options = options.Value.Stripe;
            _logger = logger;
        }

        public bool IsEnabled => _options.IsConfigured;

        private StripeClient Client() => new(_options.SecretKey);

        public async Task<string> CreateCheckoutAsync(User user, SubscriptionPlan plan, string successUrl, string cancelUrl)
        {
            var metadata = new Dictionary<string, string>
            {
                ["userId"] = user.Id.ToString(),
                ["planId"] = plan.Id.ToString()
            };
            // Un cliente Stripe per utente: riusa quello di un abbonamento precedente
            var customerId = await _context.UserSubscriptions
                .Where(s => s.UserId == user.Id && s.ExternalCustomerId != null)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => s.ExternalCustomerId)
                .FirstOrDefaultAsync();

            var options = new SessionCreateOptions
            {
                Mode = "subscription",
                ClientReferenceId = user.Id.ToString(),
                Customer = customerId,
                CustomerEmail = customerId == null ? user.Email : null,
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                Locale = "it",
                Metadata = metadata,
                SubscriptionData = new SessionSubscriptionDataOptions { Metadata = metadata },
                // Promozioni (es. codice dei partner fondatori: 6 mesi gratis, poi -50%) inseribili nel Checkout
                AllowPromotionCodes = true,
                LineItems = new List<SessionLineItemOptions>
                {
                    // Prezzo creato nella dashboard Stripe, se collegato al piano; altrimenti dal prezzo del piano
                    !string.IsNullOrWhiteSpace(plan.StripePriceId) ? new() { Quantity = 1, Price = plan.StripePriceId } :
                    new()
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = plan.Currency.ToLowerInvariant(),
                            UnitAmount = (long)Math.Round(plan.Price * 100m),
                            Recurring = new SessionLineItemPriceDataRecurringOptions
                            {
                                Interval = plan.Cycle == BillingCycle.Annual ? "year" : "month"
                            },
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"CardExchange {plan.Name}",
                                Metadata = new Dictionary<string, string> { ["planId"] = plan.Id.ToString() }
                            }
                        }
                    }
                }
            };
            var session = await new SessionService(Client()).CreateAsync(options);
            return session.Url;
        }

        public async Task<string> CreatePaymentCheckoutAsync(User user, Purchase purchase, string productName, string successUrl, string cancelUrl)
        {
            var metadata = new Dictionary<string, string> { ["userId"] = user.Id.ToString(), ["purchaseId"] = purchase.Id.ToString() };
            var customerId = await _context.UserSubscriptions
                .Where(s => s.UserId == user.Id && s.ExternalCustomerId != null)
                .OrderByDescending(s => s.CreatedAt).Select(s => s.ExternalCustomerId).FirstOrDefaultAsync();
            var options = new SessionCreateOptions
            {
                Mode = "payment",
                ClientReferenceId = user.Id.ToString(),
                Customer = customerId,
                CustomerEmail = customerId == null ? user.Email : null,
                CustomerCreation = customerId == null ? "always" : null,
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                Locale = "it",
                Metadata = metadata,
                PaymentIntentData = new SessionPaymentIntentDataOptions { Metadata = metadata },
                // Ricevuta e fattura con i dati inseriti nel Checkout
                BillingAddressCollection = "required",
                TaxIdCollection = new SessionTaxIdCollectionOptions { Enabled = true },
                InvoiceCreation = new SessionInvoiceCreationOptions { Enabled = true },
                LineItems = new List<SessionLineItemOptions>
                {
                    new()
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "eur",
                            UnitAmount = (long)Math.Round(purchase.AmountEur * 100m),
                            ProductData = new SessionLineItemPriceDataProductDataOptions { Name = $"CardExchange · {productName}" }
                        }
                    }
                }
            };
            var session = await new SessionService(Client()).CreateAsync(options);
            purchase.StripeSessionId = session.Id;
            await _context.SaveChangesAsync();
            return session.Url;
        }

        public async Task<string> CreatePortalAsync(string customerId, string returnUrl)
        {
            var session = await new Stripe.BillingPortal.SessionService(Client()).CreateAsync(
                new Stripe.BillingPortal.SessionCreateOptions { Customer = customerId, ReturnUrl = returnUrl, Locale = "it" });
            return session.Url;
        }

        public Task CancelAtPeriodEndAsync(string subscriptionId) =>
            new Stripe.SubscriptionService(Client()).UpdateAsync(subscriptionId, new SubscriptionUpdateOptions { CancelAtPeriodEnd = true });

        public async Task<WebhookResult> HandleWebhookAsync(string json, string? signatureHeader)
        {
            if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(json))
                return WebhookResult.InvalidSignature;
            Stripe.Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, _options.WebhookSecret,
                    throwOnApiVersionMismatch: false);
            }
            catch (Exception ex) when (ex is StripeException or Newtonsoft.Json.JsonException or ArgumentException)
            {
                _logger.LogWarning("Webhook Stripe rifiutato: {Message}", ex.Message);
                return WebhookResult.InvalidSignature;
            }

            if (await _context.ProcessedWebhookEvents.AnyAsync(e => e.Provider == Provider && e.EventId == stripeEvent.Id))
                return WebhookResult.Duplicate;

            var handled = stripeEvent.Type switch
            {
                EventTypes.CheckoutSessionCompleted => await OnCheckoutCompletedAsync((Session)stripeEvent.Data.Object),
                EventTypes.CheckoutSessionAsyncPaymentSucceeded => await OnPaymentCheckoutAsync((Session)stripeEvent.Data.Object),
                EventTypes.CheckoutSessionExpired => await OnPaymentExpiredAsync((Session)stripeEvent.Data.Object),
                EventTypes.ChargeRefunded => await OnChargeRefundedAsync((Charge)stripeEvent.Data.Object),
                EventTypes.InvoicePaid => await OnInvoicePaidAsync((Invoice)stripeEvent.Data.Object),
                EventTypes.InvoicePaymentFailed => await OnPaymentFailedAsync((Invoice)stripeEvent.Data.Object),
                EventTypes.CustomerSubscriptionUpdated => await OnSubscriptionUpdatedAsync((Subscription)stripeEvent.Data.Object),
                EventTypes.CustomerSubscriptionDeleted => await OnSubscriptionDeletedAsync((Subscription)stripeEvent.Data.Object),
                _ => false
            };

            _context.ProcessedWebhookEvents.Add(new ProcessedWebhookEvent
            {
                Provider = Provider,
                EventId = stripeEvent.Id,
                EventType = stripeEvent.Type
            });
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Stesso evento consegnato due volte in parallelo: l'indice univoco blocca il secondo
                return WebhookResult.Duplicate;
            }
            return handled ? WebhookResult.Processed : WebhookResult.Ignored;
        }

        /// <summary>Pagamento una tantum: si attiva solo qui, mai dal ritorno del browser.</summary>
        private async Task<bool> OnPaymentCheckoutAsync(Session session)
        {
            if (_purchases == null || session.Mode != "payment") return false;
            if (session.PaymentStatus != "paid") return true; // pagamento differito: arriverà async_payment_succeeded
            if (session.Metadata == null || !session.Metadata.TryGetValue("purchaseId", out var raw) || !int.TryParse(raw, out var purchaseId))
            {
                _logger.LogError("Checkout {Session} senza acquisto nei metadati", session.Id);
                return false;
            }
            return await _purchases.MarkPaidAsync(purchaseId, session.Id, session.PaymentIntentId,
                session.AmountTotal.HasValue ? session.AmountTotal.Value / 100m : null, session.InvoiceId);
        }

        private async Task<bool> OnPaymentExpiredAsync(Session session)
        {
            if (_purchases == null || session.Mode != "payment") return false;
            if (session.Metadata != null && session.Metadata.TryGetValue("purchaseId", out var raw) && int.TryParse(raw, out var purchaseId))
                await _purchases.MarkExpiredAsync(purchaseId);
            return true;
        }

        private async Task<bool> OnChargeRefundedAsync(Charge charge)
        {
            if (_purchases == null || string.IsNullOrEmpty(charge.PaymentIntentId)) return false;
            return await _purchases.MarkRefundedAsync(charge.PaymentIntentId, charge.AmountRefunded, charge.Refunded);
        }

        private async Task<bool> OnCheckoutCompletedAsync(Session session)
        {
            if (session.Mode == "payment") return await OnPaymentCheckoutAsync(session);
            if (session.Mode != "subscription" || string.IsNullOrEmpty(session.SubscriptionId)) return false;
            if (!TryReadIds(session.Metadata, out var userId, out var planId) &&
                !(int.TryParse(session.ClientReferenceId, out userId) && planId > 0))
            {
                _logger.LogError("Checkout {Session} senza utente o piano nei metadati", session.Id);
                return false;
            }
            await EnsureSubscriptionAsync(userId, planId, session.SubscriptionId, session.CustomerId,
                (session.AmountTotal ?? 0) / 100m, periodEnd: null, reference: session.Id);
            return true;
        }

        private async Task<bool> OnInvoicePaidAsync(Invoice invoice)
        {
            var details = invoice.Parent?.SubscriptionDetails;
            var subscriptionId = details?.SubscriptionId;
            if (string.IsNullOrEmpty(subscriptionId)) return false;
            var periodEnd = invoice.Lines?.Data?.Where(l => l.Period != null).Select(l => (DateTime?)l.Period.End).Max();
            var amount = invoice.AmountPaid / 100m;

            var existing = await FindAsync(subscriptionId);
            if (existing == null)
            {
                // invoice.paid può arrivare prima di checkout.session.completed
                if (!TryReadIds(details?.Metadata, out var userId, out var planId)) return false;
                await EnsureSubscriptionAsync(userId, planId, subscriptionId, invoice.CustomerId, amount, periodEnd, invoice.Id);
                return true;
            }

            if (invoice.BillingReason != "subscription_create")
            {
                existing.AmountPaid += amount; // rinnovo: si somma all'importo già incassato
                existing.ExpiryReminderSentAt = null;
            }
            if (periodEnd.HasValue && periodEnd.Value > existing.EndDate) existing.EndDate = DateTime.SpecifyKind(periodEnd.Value, DateTimeKind.Utc);
            existing.Status = SubscriptionStatus.Active;
            existing.PaymentReference = invoice.Id;
            await _context.SaveChangesAsync();
            await _billing.SyncPremiumRoleAsync(existing.UserId);
            return true;
        }

        private async Task<bool> OnPaymentFailedAsync(Invoice invoice)
        {
            var subscriptionId = invoice.Parent?.SubscriptionDetails?.SubscriptionId;
            var existing = subscriptionId != null ? await FindAsync(subscriptionId) : null;
            if (existing == null) return false;
            await TryNotifyAsync(existing.UserId, NotificationType.SubscriptionExpiring, "Pagamento non riuscito",
                "Non siamo riusciti a rinnovare il tuo piano. Aggiorna la carta dalla pagina dei piani per non perderlo.");
            return true;
        }

        private async Task<bool> OnSubscriptionUpdatedAsync(Subscription subscription)
        {
            var existing = await FindAsync(subscription.Id);
            if (existing == null) return false;
            existing.AutoRenew = !subscription.CancelAtPeriodEnd && subscription.Status is "active" or "trialing" or "past_due";
            if (subscription.CancelAtPeriodEnd && existing.CancelledAt == null) existing.CancelledAt = DateTime.UtcNow;
            if (!subscription.CancelAtPeriodEnd) existing.CancelledAt = null;
            var periodEnd = subscription.Items?.Data?.Select(i => (DateTime?)i.CurrentPeriodEnd).Max();
            if (periodEnd.HasValue && periodEnd.Value > existing.EndDate && subscription.Status == "active")
                existing.EndDate = DateTime.SpecifyKind(periodEnd.Value, DateTimeKind.Utc);
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<bool> OnSubscriptionDeletedAsync(Subscription subscription)
        {
            var existing = await FindAsync(subscription.Id);
            if (existing == null) return false;
            var now = DateTime.UtcNow;
            existing.AutoRenew = false;
            existing.Status = SubscriptionStatus.Expired;
            existing.CancelledAt ??= now;
            if (existing.EndDate > now) existing.EndDate = now;
            await _context.SaveChangesAsync();
            await _billing.SyncPremiumRoleAsync(existing.UserId);
            return true;
        }

        private async Task EnsureSubscriptionAsync(int userId, int planId, string subscriptionId, string? customerId,
            decimal amount, DateTime? periodEnd, string reference)
        {
            var existing = await FindAsync(subscriptionId);
            if (existing != null)
            {
                existing.ExternalCustomerId ??= customerId;
                await _context.SaveChangesAsync();
                return;
            }
            var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == planId);
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (plan == null || !userExists)
            {
                _logger.LogError("Abbonamento Stripe {Subscription}: utente {UserId} o piano {PlanId} inesistente", subscriptionId, userId, planId);
                return;
            }
            await _billing.ActivateAsync(new ActivationRequest(userId, plan, SubscriptionPaymentMethod.Stripe, amount, reference,
                periodEnd.HasValue ? DateTime.SpecifyKind(periodEnd.Value, DateTimeKind.Utc) : null,
                ExternalCustomerId: customerId, ExternalSubscriptionId: subscriptionId, AutoRenew: true));
        }

        private Task<UserSubscription?> FindAsync(string subscriptionId) =>
            _context.UserSubscriptions
                .Where(s => s.ExternalSubscriptionId == subscriptionId)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

        private static bool TryReadIds(IDictionary<string, string>? metadata, out int userId, out int planId)
        {
            userId = 0;
            planId = 0;
            return metadata != null
                   && metadata.TryGetValue("userId", out var u) && int.TryParse(u, out userId)
                   && metadata.TryGetValue("planId", out var p) && int.TryParse(p, out planId);
        }

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
