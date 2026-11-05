using System.Net;

namespace CardExchange.API.Services.Email
{
    /// <summary>
    /// Testi delle email transazionali (italiano).
    /// </summary>
    public static class EmailTemplates
    {
        public static EmailMessage VerifyEmail(string firstName, string link) => Build(
            subject: "Conferma il tuo indirizzo email - CardExchange",
            greeting: firstName,
            intro: "grazie per esserti registrato su CardExchange! Conferma il tuo indirizzo email per completare l'attivazione dell'account.",
            buttonText: "Conferma email",
            link: link,
            footer: "Il link scade tra 24 ore. Se non ti sei registrato, ignora questa email.");

        public static EmailMessage ResetPassword(string firstName, string link) => Build(
            subject: "Reimposta la tua password - CardExchange",
            greeting: firstName,
            intro: "abbiamo ricevuto una richiesta di reimpostazione della password del tuo account.",
            buttonText: "Reimposta password",
            link: link,
            footer: "Il link scade tra 1 ora e può essere usato una sola volta. Se non hai richiesto tu il reset, ignora questa email: la tua password resta invariata.");

        public static EmailMessage WeeklyDigest(string firstName, int totalMatches, IReadOnlyList<string> highlights, string link, string preferencesLink) => Build(
            subject: totalMatches == 1
                ? "Un collezionista vicino a te ha carte che cerchi"
                : $"{totalMatches} collezionisti vicino a te hanno carte che cerchi",
            greeting: firstName,
            intro: "ecco i match più interessanti di questa settimana:\n" + string.Join("\n", highlights.Select(h => "• " + h)),
            buttonText: "Vedi tutti i match",
            link: link,
            footer: $"Ricevi questa email perché hai attivo il riepilogo settimanale. Puoi disattivarlo dalle preferenze notifiche: {preferencesLink}");

        public static EmailMessage StoreMonthlyReport(string firstName, string storeName, string monthName, string headline, IReadOnlyList<string> lines, string link) => Build(
            subject: $"{storeName}: il tuo mese di {monthName} su CardExchange",
            greeting: firstName,
            intro: $"{headline}.\n\nNel mese di {monthName}:\n" + string.Join("\n", lines.Select(l => "• " + l)),
            buttonText: "Vedi tutti i risultati",
            link: link,
            footer: "Ricevi questa email perché gestisci un negozio su CardExchange. Il riepilogo arriva una volta al mese.");

        private static EmailMessage Build(string subject, string greeting, string intro, string buttonText, string link, string footer)
        {
            var name = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(greeting) ? "ciao" : greeting);
            var safeLink = WebUtility.HtmlEncode(link);

            var html = $"""
                <!doctype html>
                <html lang="it">
                <body style="margin:0;padding:24px;background:#f3f5f9;font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;margin:0 auto;background:#ffffff;border-radius:12px;">
                    <tr><td style="background:#1a3461;color:#ffffff;padding:20px 24px;border-radius:12px 12px 0 0;font-size:20px;font-weight:bold;">CardExchange</td></tr>
                    <tr><td style="padding:24px;font-size:15px;line-height:1.5;">
                      <p>Ciao {name},</p>
                      <p>{WebUtility.HtmlEncode(intro).Replace("\n", "<br>")}</p>
                      <p style="text-align:center;margin:28px 0;">
                        <a href="{safeLink}" style="background:#1a3461;color:#ffffff;text-decoration:none;padding:12px 24px;border-radius:10px;font-weight:bold;display:inline-block;">{WebUtility.HtmlEncode(buttonText)}</a>
                      </p>
                      <p style="font-size:13px;color:#6b7280;">Se il pulsante non funziona, copia questo link nel browser:<br><a href="{safeLink}" style="color:#1a3461;word-break:break-all;">{safeLink}</a></p>
                      <p style="font-size:13px;color:#6b7280;">{WebUtility.HtmlEncode(footer)}</p>
                    </td></tr>
                  </table>
                </body>
                </html>
                """;

            var text = $"Ciao {(string.IsNullOrWhiteSpace(greeting) ? "" : greeting)},\n\n{intro}\n\n{buttonText}: {link}\n\n{footer}\n\nCardExchange";

            return new EmailMessage(subject, html, text);
        }
    }
}
