namespace CardExchange.API.Configuration
{
    /// <summary>
    /// Richieste di inserimento dei negozi (sezione "StoreRequests"). L'indirizzo resta solo nella configurazione
    /// del server: non viene mai mandato al frontend, così non finisce in pagina e non attira spam.
    /// </summary>
    public class StoreRequestOptions
    {
        /// <summary>Casella che riceve il riepilogo di ogni richiesta. Vuota: nessuna email (resta nel log).</summary>
        public string NotifyEmail { get; set; } = string.Empty;
    }
}
