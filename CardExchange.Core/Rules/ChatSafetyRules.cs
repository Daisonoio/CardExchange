using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CardExchange.Core.Rules
{
    /// <summary>Che cosa è stato riconosciuto in un messaggio.</summary>
    [Flags]
    public enum ChatSignal
    {
        None = 0,
        /// <summary>Numero di telefono, email, nome utente o app di messaggistica/social.</summary>
        Contact = 1,
        /// <summary>Link a siti esterni.</summary>
        Link = 2,
        /// <summary>Frasi tipiche dell'adescamento (età, solitudine, segreti, foto, passaggi in auto…).</summary>
        Grooming = 4,
        /// <summary>Droghe, armi, contenuti sessuali: nulla a che fare con le carte.</summary>
        Illegal = 8,
        /// <summary>Richieste di denaro online (ricariche, bonifici, gift card…).</summary>
        Payment = 16
    }

    /// <summary>
    /// Riconoscimento dei messaggi a rischio, sul testo normalizzato (minuscole, senza accenti, cifre staccate unite).
    /// Le regole su cosa bloccare e per chi stanno nel servizio di sicurezza della chat.
    /// </summary>
    public static class ChatSafetyRules
    {
        private const RegexOptions Opts = RegexOptions.Compiled | RegexOptions.CultureInvariant;

        // 9 o più cifre di fila, anche separate da spazi, punti o trattini (i numeri delle carte hanno la "/" e sono corti)
        private static readonly Regex Phone = new(@"(?<![\w/])\+?(?:\d[\s.\-]?){8,}\d(?![\w/])", Opts);
        private static readonly Regex Email = new(@"[a-z0-9._%+\-]+\s*(?:@|\(at\)|\[at\])\s*[a-z0-9\-]+\s*(?:\.|\(dot\)|\[dot\])\s*[a-z]{2,}", Opts);
        private static readonly Regex Handle = new(@"(?<![\w@])@[a-z0-9_.]{3,}", Opts);
        private static readonly Regex Apps = new(
            @"\b(?:whats\s*app|wapp|whatsapp|telegram|snap\s*chat|snapchat|insta(?:gram)?|tik\s*tok|tiktok|discord|signal|facebook|messenger|onlyfans|kik|wechat|skype|t\.me|wa\.me)\b", Opts);
        private static readonly Regex Link = new(@"https?://|www\.|\b[a-z0-9\-]{2,}\.(?:com|it|net|org|io|me|ly|gg|to|link|app|xyz|eu)\b", Opts);

        private static readonly Regex Grooming = new(string.Join("|", new[]
        {
            @"quanti anni hai", @"che eta hai", @"\beta\?", @"sei (?:da )?sol[oa]\b", @"a casa da sol[oa]", @"(?:i tuoi|tuoi genitori|tua madre|tuo padre|mamma|papa) (?:lo )?sann[oa]",
            @"non (?:lo )?dir(?:e|lo) (?:a |ai |alla |al )?(?:nessuno|tuoi|genitori|mamma|papa)", @"non dire (?:niente|nulla)", @"(?:nostro|un) segreto", @"tienilo per te",
            @"mand(?:a|ami|ami una) (?:una )?foto", @"una foto (?:di te|tua)", @"\bfoto tua\b", @"\btua foto\b", @"come sei vestit", @"cosa indossi",
            @"vieni a casa mia", @"(?:ti )?vengo a prenderti", @"ti (?:vengo|passo) a prendere", @"sali in macchina", @"in macchina con me", @"ti accompagno a casa",
            @"dove abiti", @"(?:che|quale|in che) scuola", @"sei (?:molto )?(?:carin[oa]|bellissim[oa]|sexy)", @"\bfidanzat[oa]\b"
        }), Opts);

        private static readonly Regex Illegal = new(@"\b(?:" + string.Join("|", new[]
        {
            "droga", "droghe", "hashish", "marijuana", "cocaina", "eroina", "ketamina", "mdma", "ecstasy", "anfetamin\\w*", "spaccio", "spacciare", "pusher",
            "xanax", "pistola", "pistole", "fucile", "munizioni", "armi da fuoco", "tirapugni",
            "sesso", "sessuale", "escort", "porno", "nud[oaie]", "video hot", "foto hot", "incontri hot"
        }) + @")\b", Opts);

        private static readonly Regex Payment = new(@"\b(?:paypal|iban|bonifico|postepay|ricarica|satispay|revolut|bitcoin|crypto|western union|moneygram|gift ?card|buono amazon|anticipo|caparra)\b", Opts);

        /// <summary>Minuscole, senza accenti e senza caratteri invisibili: "Ẁhàts App" → "whats app".</summary>
        public static string Normalize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var d = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (var c in d)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat is UnicodeCategory.NonSpacingMark or UnicodeCategory.Format) continue;
                sb.Append(char.ToLowerInvariant(c));
            }
            return Regex.Replace(sb.ToString(), @"\s+", " ");
        }

        public static ChatSignal Detect(string? text)
        {
            var t = Normalize(text);
            if (t.Length == 0) return ChatSignal.None;
            var s = ChatSignal.None;
            if (Phone.IsMatch(t) || Email.IsMatch(t) || Handle.IsMatch(t) || Apps.IsMatch(t)) s |= ChatSignal.Contact;
            if (Link.IsMatch(t)) s |= ChatSignal.Link;
            if (Grooming.IsMatch(t)) s |= ChatSignal.Grooming;
            if (Illegal.IsMatch(t)) s |= ChatSignal.Illegal;
            if (Payment.IsMatch(t)) s |= ChatSignal.Payment;
            return s;
        }

        /// <summary>Minorenne o età non indicata (per prudenza come minorenne).</summary>
        public static bool IsMinorOrUnknown(DateOnly? birthDate, DateOnly today) =>
            birthDate is not { } bd || AgeRules.IsMinor(bd, today);
    }
}
