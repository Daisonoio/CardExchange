namespace CardExchange.Core.Entities
{
    /// <summary>
    /// Registro delle modifiche fatte dagli amministratori su un utente (tipo di profilo, account, chat):
    /// chi, quando, cosa e perché. Non si modifica né si cancella dall'app.
    /// </summary>
    public class AdminAuditEntry
    {
        public long Id { get; set; }
        /// <summary>Amministratore che ha agito (null = azione automatica, es. negozio verificato).</summary>
        public int? AdminId { get; set; }
        public int TargetUserId { get; set; }
        /// <summary>Codice dell'azione: ProfileType, Deactivate, Reactivate, ChatLifted.</summary>
        public string Action { get; set; } = string.Empty;
        /// <summary>Dettaglio leggibile, es. "Collector → Store".</summary>
        public string? Detail { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
