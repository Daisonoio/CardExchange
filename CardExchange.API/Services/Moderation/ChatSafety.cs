using CardExchange.API.Services.Notifications;
using CardExchange.Core.Entities;
using CardExchange.Core.Rules;
using CardExchange.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardExchange.API.Services.Moderation
{
    public record ChatCheck(bool Allowed, string? Message = null, string? Code = null)
    {
        public static readonly ChatCheck Ok = new(true);
        public static ChatCheck Refuse(string message, string code) => new(false, message, code);
    }

    /// <summary>
    /// Sicurezza della chat e degli incontri. L'app serve a scambiare carte: niente contatti o link per spostarsi altrove,
    /// niente contenuti illegali, e regole più strette quando un adulto scrive a un minorenne (o a chi non ha indicato l'età).
    /// I messaggi bloccati non arrivano; i casi gravi diventano segnalazioni per i moderatori e,
    /// se si ripetono, la chat di chi scrive viene sospesa in attesa di verifica.
    /// Al destinatario non viene mai detto che l'altra persona è minorenne.
    /// </summary>
    public class ChatSafety
    {
        public const int MaxMinorsPerWeek = 5;
        public const int SevereFlagsToSuspend = 2;
        public static readonly TimeSpan SuspendFor = TimeSpan.FromDays(7);
        public static readonly TimeSpan FlagWindow = TimeSpan.FromDays(30);
        /// <summary>Tra adulti e minorenni niente messaggi di notte (ora italiana).</summary>
        public const int QuietFromHour = 22, QuietToHour = 7;
        /// <summary>Incontri con un minorenne: solo in un negozio partner e in questa fascia oraria.</summary>
        public const int MeetupFromHour = 9, MeetupToHour = 20;

        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;
        private readonly ILogger<ChatSafety> _logger;

        public ChatSafety(ApplicationDbContext db, INotificationService notifications, ILogger<ChatSafety> logger)
        {
            _db = db;
            _notifications = notifications;
            _logger = logger;
        }

        private sealed record Party(int Id, DateOnly? BirthDate, DateTime? SuspendedUntil);

        private async Task<(Party? Sender, Party? Recipient)> PartiesAsync(int senderId, int recipientId)
        {
            var rows = await _db.Users.AsNoTracking().Where(u => u.Id == senderId || u.Id == recipientId)
                .Select(u => new Party(u.Id, u.BirthDate, u.MessagingSuspendedUntil)).ToListAsync();
            return (rows.FirstOrDefault(r => r.Id == senderId), rows.FirstOrDefault(r => r.Id == recipientId));
        }

        public static bool IsQuietHour(DateTime nowUtc)
        {
            var h = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, TimeZones.Italy).Hour;
            return h >= QuietFromHour || h < QuietToHour;
        }

        /// <summary>Controllo di un messaggio prima dell'invio (chat in tempo reale ed endpoint REST).</summary>
        public async Task<ChatCheck> CheckMessageAsync(int senderId, int recipientId, string? content, int? conversationId, DateTime nowUtc)
        {
            var (sender, recipient) = await PartiesAsync(senderId, recipientId);
            if (sender == null || recipient == null) return ChatCheck.Refuse("Utente non trovato", "NOT_FOUND");
            if (sender.SuspendedUntil > nowUtc)
                return ChatCheck.Refuse("La tua chat è sospesa in attesa di una verifica dei moderatori.", "CHAT_SUSPENDED");

            var today = DateOnly.FromDateTime(nowUtc);
            var senderMinor = ChatSafetyRules.IsMinorOrUnknown(sender.BirthDate, today);
            var recipientMinor = ChatSafetyRules.IsMinorOrUnknown(recipient.BirthDate, today);
            var adultToMinor = !senderMinor && recipientMinor;
            var mixed = senderMinor != recipientMinor;
            var signals = ChatSafetyRules.Detect(content);

            // 1. Contenuti illegali o non legati alle carte: mai, per nessuno
            if (signals.HasFlag(ChatSignal.Illegal))
            {
                await FlagAsync(senderId, recipientId, conversationId, SafetyFlagKind.Illegal, content, recipientMinor, nowUtc);
                return ChatCheck.Refuse("Il messaggio non è stato inviato: CardExchange serve solo per scambiare carte.", "BLOCKED_CONTENT");
            }

            // 2. Contatti e link per spostarsi fuori dall'app: mai, per nessuno (ci si accorda qui e ci si vede in un negozio)
            if (signals.HasFlag(ChatSignal.Contact) || signals.HasFlag(ChatSignal.Link))
            {
                if (adultToMinor)
                    await FlagAsync(senderId, recipientId, conversationId, SafetyFlagKind.ContactToMinor, content, true, nowUtc);
                return ChatCheck.Refuse("Per la sicurezza di tutti in chat non si possono condividere numeri, email, social o link. Accordatevi qui e incontratevi in un negozio partner.", "BLOCKED_CONTACT");
            }

            if (mixed)
            {
                // 3. Tra adulti e minorenni niente messaggi di notte, in entrambe le direzioni
                if (IsQuietHour(nowUtc))
                    return ChatCheck.Refuse($"Questa conversazione è in pausa dalle {QuietFromHour}:00 alle {QuietToHour}:00. Riprova domattina.", "QUIET_HOURS");

                // 4. Denaro online in una conversazione con un minorenne: si paga solo di persona
                if (signals.HasFlag(ChatSignal.Payment))
                {
                    if (adultToMinor) await FlagAsync(senderId, recipientId, conversationId, SafetyFlagKind.PaymentWithMinor, content, true, nowUtc);
                    return ChatCheck.Refuse("Niente pagamenti online o ricariche: su CardExchange ci si accorda e si paga di persona.", "BLOCKED_PAYMENT");
                }
            }

            if (adultToMinor)
            {
                // 5. Frasi tipiche dell'adescamento: bloccate e segnalate
                if (signals.HasFlag(ChatSignal.Grooming))
                {
                    await FlagAsync(senderId, recipientId, conversationId, SafetyFlagKind.Grooming, content, true, nowUtc);
                    return ChatCheck.Refuse("Il messaggio non è stato inviato perché non riguarda lo scambio di carte.", "BLOCKED_CONTENT");
                }

                // 6. Un adulto scrive a un minorenne solo dentro uno scambio tra loro due
                var hasTrade = await _db.TradeOffers.AsNoTracking().AnyAsync(t =>
                    (t.SenderId == senderId && t.ReceiverId == recipientId) || (t.SenderId == recipientId && t.ReceiverId == senderId));
                if (!hasTrade)
                    return ChatCheck.Refuse("Per scrivere a questo utente parti da una proposta di scambio sulle sue carte.", "TRADE_REQUIRED");

                // 7. Troppi minorenni diversi contattati in una settimana: bloccato e segnalato
                var since = nowUtc.AddDays(-7);
                var others = await _db.Messages.AsNoTracking()
                    .Where(m => m.SenderId == senderId && m.CreatedAt >= since)
                    .Select(m => m.Conversation.User1Id == senderId ? m.Conversation.User2Id : m.Conversation.User1Id)
                    .Distinct().ToListAsync();
                if (!others.Contains(recipientId))
                {
                    var minors = (await _db.Users.AsNoTracking().Where(u => others.Contains(u.Id)).Select(u => u.BirthDate).ToListAsync())
                        .Count(bd => ChatSafetyRules.IsMinorOrUnknown(bd, today));
                    if (minors >= MaxMinorsPerWeek)
                    {
                        await FlagAsync(senderId, recipientId, conversationId, SafetyFlagKind.ManyMinors, content, true, nowUtc);
                        return ChatCheck.Refuse("Hai già scritto a molte persone nuove questa settimana. Riprova tra qualche giorno.", "TOO_MANY_CONTACTS");
                    }
                }
            }

            return ChatCheck.Ok;
        }

        /// <summary>Testo libero fuori dalla chat (messaggio della proposta, note dell'incontro): niente contatti, link o contenuti illegali.</summary>
        public async Task<ChatCheck> CheckTextAsync(int senderId, int? recipientId, string? text, DateTime nowUtc)
        {
            var signals = ChatSafetyRules.Detect(text);
            if (signals.HasFlag(ChatSignal.Illegal))
            {
                await FlagAsync(senderId, recipientId, null, SafetyFlagKind.Illegal, text, false, nowUtc);
                return ChatCheck.Refuse("Il testo non è stato accettato: CardExchange serve solo per scambiare carte.", "BLOCKED_CONTENT");
            }
            if (signals.HasFlag(ChatSignal.Contact) || signals.HasFlag(ChatSignal.Link))
                return ChatCheck.Refuse("Non si possono condividere numeri, email, social o link: accordatevi in chat.", "BLOCKED_CONTACT");
            if (recipientId != null && signals.HasFlag(ChatSignal.Grooming))
            {
                var (sender, recipient) = await PartiesAsync(senderId, recipientId.Value);
                var today = DateOnly.FromDateTime(nowUtc);
                if (sender != null && recipient != null && !ChatSafetyRules.IsMinorOrUnknown(sender.BirthDate, today) && ChatSafetyRules.IsMinorOrUnknown(recipient.BirthDate, today))
                {
                    await FlagAsync(senderId, recipientId, null, SafetyFlagKind.Grooming, text, true, nowUtc);
                    return ChatCheck.Refuse("Il testo non è stato accettato perché non riguarda lo scambio di carte.", "BLOCKED_CONTENT");
                }
            }
            return ChatCheck.Ok;
        }

        /// <summary>Incontro con un minorenne (o con chi non ha indicato l'età): solo in un negozio partner e di giorno.</summary>
        public async Task<ChatCheck> CheckMeetupAsync(int proposerId, int otherId, int? storeId, DateTime scheduledUtc)
        {
            var (a, b) = await PartiesAsync(proposerId, otherId);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var involvesMinor = a == null || b == null || ChatSafetyRules.IsMinorOrUnknown(a.BirthDate, today) || ChatSafetyRules.IsMinorOrUnknown(b.BirthDate, today);
            if (!involvesMinor) return ChatCheck.Ok;
            if (storeId == null)
                return ChatCheck.Refuse("Per questo scambio scegli uno dei negozi partner come punto d'incontro.", "STORE_REQUIRED");
            var local = TimeZoneInfo.ConvertTimeFromUtc(scheduledUtc, TimeZones.Italy);
            if (local.Hour < MeetupFromHour || local.Hour >= MeetupToHour)
                return ChatCheck.Refuse($"Per questo scambio scegli un orario tra le {MeetupFromHour}:00 e le {MeetupToHour}:00.", "DAYTIME_REQUIRED");
            return ChatCheck.Ok;
        }

        private static bool IsSevere(SafetyFlagKind k) => k is SafetyFlagKind.Grooming or SafetyFlagKind.Illegal or SafetyFlagKind.ContactToMinor or SafetyFlagKind.ManyMinors;

        /// <summary>Registra la segnalazione, avvisa i moderatori e, se le segnalazioni gravi si ripetono, sospende la chat.</summary>
        public async Task FlagAsync(int senderId, int? recipientId, int? conversationId, SafetyFlagKind kind, string? text, bool involvesMinor, DateTime nowUtc, bool blocked = true)
        {
            var trimmed = text?.Trim();
            var excerpt = string.IsNullOrEmpty(trimmed) ? null : trimmed.Length > 300 ? trimmed[..300] : trimmed;
            _db.SafetyFlags.Add(new SafetyFlag
            {
                SenderId = senderId, RecipientId = recipientId, ConversationId = conversationId, Kind = kind,
                Blocked = blocked, Excerpt = excerpt, InvolvesMinor = involvesMinor, CreatedAt = nowUtc
            });
            await _db.SaveChangesAsync();

            var suspended = false;
            if (IsSevere(kind))
            {
                var since = nowUtc - FlagWindow;
                var severe = await _db.SafetyFlags.CountAsync(f => f.SenderId == senderId && f.CreatedAt >= since
                    && (f.Kind == SafetyFlagKind.Grooming || f.Kind == SafetyFlagKind.Illegal || f.Kind == SafetyFlagKind.ContactToMinor || f.Kind == SafetyFlagKind.ManyMinors));
                if (severe >= SevereFlagsToSuspend)
                {
                    await _db.Users.Where(u => u.Id == senderId)
                        .ExecuteUpdateAsync(u => u.SetProperty(x => x.MessagingSuspendedUntil, nowUtc + SuspendFor));
                    suspended = true;
                }
            }
            _logger.LogWarning("Sicurezza chat: {Kind} da {SenderId} (minore coinvolto: {Minor}, chat sospesa: {Suspended})", kind, senderId, involvesMinor, suspended);

            if (IsSevere(kind) || involvesMinor)
            {
                try
                {
                    var admins = await _db.UserRoles.AsNoTracking()
                        .Where(ur => ur.Role.Name == "Admin" || ur.Role.Name == "SuperAdmin").Select(ur => ur.UserId).Distinct().ToListAsync();
                    foreach (var adminId in admins)
                        await _notifications.SendAsync(adminId, NotificationType.SystemAnnouncement, "Segnalazione di sicurezza in chat",
                            involvesMinor ? "Un messaggio verso un minorenne è stato bloccato: verifica nella moderazione." : "Un messaggio è stato bloccato: verifica nella moderazione.",
                            null, "SafetyFlag");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Avviso ai moderatori non inviato");
                }
            }
        }
    }
}
