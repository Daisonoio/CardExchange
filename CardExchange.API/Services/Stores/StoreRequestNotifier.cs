using System.Net;
using System.Text;
using CardExchange.API.Configuration;
using CardExchange.API.Services.Email;
using CardExchange.Core.Entities;
using Microsoft.Extensions.Options;

namespace CardExchange.API.Services.Stores
{
    /// <summary>Manda alla casella delle richieste il riepilogo di un negozio inviato per la verifica.</summary>
    public class StoreRequestNotifier
    {
        private readonly IEmailSender _email;
        private readonly StoreRequestOptions _options;
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<StoreRequestNotifier> _logger;

        public StoreRequestNotifier(IEmailSender email, IOptions<StoreRequestOptions> options, IOptions<EmailSettings> emailSettings, ILogger<StoreRequestNotifier> logger)
        {
            _email = email;
            _options = options.Value;
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        /// <summary>Un errore d'invio non blocca la richiesta: il negozio resta comunque in attesa di verifica.</summary>
        public async Task<bool> NotifyAsync(Store store, User submitter, IEnumerable<string> gameNames, bool resubmitted, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_options.NotifyEmail))
            {
                _logger.LogWarning("Richiesta negozio {StoreId} senza email di riepilogo: imposta StoreRequests:NotifyEmail nella configurazione", store.Id);
                return false;
            }
            try
            {
                await _email.SendAsync(_options.NotifyEmail, "Richieste negozi", Build(store, submitter, gameNames.ToList(), resubmitted), ct);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email di riepilogo per il negozio {StoreId} non inviata", store.Id);
                return false;
            }
        }

        public EmailMessage Build(Store store, User submitter, IReadOnlyList<string> games, bool resubmitted)
        {
            var rows = new List<(string Label, string Value)>
            {
                ("Nome", store.Name),
                ("Descrizione", store.Description ?? ""),
                ("Indirizzo", store.Address),
                ("CAP", store.PostalCode ?? ""),
                ("Città", store.City),
                ("Provincia", store.Province ?? ""),
                ("Paese", store.Country),
                ("Posizione", $"{store.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {store.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}"),
                ("Orari", store.OpeningHours ?? ""),
                ("Telefono", store.Phone ?? ""),
                ("Sito", store.Website ?? ""),
                ("Instagram", string.IsNullOrWhiteSpace(store.Instagram) ? "—" : store.Instagram!),
                ("Giochi", string.Join(", ", games)),
                ("Punto d'incontro", store.IsSafeMeetupSpot ? "Sì" : "No"),
                ("Inviato da", $"@{submitter.Username} ({submitter.FirstName} {submitter.LastName}) · {submitter.Email}"),
                ("Id negozio", store.Id.ToString()),
            };
            var map = $"https://www.openstreetmap.org/?mlat={store.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&mlon={store.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}#map=18/{store.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}/{store.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            var moderation = $"{_emailSettings.FrontendBaseUrl.TrimEnd('/')}/moderation";
            var subject = $"{(resubmitted ? "Richiesta negozio aggiornata" : "Nuova richiesta negozio")}: {store.Name} ({store.City})";

            var text = new StringBuilder();
            text.AppendLine(resubmitted ? "Una richiesta di negozio è stata corretta e rimandata in verifica." : "È arrivata una nuova richiesta di inserimento di un negozio.");
            text.AppendLine();
            foreach (var (label, value) in rows) text.AppendLine($"{label}: {value}");
            text.AppendLine();
            text.AppendLine($"Mappa: {map}");
            text.AppendLine($"Verifica: {moderation}");

            var html = new StringBuilder();
            html.Append($"<p>{WebUtility.HtmlEncode(resubmitted ? "Una richiesta di negozio è stata corretta e rimandata in verifica." : "È arrivata una nuova richiesta di inserimento di un negozio.")}</p>");
            html.Append("<table cellpadding=\"6\" style=\"border-collapse:collapse\">");
            foreach (var (label, value) in rows)
                html.Append($"<tr><td style=\"font-weight:bold;vertical-align:top\">{WebUtility.HtmlEncode(label)}</td><td>{WebUtility.HtmlEncode(value)}</td></tr>");
            html.Append("</table>");
            html.Append($"<p><a href=\"{WebUtility.HtmlEncode(map)}\">Vedi sulla mappa</a> · <a href=\"{WebUtility.HtmlEncode(moderation)}\">Apri la verifica</a></p>");

            return new EmailMessage(subject, html.ToString(), text.ToString());
        }
    }
}
