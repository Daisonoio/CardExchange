namespace CardExchange.Core.Constants
{
    public static class FreeTierLimits
    {
        public const int MaxCards = 50;
        public const int MaxWishlistItems = 20;
        public const int MaxActiveTradeOffers = 5;
        public const int MaxDailyMessages = 10;
        public const int TradeOfferExpirationDays = 7;
    }

    public static class Permissions
    {
        // Cards
        public const string CardsReadAll = "CARDS.READ.ALL";
        public const string CardsCreateOwn = "CARDS.CREATE.OWN";
        public const string CardsUpdateOwn = "CARDS.UPDATE.OWN";
        public const string CardsDeleteOwn = "CARDS.DELETE.OWN";
        public const string CardsDeleteAny = "CARDS.DELETE.ANY";

        // Trades
        public const string TradesCreateOwn = "TRADES.CREATE.OWN";
        public const string TradesReadOwn = "TRADES.READ.OWN";
        public const string TradesReadAll = "TRADES.READ.ALL";
        public const string TradesUpdateOwn = "TRADES.UPDATE.OWN";

        // Search
        public const string SearchBasic = "SEARCH.BASIC";
        public const string SearchGeographic = "SEARCH.GEOGRAPHIC";
        public const string SearchAdvanced = "SEARCH.ADVANCED";

        // Catalog
        public const string CatalogCreate = "CATALOG.CREATE";
        public const string CatalogUpdate = "CATALOG.UPDATE";
        public const string CatalogDelete = "CATALOG.DELETE";

        // Users
        public const string UsersReadAll = "USERS.READ.ALL";
        public const string UsersUpdateOwn = "USERS.UPDATE.OWN";
        public const string UsersDeleteAny = "USERS.DELETE.ANY";

        // Events
        public const string EventsCreate = "EVENTS.CREATE";
        public const string EventsReadAll = "EVENTS.READ.ALL";
        public const string EventsUpdateOwn = "EVENTS.UPDATE.OWN";
        public const string EventsDeleteAny = "EVENTS.DELETE.ANY";

        // Admin
        public const string AdminPanel = "ADMIN.PANEL";
        public const string AdminStats = "ADMIN.STATS";
    }

    public static class ErrorMessages
    {
        public const string UserNotFound = "Utente non trovato";
        public const string CardNotFound = "Carta non trovata";
        public const string TradeOfferNotFound = "Offerta di scambio non trovata";
        public const string Unauthorized = "Non autorizzato";
        public const string Forbidden = "Accesso negato";
        public const string FreeTierLimitReached = "Hai raggiunto il limite del tuo piano. Passa a Premium per sbloccare più funzionalità.";
        public const string InvalidRequest = "Richiesta non valida";
        public const string InternalError = "Errore interno del server";
        public const string TradeOfferExpired = "L'offerta di scambio è scaduta";
        public const string CannotTradeWithSelf = "Non puoi fare uno scambio con te stesso";
        public const string CardNotAvailable = "La carta non è disponibile per lo scambio";
        public const string AlreadyReviewed = "Hai già lasciato una recensione per questo scambio";
        public const string TradeNotCompleted = "Lo scambio deve essere completato prima di poter lasciare una recensione";
        public const string EventNotFound = "Evento non trovato";
        public const string EventFull = "L'evento ha raggiunto il numero massimo di partecipanti";
        public const string AlreadyRegistered = "Sei già iscritto a questo evento";
        public const string AlertNotFound = "Alert non trovato";
    }

    public static class RoleNames
    {
        public const string SuperAdmin = "SuperAdmin";
        public const string Admin = "Admin";
        public const string Moderator = "Moderator";
        public const string PremiumUser = "PremiumUser";
        public const string User = "User";
        public const string Guest = "Guest";
    }
}
