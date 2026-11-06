using CardExchange.Core.Rules;
using CardExchange.API.Controllers;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services
{
    /// <summary>
    /// Promemoria degli appuntamenti confermati: uno entro le 24 ore precedenti e, un'ora dopo l'incontro,
    /// l'invito a confermare lo scambio se non è ancora stato completato. Ogni messaggio parte una sola volta.
    /// </summary>
    public class MeetupReminders
    {
        public static readonly TimeSpan ReminderWindow = TimeSpan.FromHours(24);
        public static readonly TimeSpan FollowUpDelay = TimeSpan.FromHours(1);

        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notifications;
        private readonly ILogger<MeetupReminders> _logger;

        public MeetupReminders(ApplicationDbContext context, INotificationService notifications, ILogger<MeetupReminders> logger)
        {
            _context = context;
            _notifications = notifications;
            _logger = logger;
        }

        public async Task<(int Reminders, int FollowUps)> RunAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var upcoming = await _context.TradeMeetups
                .Include(m => m.TradeOffer).ThenInclude(o => o.Sender)
                .Include(m => m.TradeOffer).ThenInclude(o => o.Receiver)
                .Where(m => m.Status == MeetupStatus.Accepted && m.ReminderSentAt == null
                            && m.ScheduledAt > nowUtc && m.ScheduledAt <= nowUtc + ReminderWindow)
                .ToListAsync(ct);

            foreach (var m in upcoming)
            {
                await NotifyBothAsync(m, "Promemoria appuntamento", tz =>
                    $"Ricorda l'incontro {TimeZones.FormatWhen(m.ScheduledAt, tz)} a {m.LocationName}. Porta le carte dello scambio #{m.TradeOfferId}.");
                m.ReminderSentAt = nowUtc;
            }

            var past = await _context.TradeMeetups
                .Include(m => m.TradeOffer)
                .Where(m => m.Status == MeetupStatus.Accepted && m.FollowUpSentAt == null
                            && m.ScheduledAt <= nowUtc - FollowUpDelay
                            && m.TradeOffer.Status == TradeOfferStatus.Accepted)
                .ToListAsync(ct);

            foreach (var m in past)
            {
                await NotifyBothAsync(m, "Lo scambio è avvenuto?", _ =>
                    $"Se vi siete incontrati, conferma lo scambio #{m.TradeOfferId} nell'app: le carte passeranno di collezione quando confermerete entrambi.");
                m.FollowUpSentAt = nowUtc;
            }

            await NotifyStoresAsync(nowUtc, ct);

            await _context.SaveChangesAsync(ct);
            return (upcoming.Count, past.Count);
        }

        /// <summary>
        /// Avvisa i negozi degli scambi fissati da loro oggi: dalle 8:00 (nel fuso del proprietario) un riepilogo,
        /// poi un avviso per ogni incontro confermato più tardi. Il negozio vede quanti e quando, mai chi.
        /// </summary>
        public async Task<int> NotifyStoresAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            // Finestra larga (fino a 36 ore): il "oggi" di ogni negozio si calcola nel fuso del proprietario
            var horizon = nowUtc.AddHours(36);
            var meetups = await _context.TradeMeetups
                .Include(m => m.Store).ThenInclude(s => s!.Owner)
                .Where(m => m.Status == MeetupStatus.Accepted && m.StoreId != null && m.StoreNotifiedAt == null
                            && m.ScheduledAt > nowUtc && m.ScheduledAt < horizon)
                .ToListAsync(ct);

            var notified = 0;
            foreach (var group in meetups.Where(m => m.Store != null).GroupBy(m => m.StoreId!.Value))
            {
                var store = group.First().Store!;
                var tzId = store.Owner?.TimeZone;
                var zone = TimeZones.Resolve(tzId);
                var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone);
                if (local.Hour < StoreDigestHour) continue;
                var endOfDayUtc = TimeZoneInfo.ConvertTimeToUtc(local.Date.AddDays(1), zone);
                var today = group.Where(m => m.ScheduledAt < endOfDayUtc).ToList();
                if (today.Count == 0) continue;
                var first = TimeZones.ToLocal(today.Min(m => m.ScheduledAt), tzId);
                var count = today.Count;
                var title = count == 1 ? "Oggi da te c'è uno scambio" : $"Oggi da te ci sono {count} scambi";
                var body = count == 1
                    ? $"Due persone si incontrano da {store.Name} alle {first:HH:mm} per uno scambio."
                    : $"{count * 2} persone si incontrano da {store.Name}, il primo scambio alle {first:HH:mm}.";
                try
                {
                    await _notifications.SendAsync(store.OwnerId, NotificationType.StoreActivity, title, body, store.Id, "Store");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Avviso al negozio {StoreId} non inviato", store.Id);
                    continue;
                }
                foreach (var m in today) m.StoreNotifiedAt = nowUtc;
                notified += today.Count;
            }
            return notified;
        }

        /// <summary>Ora (italiana) da cui parte l'avviso giornaliero ai negozi.</summary>
        public const int StoreDigestHour = 8;

        private async Task NotifyBothAsync(TradeMeetup m, string title, Func<string?, string> body)
        {
            foreach (var (userId, tz) in new[] { (m.TradeOffer.SenderId, m.TradeOffer.Sender?.TimeZone), (m.TradeOffer.ReceiverId, m.TradeOffer.Receiver?.TimeZone) })
            {
                try
                {
                    await _notifications.SendAsync(userId, NotificationType.MeetupUpdate, title, body(tz), m.TradeOfferId, "TradeOffer");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Promemoria appuntamento {MeetupId} non inviato a {UserId}", m.Id, userId);
                }
            }
        }
    }

    /// <summary>Esegue <see cref="MeetupReminders"/> ogni 15 minuti.</summary>
    public class MeetupRemindersHostedService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MeetupRemindersHostedService> _logger;

        public MeetupRemindersHostedService(IServiceScopeFactory scopeFactory, ILogger<MeetupRemindersHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var (reminders, followUps) = await scope.ServiceProvider.GetRequiredService<MeetupReminders>().RunAsync(DateTime.UtcNow, stoppingToken);
                    if (reminders + followUps > 0)
                        _logger.LogInformation("Promemoria appuntamenti: {Reminders} promemoria, {FollowUps} inviti a confermare", reminders, followUps);

                    // Eventi: posti offerti scaduti, promemoria del giorno prima, riepilogo all'organizzatore
                    using (var eventsScope = _scopeFactory.CreateScope())
                        await eventsScope.ServiceProvider.GetRequiredService<CardExchange.API.Services.Events.EventRegistrations>().RunAsync(DateTime.UtcNow, stoppingToken);

                    // Riepilogo serale degli avvisi wishlist (piano gratuito)
                    using (var digestScope = _scopeFactory.CreateScope())
                        await digestScope.ServiceProvider.GetRequiredService<CardExchange.API.Services.WishlistDigest>().RunAsync(DateTime.UtcNow, stoppingToken);

                    // Prenota e ritira: promemoria e scadenze
                    using (var reservationsScope = _scopeFactory.CreateScope())
                        await reservationsScope.ServiceProvider.GetRequiredService<CardExchange.API.Services.Stores.ReservationService>().RunAsync(DateTime.UtcNow, stoppingToken);

                    // Lista acquisti: promemoria e scadenze delle consegne
                    using (var buyScope = _scopeFactory.CreateScope())
                        await buyScope.ServiceProvider.GetRequiredService<CardExchange.API.Services.Stores.BuyListService>().RunAsync(DateTime.UtcNow, stoppingToken);

                    // Promozioni: consegna di quelle pagate con la carta, chiusura di quelle finite
                    using (var promoScope = _scopeFactory.CreateScope())
                        await promoScope.ServiceProvider.GetRequiredService<CardExchange.API.Services.Billing.PromotionService>().RunAsync(DateTime.UtcNow, stoppingToken);

                    // Metrica "match entro 48 ore" per i nuovi iscritti
                    using (var metricsScope = _scopeFactory.CreateScope())
                        await metricsScope.ServiceProvider.GetRequiredService<CardExchange.API.Services.Metrics.ActivationCheck>().RunAsync(DateTime.UtcNow, stoppingToken);

                    await Task.Delay(Interval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore nei promemoria appuntamenti");
                    try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { return; }
                }
            }
        }
    }
}
