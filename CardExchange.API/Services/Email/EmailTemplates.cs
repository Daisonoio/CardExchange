using System.Net;

namespace CardExchange.API.Services.Email
{
    /// <summary>
    /// Testi delle email transazionali: scritti in italiano, tradotti nella lingua dell'utente (ServerText).
    /// </summary>
    public static class EmailTemplates
    {
        private static string L(string text, string? lang) => CardExchange.API.Localization.ServerText.T(text, lang);

        public static EmailMessage VerifyEmail(string firstName, string link, string? lang = null) => Build(lang,
            subject: L("Conferma il tuo indirizzo email - CardExchange", lang),
            greeting: firstName,
            intro: L("grazie per esserti registrato su CardExchange! Conferma il tuo indirizzo email per completare l'attivazione dell'account.", lang),
            buttonText: L("Conferma email", lang),
            link: link,
            footer: L("Il link scade tra 24 ore. Se non ti sei registrato, ignora questa email.", lang));

        public static EmailMessage ResetPassword(string firstName, string link, string? lang = null) => Build(lang,
            subject: L("Reimposta la tua password - CardExchange", lang),
            greeting: firstName,
            intro: L("abbiamo ricevuto una richiesta di reimpostazione della password del tuo account.", lang),
            buttonText: L("Reimposta password", lang),
            link: link,
            footer: L("Il link scade tra 1 ora e può essere usato una sola volta. Se non hai richiesto tu il reset, ignora questa email: la tua password resta invariata.", lang));

        public static EmailMessage WeeklyDigest(string firstName, int totalMatches, IReadOnlyList<string> highlights, string link, string preferencesLink, string? lang = null) => Build(lang,
            subject: totalMatches == 1
                ? L("Un collezionista vicino a te ha carte che cerchi", lang)
                : L($"{totalMatches} collezionisti vicino a te hanno carte che cerchi", lang),
            greeting: firstName,
            intro: L("ecco i match più interessanti di questa settimana:", lang) + "\n" + string.Join("\n", highlights.Select(h => "• " + L(h, lang))),
            buttonText: L("Vedi tutti i match", lang),
            link: link,
            footer: L($"Ricevi questa email perché hai attivo il riepilogo settimanale. Puoi disattivarlo dalle preferenze notifiche: {preferencesLink}", lang));

        public static EmailMessage StoreMonthlyReport(string firstName, string storeName, string monthName, string headline, IReadOnlyList<string> lines, string link, string? lang = null) => Build(lang,
            subject: L($"{storeName}: il tuo mese di {monthName} su CardExchange", lang),
            greeting: firstName,
            intro: $"{L(headline, lang)}.\n\n{L($"Nel mese di {monthName}:", lang)}\n" + string.Join("\n", lines.Select(l => "• " + L(l, lang))),
            buttonText: L("Vedi tutti i risultati", lang),
            link: link,
            footer: L("Ricevi questa email perché gestisci un negozio su CardExchange. Il riepilogo arriva una volta al mese.", lang));

        private static EmailMessage Build(string? lang, string subject, string greeting, string intro, string buttonText, string link, string footer)
        {
            var hello = L("Ciao", lang);
            var name = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(greeting) ? "" : greeting);
            var htmlLang = CardExchange.API.Localization.ServerText.Normalize(lang);
            var safeLink = WebUtility.HtmlEncode(link);

            var html = $"""
                <!doctype html>
                <html lang="{htmlLang}">
                <body style="margin:0;padding:24px;background:#f3f5f9;font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;margin:0 auto;background:#ffffff;border-radius:12px;">
                    <tr><td style="background:#1a3461;color:#ffffff;padding:20px 24px;border-radius:12px 12px 0 0;font-size:20px;font-weight:bold;">CardExchange</td></tr>
                    <tr><td style="padding:24px;font-size:15px;line-height:1.5;">
                      <p>{hello}{(name.Length > 0 ? " " + name : "")},</p>
                      <p>{WebUtility.HtmlEncode(intro).Replace("\n", "<br>")}</p>
                      <p style="text-align:center;margin:28px 0;">
                        <a href="{safeLink}" style="background:#1a3461;color:#ffffff;text-decoration:none;padding:12px 24px;border-radius:10px;font-weight:bold;display:inline-block;">{WebUtility.HtmlEncode(buttonText)}</a>
                      </p>
                      <p style="font-size:13px;color:#6b7280;">{WebUtility.HtmlEncode(L("Se il pulsante non funziona, copia questo link nel browser:", lang))}<br><a href="{safeLink}" style="color:#1a3461;word-break:break-all;">{safeLink}</a></p>
                      <p style="font-size:13px;color:#6b7280;">{WebUtility.HtmlEncode(footer)}</p>
                    </td></tr>
                  </table>
                </body>
                </html>
                """;

            var text = $"{hello} {(string.IsNullOrWhiteSpace(greeting) ? "" : greeting)},\n\n{intro}\n\n{buttonText}: {link}\n\n{footer}\n\nCardExchange";

            return new EmailMessage(subject, html, text);
        }
    }
}
