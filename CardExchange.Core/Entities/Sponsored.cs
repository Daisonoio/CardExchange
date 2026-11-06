namespace CardExchange.Core.Entities
{
    /// <summary>Dove compare uno spazio sponsorizzato. Mai in chat, proposte di scambio, conferme o appuntamenti.</summary>
    public enum SlotPlacement { HomeFeed = 1, ExploreCards = 2, ExploreStores = 3 }

    /// <summary>
    /// Spazio sponsorizzato nativo: una scheda "Sponsorizzato" tra le schede normali.
    /// Viene da una promozione locale (negozio o banco) o da uno sponsor di marca.
    /// </summary>
    public class SponsoredSlot : BaseEntity
    {
        public int? PromotionId { get; set; }
        public int? SponsorshipId { get; set; }
        /// <summary>Negozio o venditore che ha pagato (null per gli sponsor di marca).</summary>
        public int? OwnerUserId { get; set; }
        public SlotPlacement Placement { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        /// <summary>Solo pagine interne all'app (iniziano con "/").</summary>
        public string TargetUrl { get; set; } = "/";
        public int FrequencyCapPerDay { get; set; } = 3;
        /// <summary>Zona (null = tutta Italia, l'unico caso mostrato ai minorenni).</summary>
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public int? RadiusKm { get; set; }
        public string? City { get; set; }
        public int? GameId { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public bool IsActive { get; set; } = true;
        /// <summary>Persone diverse che l'hanno visto, per giorno (deduplicato).</summary>
        public int Impressions { get; set; }
        public int Clicks { get; set; }

        public bool IsBrand => SponsorshipId != null;
    }

    /// <summary>Visualizzazioni di uno spazio da parte di una persona in un giorno: deduplica e limite di frequenza.</summary>
    public class SlotImpression
    {
        public long Id { get; set; }
        public int SlotId { get; set; }
        public int UserId { get; set; }
        public DateOnly Day { get; set; }
        public int Count { get; set; }
    }

    /// <summary>Marchio che sostiene premi, serate e spazi (contratto diretto, gestito dagli amministratori).</summary>
    public class Sponsor : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? WebsiteUrl { get; set; }
        /// <summary>Contatto commerciale: visibile solo agli amministratori.</summary>
        public string? ContactEmail { get; set; }
        public string? Description { get; set; }
        /// <summary>Clic verso il sito, solo come totale (nessun tracciamento dell'utente).</summary>
        public int WebsiteClicks { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public enum SponsorshipScope
    {
        /// <summary>Un evento (ScopeRef = id dell'evento).</summary>
        Event = 1,
        /// <summary>Tutte le serate di scambio di una città nel periodo (ScopeRef = città).</summary>
        CityTradeNights = 2,
        /// <summary>Tutti gli eventi del periodo, in una città o ovunque (ScopeRef = città o vuoto).</summary>
        Month = 3
    }

    public enum SponsorshipPlacement { EventPage = 1, TradeNightBanner = 2, Slot = 3 }

    public class Sponsorship : BaseEntity
    {
        public int SponsorId { get; set; }
        public SponsorshipScope Scope { get; set; }
        public string? ScopeRef { get; set; }
        public SponsorshipPlacement Placement { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        /// <summary>Solo per lo spazio: dove compare, titolo, testo e gioco.</summary>
        public SlotPlacement? SlotPlacement { get; set; }
        public string? SlotTitle { get; set; }
        public string? SlotBody { get; set; }
        public int? GameId { get; set; }

        public virtual Sponsor Sponsor { get; set; } = null!;
    }
}
