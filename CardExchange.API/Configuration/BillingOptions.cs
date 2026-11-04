namespace CardExchange.API.Configuration
{
    /// <summary>
    /// Pagamenti degli abbonamenti alla piattaforma (sezione "Billing"). Riguarda solo Premium e Negozio:
    /// tra utenti non passano pagamenti, che restano fuori dall'app.
    /// </summary>
    public class BillingOptions
    {
        public BankTransferOptions BankTransfer { get; set; } = new();
        public StripeOptions Stripe { get; set; } = new();
    }

    public class BankTransferOptions
    {
        public bool Enabled { get; set; } = true;
        public string? AccountHolder { get; set; }
        public string? Iban { get; set; }
        public string? Bic { get; set; }
        public string? BankName { get; set; }

        public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(Iban) && !string.IsNullOrWhiteSpace(AccountHolder);
    }

    public class StripeOptions
    {
        public string? SecretKey { get; set; }
        public string? WebhookSecret { get; set; }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey) && !string.IsNullOrWhiteSpace(WebhookSecret);
    }
}
