namespace CardExchange.Core.Entities
{
    /// <summary>Riepilogo mensile di un negozio, calcolato una volta all'inizio del mese successivo.</summary>
    public class StoreMonthlyReport : BaseEntity
    {
        public int StoreId { get; set; }
        /// <summary>Primo giorno del mese (ora italiana).</summary>
        public DateOnly Month { get; set; }
        /// <summary>Valori del mese e del mese precedente (per il confronto).</summary>
        public string MetricsJson { get; set; } = "{}";
        public DateTime? SentAt { get; set; }

        public virtual Store Store { get; set; } = null!;
    }
}
