using System.Security.Cryptography;
using System.Text;

namespace CardExchange.Core.Rules
{
    /// <summary>Regole degli inviti con premio.</summary>
    public static class ReferralRules
    {
        /// <summary>Invitati attivi per guadagnare un mese Premium.</summary>
        public const int PerReward = 3;
        /// <summary>Mesi Premium regalati al massimo in 12 mesi.</summary>
        public const int MaxMonthsPerYear = 6;
        /// <summary>Entro quanti giorni dall'iscrizione l'invitato deve diventare attivo.</summary>
        public const int QualifyWithinDays = 30;
        public const int MinCards = 10;
        public const int MinActiveDays = 3;

        private static readonly HashSet<string> Disposable = new(StringComparer.OrdinalIgnoreCase)
        {
            "mailinator.com", "guerrillamail.com", "guerrillamail.net", "sharklasers.com", "grr.la", "10minutemail.com",
            "10minutemail.net", "temp-mail.org", "tempmail.com", "tempmail.net", "tempmailo.com", "yopmail.com", "yopmail.fr",
            "trashmail.com", "trashmail.de", "getnada.com", "nada.email", "dispostable.com", "maildrop.cc", "mailnesia.com",
            "mohmal.com", "throwawaymail.com", "fakeinbox.com", "mintemail.com", "emailondeck.com", "spamgourmet.com",
            "mytemp.email", "tempr.email", "discard.email", "moakt.com", "minuteinbox.com", "burnermail.io", "inboxkitten.com",
            "mailpoof.com", "tmail.ws", "luxusmail.org", "byom.de", "spam4.me", "33mail.com", "harakirimail.com"
        };

        public static bool IsDisposableEmail(string? email)
        {
            var at = email?.LastIndexOf('@') ?? -1;
            return at >= 0 && Disposable.Contains(email![(at + 1)..].Trim());
        }

        /// <summary>
        /// Impronta della connessione all'iscrizione: hash dell'IP con la data, così serve solo a riconoscere
        /// più iscrizioni dallo stesso posto nello stesso giorno e non permette di risalire all'indirizzo.
        /// </summary>
        public static string? SignupFingerprint(string? ip, DateTime utc)
        {
            if (string.IsNullOrWhiteSpace(ip)) return null;
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"ce-signup|{ip}|{utc:yyyy-MM-dd}"));
            return Convert.ToHexString(bytes, 0, 12);
        }
    }
}
