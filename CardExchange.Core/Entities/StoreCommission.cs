namespace CardExchange.Core.Entities
{
    public enum CommissionStatus
    {
        /// <summary>Maturata, in attesa dei 30 giorni per eventuali rimborsi.</summary>
        Pending = 1,
        /// <summary>Superati i 30 giorni: sarà liquidata alla prossima chiusura mensile.</summary>
        Approved = 2,
        /// <summary>Scalata dal piano del negozio.</summary>
        Credited = 3,
        /// <summary>Pagata con bonifico.</summary>
        Paid = 4,
        /// <summary>Abbonamento rimborsato o stornato: nessun accredito.</summary>
        Reversed = 5
    }

    /// <summary>
    /// Commissione del negozio su un pagamento Premium di un utente che si è iscritto dal suo QR.
    /// Il negozio vede solo importi e date, mai chi si è abbonato.
    /// </summary>
    public class StoreCommission : BaseEntity
    {
        public int StoreId { get; set; }
        public int UserSubscriptionId { get; set; }
        /// <summary>Riferimento del pagamento (fattura Stripe o bonifico): una commissione per pagamento.</summary>
        public string PaymentReference { get; set; } = string.Empty;
        public string? PaymentIntentId { get; set; }
        /// <summary>Incassato al netto dell'IVA.</summary>
        public decimal GrossEur { get; set; }
        public decimal Rate { get; set; } = 0.25m;
        public decimal AmountEur { get; set; }
        public CommissionStatus Status { get; set; } = CommissionStatus.Pending;
        public DateTime PaidAt { get; set; }
        public DateTime AvailableAt { get; set; }
        public int? PayoutId { get; set; }
    }

    public enum PayoutMethod { PlanCredit = 1, BankTransfer = 2 }

    public enum PayoutStatus
    {
        /// <summary>Da applicare (es. il negozio non paga ancora con Stripe): lo fa un amministratore.</summary>
        Pending = 1,
        /// <summary>Sconto registrato sul conto Stripe del negozio o bonifico eseguito.</summary>
        Done = 2
    }

    /// <summary>Liquidazione mensile delle commissioni di un negozio.</summary>
    public class StorePayout : BaseEntity
    {
        public int StoreId { get; set; }
        public PayoutMethod Method { get; set; } = PayoutMethod.PlanCredit;
        public PayoutStatus Status { get; set; } = PayoutStatus.Pending;
        public decimal AmountEur { get; set; }
        public DateOnly PeriodMonth { get; set; }
        public string? Reference { get; set; }
    }
}
