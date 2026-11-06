namespace CardExchange.Core.Entities
{
    /// <summary>Base: elenco espositori e "Sono qui". Plus: anche piantina ed evento in evidenza. Regione: evento in evidenza fino a 100 km.</summary>
    public enum EventPackageTier { Base = 1, Plus = 2, Region = 3 }

    /// <summary>Pacchetto acquistato dall'organizzatore di una fiera o di un mercatino (49–149 €).</summary>
    public class EventPackage : BaseEntity
    {
        public int EventId { get; set; }
        public EventPackageTier Tier { get; set; }
        public int? PurchaseId { get; set; }
        /// <summary>Attivato a mano da un amministratore (es. pagato con bonifico).</summary>
        public int? ActivatedById { get; set; }
        public DateTime ActivatedAt { get; set; }
        public string? FloorPlanImageUrl { get; set; }
        public int? PromotionId { get; set; }
        /// <summary>Visite alla pagina dell'evento da quando c'è il pacchetto.</summary>
        public int PageViews { get; set; }

        public virtual Event Event { get; set; } = null!;
    }

    /// <summary>Espositore di un evento: invitato con un link, poi legato a un utente o a un suo negozio.</summary>
    public class EventExhibitor : BaseEntity
    {
        public int EventId { get; set; }
        public int? UserId { get; set; }
        public int? StoreId { get; set; }
        /// <summary>Nome indicato dall'organizzatore, finché l'invito non è accettato.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Stand, es. "Pad. 3 – B12".</summary>
        public string BoothCode { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string InviteToken { get; set; } = string.Empty;
        public DateTime? AcceptedAt { get; set; }

        public virtual Event Event { get; set; } = null!;
        public virtual User? User { get; set; }
        public virtual Store? Store { get; set; }
    }
}
