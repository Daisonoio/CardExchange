namespace CardExchange.API.Configuration
{
    /// <summary>
    /// Configurazione invio email (sezione "Email").
    /// Se Smtp:Host è vuoto le email non vengono inviate ma solo registrate nel log.
    /// </summary>
    public class EmailSettings
    {
        public string FromAddress { get; set; } = "no-reply@cardexchange.local";
        public string FromName { get; set; } = "CardExchange";

        /// <summary>URL pubblico del frontend, usato per costruire i link nelle email (senza slash finale).</summary>
        public string FrontendBaseUrl { get; set; } = "http://localhost:5173";

        public SmtpSettings Smtp { get; set; } = new();
    }

    public class SmtpSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public string? Username { get; set; }
        public string? Password { get; set; }

        /// <summary>true = STARTTLS (porta 587), false = SSL implicito (porta 465).</summary>
        public bool UseStartTls { get; set; } = true;
    }
}
