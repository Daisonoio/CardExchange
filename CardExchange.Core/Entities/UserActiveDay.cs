namespace CardExchange.Core.Entities
{
    /// <summary>
    /// Giorno (ora italiana) in cui un utente ha usato l'app. Serve solo alle metriche interne
    /// (utenti attivi a settimana, ritorno dopo 4 settimane); si cancella dopo 400 giorni.
    /// </summary>
    public class UserActiveDay
    {
        public int UserId { get; set; }
        public DateOnly Day { get; set; }
    }
}
