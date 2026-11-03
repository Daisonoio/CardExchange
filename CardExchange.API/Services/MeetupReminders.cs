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

            await _context.SaveChangesAsync(ct);
            return (upcoming.Count, past.Count);
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
