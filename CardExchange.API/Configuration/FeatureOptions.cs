namespace CardExchange.API.Configuration
{
    /// <summary>Interruttori delle funzioni (sezione "Features" della configurazione).</summary>
    public class FeatureOptions
    {
        /// <summary>
        /// Piani a pagamento, promozioni e acquisti. Spenti (default): le funzioni dipendono solo dal tipo di profilo
        /// e l'interfaccia non mostra piani, prezzi né inviti all'upgrade.
        /// </summary>
        public bool PaidPlans { get; set; }
    }
}
