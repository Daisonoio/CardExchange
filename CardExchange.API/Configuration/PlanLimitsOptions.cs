namespace CardExchange.API.Configuration
{
    /// <summary>
    /// Limiti del piano gratuito configurabili (sezione "PlanLimits").
    /// I piani a pagamento hanno i propri limiti nella tabella SubscriptionPlans.
    /// </summary>
    public class PlanLimitsOptions
    {
        /// <summary>
        /// Numero massimo di voci in collezione per il piano gratuito (una voce = stessa carta, condizione,
        /// lingua e finitura, qualunque sia la quantità). Vuoto o 0 = nessun limite.
        /// Utile in futuro per distinguere i privati dai negozi con un piano dedicato.
        /// </summary>
        public int? FreeMaxCards { get; set; }

        public int EffectiveFreeMaxCards => FreeMaxCards is > 0 ? FreeMaxCards.Value : int.MaxValue;
    }
}
