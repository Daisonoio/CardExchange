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
                .Include(m => m.TradeOffer)
                .Where(m => m.Status == MeetupStatus.Accepted && m.ReminderSentAt == null
                            && m.ScheduledAt > nowUtc && m.ScheduledAt <= nowUtc + ReminderWindow)
                .ToListAsync(ct);

            foreach (var m in upcoming)
            {
                var body = $"Ricorda l'incontro {MeetupsController.FormatWhen(m.ScheduledAt)} a {m.LocationName}. Porta le carte dello scambio #{m.TradeOfferId}.";
                await NotifyBothAsync(m, "Promemoria appuntamento", body);
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
                await NotifyBothAsync(m, "Lo scambio è avvenuto?",
                    $"Se vi siete incontrati, conferma lo scambio #{m.TradeOfferId} nell'app: le carte passeranno di collezione quando confermerete entrambi.");
                m.FollowUpSentAt = nowUtc;
            }

            await NotifyStoresAsync(nowUtc, ct);

            await _context.SaveChangesAsync(ct);
            return (upcoming.Count, past.Count);
        }

        /// <summary>
        /// Avvisa i negozi degli scambi fissati da loro oggi: dalle 8:00 (ora italiana) un riepilogo,
        /// poi un avviso per ogni incontro confermato più tardi. Il negozio vede quanti e quando, mai chi.
        /// </summary>
        public async Task<int> NotifyStoresAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, ItalyTime);
            if (local.Hour < StoreDigestHour) return 0;
            var endOfDayUtc = TimeZoneInfo.ConvertTimeToUtc(local.Date.AddDays(1), ItalyTime);

            var meetups = await _context.TradeMeetups
                .Include(m => m.Store)
                .Where(m => m.Status == MeetupStatus.Accepted && m.StoreId != null && m.StoreNotifiedAt == null
                            && m.ScheduledAt > nowUtc && m.ScheduledAt < endOfDayUtc)
                .ToListAsync(ct);

            foreach (var group in meetups.Where(m => m.Store != null).GroupBy(m => m.StoreId!.Value))
            {
                var store = group.First().Store!;
                var first = TimeZoneInfo.ConvertTimeFromUtc(group.Min(m => m.ScheduledAt), ItalyTime);
                var count = group.Count();
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
                foreach (var m in group) m.StoreNotifiedAt = nowUtc;
            }
            return meetups.Count;
        }

        /// <summary>Ora (italiana) da cui parte l'avviso giornaliero ai negozi.</summary>
        public const int StoreDigestHour = 8;

        private static readonly TimeZoneInfo ItalyTime = ResolveItalyTimeZone();

        private static TimeZoneInfo ResolveItalyTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time"); }
        }

        private async Task NotifyBothAsync(TradeMeetup m, string title, string body)
        {
            foreach (var userId in new[] { m.TradeOffer.SenderId, m.TradeOffer.ReceiverId })
            {
                try
                {
                    await _notifications.SendAsync(userId, NotificationType.MeetupUpdate, title, body, m.TradeOfferId, "TradeOffer");
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
