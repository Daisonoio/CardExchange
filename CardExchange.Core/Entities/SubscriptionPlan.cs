using System.ComponentModel.DataAnnotations;

namespace CardExchange.Core.Entities
{
    public enum SubscriptionTier
    {
        Free = 0,
        Premium = 1,
        /// <summary>Piano Negozio: scheda in evidenza, badge, statistiche e riepilogo mensile.</summary>
        Store = 2,
        /// <summary>Piano Banco: per chi vende ai mercatini e alle fiere.</summary>
        Seller = 3,
        /// <summary>Piano Negozio Pro: domanda locale, lista acquisti, prenota e ritira.</summary>
        StorePro = 4
    }

    public enum BillingCycle
    {
        Monthly = 1,
        Annual = 2
    }

    public class SubscriptionPlan : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;

        public BillingCycle Cycle { get; set; } = BillingCycle.Monthly;

        public decimal Price { get; set; }

        [MaxLength(3)]
        public string Currency { get; set; } = "EUR";

        public bool IsActive { get; set; } = true;

        // Limiti del piano
        public int MaxCards { get; set; } = 50;
        public int MaxWishlistItems { get; set; } = 20;
        public int MaxActiveTradeOffers { get; set; } = 5;
        public int MaxDailyMessages { get; set; } = 10;
        public bool CanSearchAdvanced { get; set; } = false;
        public bool CanSearchGeographic { get; set; } = false;
        public bool CanExportCollection { get; set; } = false;
        public bool CanViewStatistics { get; set; } = false;
        public bool CanSaveSearches { get; set; } = false;
        public bool HasPriorityListing { get; set; } = false;
        public bool HasVerifiedBadge { get; set; } = false;
        public bool HasWishlistAlerts { get; set; } = false;

        // Diritti aggiunti con il nuovo listino (letti solo tramite IEntitlementService)
        /// <summary>Avvisi "Sono qui" al giorno.</summary>
        public int MaxBroadcastsPerDay { get; set; } = 3;
        /// <summary>Raggio massimo di ricerca e avvisi (km).</summary>
        public int MaxRadiusKm { get; set; } = 30;
        /// <summary>Prenotazioni in negozio attive contemporaneamente.</summary>
        public int MaxActiveReservations { get; set; } = 3;
        /// <summary>Crediti promozione inclusi ogni mese.</summary>
        public int MonthlyPromotionCredits { get; set; }
        /// <summary>Avvisi sulle carte della wishlist subito (gli altri ricevono un riepilogo serale).</summary>
        public bool HasInstantAlerts { get; set; }
        /// <summary>Vede e prenota per primo le carte appena caricate da negozi e banchi.</summary>
        public bool HasEarlyAccess { get; set; }
        /// <summary>Avvisi di prezzo e storico prezzi.</summary>
        public bool HasPriceAlerts { get; set; }
        /// <summary>Negozio in evidenza nella sezione Locale e come punto d'incontro.</summary>
        public bool HasFeaturedStore { get; set; }
        public bool HasLocalDemand { get; set; }
        public bool HasBuylist { get; set; }
        public bool HasReservations { get; set; }
        /// <summary>Tessera fedeltà con timbri (Negozio e Negozio Pro).</summary>
        public bool HasLoyalty { get; set; }
        /// <summary>Espositori sui propri eventi (profili Negozio e Organizzatore).</summary>
        public bool HasEventExhibitors { get; set; }
        public bool HasSellerShowcase { get; set; }
        public bool HidesBrandSponsors { get; set; }
        /// <summary>Prezzo Stripe collegato (facoltativo: senza, il Checkout usa il prezzo del piano).</summary>
        [System.ComponentModel.DataAnnotations.MaxLength(100)]
        public string? StripePriceId { get; set; }
        /// <summary>Ordine nella pagina dei piani.</summary>
        public int SortOrder { get; set; }

        // Relazioni
        public virtual ICollection<UserSubscription> Subscriptions { get; set; } = new List<UserSubscription>();
    }
}
