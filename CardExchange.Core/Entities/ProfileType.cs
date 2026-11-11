namespace CardExchange.Core.Entities
{
    /// <summary>
    /// Tipo di profilo: decide le funzioni disponibili (non il piano). Alla registrazione tutti sono Collezionista;
    /// il passaggio a Venditore, Negozio od Organizzatore lo fa un amministratore (anche direttamente nel database:
    /// colonna Users.ProfileType, 0 = Collezionista, 1 = Venditore, 2 = Negozio, 3 = Organizzatore).
    /// Ogni tipo include tutte le funzioni del Collezionista.
    /// </summary>
    public enum ProfileType
    {
        Collector = 0,
        Seller = 1,
        Store = 2,
        Organizer = 3
    }
}
