using CardExchange.API.Configuration;
using CardExchange.API.Services.Email;
using CardExchange.Core.Entities;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CardExchange.API.Services
{
    /// <summary>
    /// Riepilogo settimanale via email con i migliori match vicini. Al massimo una email ogni 7 giorni per utente,
    /// solo a chi ha confermato l'email, ha una posizione, è stato attivo negli ultimi 60 giorni e non l'ha disattivato.
    /// </summary>
    public class WeeklyDigest
    {
        public static readonly TimeSpan Interval = TimeSpan.FromDays(7);
        public static readonly TimeSpan ActiveWithin = TimeSpan.FromDays(60);
        public const int MaxUsersPerRun = 500;

        private readonly ApplicationDbContext _context;
        private readonly IMatchmakingService _matchmaking;
        private readonly IEmailSender _email;
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<WeeklyDigest> _logger;

        public WeeklyDigest(ApplicationDbContext context, IMatchmakingService matchmaking, IEmailSender email,
            IOptions<EmailSettings> emailSettings, ILogger<WeeklyDigest> logger)
        {
            _context = context;
            _matchmaking = matchmaking;
            _email = email;
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task<int> RunAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            // Margine di 12 ore: chi l'ha ricevuto lunedì alle 02:30 lo riceve di nuovo il lunedì successivo
            var dueBefore = nowUtc - Interval + TimeSpan.FromHours(12);
            var activeSince = nowUtc - ActiveWithin;

            var optedOut = _context.NotificationPreferences
                .Where(p => p.Type == NotificationType.WeeklyDigest && !p.IsEnabled)
                .Select(p => p.UserId);

            var users = await _context.Users
                .Where(u => u.EmailConfirmed && u.Location != null && u.Location.Latitude != null
                            && u.LastLoginAt != null && u.LastLoginAt >= activeSince
                            && (u.LastWeeklyDigestAt == null || u.LastWeeklyDigestAt < dueBefore)
                            && !optedOut.Contains(u.Id))
                .OrderBy(u => u.LastWeeklyDigestAt)
                .Take(MaxUsersPerRun)
                .ToListAsync(ct);

            var baseUrl = _emailSettings.FrontendBaseUrl.TrimEnd('/');
            int sent = 0;
            foreach (var user in users)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var matches = (await _matchmaking.FindMatchesAsync(user.Id)).ToList();
                    if (matches.Count > 0)
                    {
                        var highlights = matches
                            .OrderByDescending(m => m.Score)
                            .Take(3)
                            .Select(Describe)
                            .ToList();
                        await _email.SendAsync(user.Email, $"{user.FirstName} {user.LastName}".Trim(),
                            EmailTemplates.WeeklyDigest(user.FirstName, matches.Count, highlights, $"{baseUrl}/matchmaking", $"{baseUrl}/profile", user.Language), ct);
                        sent++;
                    }
                    // Anche senza match si aggiorna la data, così l'utente non viene ricalcolato ogni notte
                    user.LastWeeklyDigestAt = nowUtc;
                    await _context.SaveChangesAsync(ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Riepilogo settimanale non inviato all'utente {UserId}", user.Id);
                }
            }

            if (users.Count > 0)
                _logger.LogInformation("Riepilogo settimanale: {Sent} email su {Users} utenti valutati", sent, users.Count);
            return sent;
        }

        public static string Describe(MatchResult m)
        {
            var where = m.DistanceKm.HasValue ? $" a {Math.Max(1, Math.Round(m.DistanceKm.Value))} km" : "";
            var theyHave = m.TheyHaveIWant.Count;
            var iHave = m.IHaveTheyWant.Count;
            var detail = theyHave > 0 && iHave > 0
                ? $"ha {theyHave} {(theyHave == 1 ? "carta" : "carte")} che cerchi e ne cerca {iHave} delle tue"
                : theyHave > 0
                    ? $"ha {theyHave} {(theyHave == 1 ? "carta" : "carte")} che cerchi"
                    : $"cerca {iHave} {(iHave == 1 ? "carta" : "carte")} che hai";
            return $"{m.Username}{where} {detail}";
        }
    }
}
