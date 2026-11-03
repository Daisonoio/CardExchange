using CardExchange.API.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CardExchange.API.Services.Email
{
    /// <summary>
    /// Invia email via SMTP (qualsiasi provider: Brevo, Mailgun, SES, Gmail, ...).
    /// </summary>
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<EmailSettings> settings, ILogger<SmtpEmailSender> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task SendAsync(string toAddress, string toName, EmailMessage message, CancellationToken cancellationToken = default)
        {
            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
            mime.To.Add(new MailboxAddress(toName, toAddress));
            mime.Subject = message.Subject;
            mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();

            var smtp = _settings.Smtp;
            using var client = new SmtpClient();
            var socketOptions = smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect;
            await client.ConnectAsync(smtp.Host, smtp.Port, socketOptions, cancellationToken);

            if (!string.IsNullOrEmpty(smtp.Username))
                await client.AuthenticateAsync(smtp.Username, smtp.Password ?? string.Empty, cancellationToken);

            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email '{Subject}' inviata a utente", message.Subject);
        }
    }

    /// <summary>
    /// Usato quando SMTP non è configurato: non invia nulla, registra l'email nel log.
    /// In Development il contenuto testuale (con i link) viene loggato per poter testare i flussi.
    /// </summary>
    public class LogOnlyEmailSender : IEmailSender
    {
        private readonly IHostEnvironment _environment;
        private readonly ILogger<LogOnlyEmailSender> _logger;

        public LogOnlyEmailSender(IHostEnvironment environment, ILogger<LogOnlyEmailSender> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public Task SendAsync(string toAddress, string toName, EmailMessage message, CancellationToken cancellationToken = default)
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogInformation("[EMAIL NON INVIATA - SMTP non configurato] A: {To} | Oggetto: {Subject}\n{Body}",
                    toAddress, message.Subject, message.TextBody);
            }
            else
            {
                _logger.LogWarning("SMTP non configurato: email '{Subject}' non inviata. Configurare la sezione Email:Smtp.", message.Subject);
            }
            return Task.CompletedTask;
        }
    }
}
