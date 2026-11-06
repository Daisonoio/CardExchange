using CardExchange.Core.Entities;

namespace CardExchange.Core.Rules
{
    /// <summary>Funzioni che dipendono dal piano.</summary>
    public enum Capability
    {
        AdvancedSearch,
        GeographicSearch,
        ExportCollection,
        Statistics,
        SavedSearches,
        PriorityListing,
        VerifiedBadge,
        WishlistAlerts,
        InstantAlerts,
        EarlyAccess,
        PriceAlerts,
        FeaturedStore,
        LocalDemand,
        Buylist,
        Reservations,
        SellerShowcase,
        HidesBrandSponsors,
        Loyalty
    }

    /// <summary>Limiti numerici che dipendono dal piano (int.MaxValue = illimitato).</summary>
    public enum PlanLimit
    {
        Cards,
        WishlistItems,
        ActiveTradeOffers,
        DailyMessages,
        BroadcastsPerDay,
        RadiusKm,
        ActiveReservations,
        MonthlyPromotionCredits
    }

    /// <summary>Diritti effettivi di un utente: unione dei piani attivi (più diritti, limiti più alti).</summary>
    public sealed record UserEntitlements(
        SubscriptionTier Tier,
        string PlanName,
        IReadOnlySet<Capability> Capabilities,
        IReadOnlyDictionary<PlanLimit, int> Limits)
    {
        public bool Has(Capability c) => Capabilities.Contains(c);
        public int Limit(PlanLimit l) => Limits.TryGetValue(l, out var v) ? v : 0;
        public bool IsPaid => Tier != SubscriptionTier.Free;

        /// <summary>Etichetta mostrata accanto al nome (Premium, Negozio…), solo con il badge verificato.</summary>
        public string? Badge => Has(Capability.VerifiedBadge) ? BadgeFor(Tier) : null;

        /// <summary>Etichetta pubblica del piano: "Store" per Negozio e Negozio Pro, "Premium" per Premium e Banco.</summary>
        public static string BadgeFor(SubscriptionTier t) => t switch
        {
            SubscriptionTier.Store or SubscriptionTier.StorePro => "Store",
            SubscriptionTier.Free => "Free",
            _ => "Premium"
        };

        /// <summary>Ordine dei piani dal più al meno completo, per scegliere quello da mostrare.</summary>
        public static int Rank(SubscriptionTier t) => t switch
        {
            SubscriptionTier.StorePro => 4,
            SubscriptionTier.Store => 3,
            SubscriptionTier.Seller => 2,
            SubscriptionTier.Premium => 1,
            _ => 0
        };

        public static IEnumerable<Capability> CapabilitiesOf(SubscriptionPlan p)
        {
            if (p.CanSearchAdvanced) yield return Capability.AdvancedSearch;
            if (p.CanSearchGeographic) yield return Capability.GeographicSearch;
            if (p.CanExportCollection) yield return Capability.ExportCollection;
            if (p.CanViewStatistics) yield return Capability.Statistics;
            if (p.CanSaveSearches) yield return Capability.SavedSearches;
            if (p.HasPriorityListing) yield return Capability.PriorityListing;
            if (p.HasVerifiedBadge) yield return Capability.VerifiedBadge;
            if (p.HasWishlistAlerts) yield return Capability.WishlistAlerts;
            if (p.HasInstantAlerts) yield return Capability.InstantAlerts;
            if (p.HasEarlyAccess) yield return Capability.EarlyAccess;
            if (p.HasPriceAlerts) yield return Capability.PriceAlerts;
            if (p.HasFeaturedStore) yield return Capability.FeaturedStore;
            if (p.HasLocalDemand) yield return Capability.LocalDemand;
            if (p.HasBuylist) yield return Capability.Buylist;
            if (p.HasReservations) yield return Capability.Reservations;
            if (p.HasSellerShowcase) yield return Capability.SellerShowcase;
            if (p.HidesBrandSponsors) yield return Capability.HidesBrandSponsors;
            if (p.HasLoyalty) yield return Capability.Loyalty;
        }

        public static IReadOnlyDictionary<PlanLimit, int> LimitsOf(SubscriptionPlan p) => new Dictionary<PlanLimit, int>
        {
            [PlanLimit.Cards] = p.MaxCards,
            [PlanLimit.WishlistItems] = p.MaxWishlistItems,
            [PlanLimit.ActiveTradeOffers] = p.MaxActiveTradeOffers,
            [PlanLimit.DailyMessages] = p.MaxDailyMessages,
            [PlanLimit.BroadcastsPerDay] = p.MaxBroadcastsPerDay,
            [PlanLimit.RadiusKm] = p.MaxRadiusKm,
            [PlanLimit.ActiveReservations] = p.MaxActiveReservations,
            [PlanLimit.MonthlyPromotionCredits] = p.MonthlyPromotionCredits,
        };

        /// <summary>Unisce il piano gratuito con i piani attivi: diritti sommati, limiti massimi.</summary>
        public static UserEntitlements Combine(SubscriptionPlan free, IEnumerable<SubscriptionPlan> active)
        {
            var plans = active.ToList();
            var caps = new HashSet<Capability>(CapabilitiesOf(free));
            var limits = new Dictionary<PlanLimit, int>(LimitsOf(free));
            foreach (var p in plans)
            {
                caps.UnionWith(CapabilitiesOf(p));
                foreach (var (k, v) in LimitsOf(p)) limits[k] = Math.Max(limits[k], v);
            }
            var top = plans.OrderByDescending(p => Rank(p.Tier)).FirstOrDefault();
            return new UserEntitlements(top?.Tier ?? SubscriptionTier.Free, top?.Name ?? "Gratuito", caps, limits);
        }
    }

    /// <summary>
    /// Listino e diritti di ogni piano: unica fonte usata per creare e aggiornare i piani nel database.
    /// </summary>
    public static class PlanCatalog
    {
        public const int Unlimited = int.MaxValue;

        public static SubscriptionPlan Free(int maxCards) => new()
        {
            Name = "Gratuito", Tier = SubscriptionTier.Free, Price = 0,
            MaxCards = maxCards, MaxWishlistItems = 20, MaxActiveTradeOffers = 5, MaxDailyMessages = 10,
            MaxBroadcastsPerDay = 3, MaxRadiusKm = 30, MaxActiveReservations = 3
        };

        private static SubscriptionPlan PremiumRights(SubscriptionPlan p)
        {
            p.MaxCards = p.MaxWishlistItems = p.MaxActiveTradeOffers = p.MaxDailyMessages = Unlimited;
            p.MaxBroadcastsPerDay = 3;
            p.MaxRadiusKm = 200;
            p.MaxActiveReservations = 5;
            p.CanSearchAdvanced = p.CanSearchGeographic = p.CanExportCollection = p.CanViewStatistics = p.CanSaveSearches = true;
            p.HasPriorityListing = p.HasVerifiedBadge = p.HasWishlistAlerts = true;
            p.HasInstantAlerts = p.HasEarlyAccess = p.HasPriceAlerts = p.HidesBrandSponsors = true;
            return p;
        }

        /// <summary>Piani in vendita (e il vecchio Negozio annuale, non più in vendita ma valido fino alla scadenza).</summary>
        public static IReadOnlyList<SubscriptionPlan> All() => new[]
        {
            PremiumRights(new SubscriptionPlan { Name = "Premium Mensile", SortOrder = 10, Tier = SubscriptionTier.Premium, Cycle = BillingCycle.Monthly, Price = 4.99m,
                Description = "Avvisi immediati sulle carte che cerchi, raggio fino a 200 km, accesso anticipato alle carte dei negozi, priorità nei match e badge verificato." }),
            PremiumRights(new SubscriptionPlan { Name = "Premium Annuale", SortOrder = 11, Tier = SubscriptionTier.Premium, Cycle = BillingCycle.Annual, Price = 47.99m,
                Description = "Tutti i vantaggi Premium per un anno: due mesi gratis rispetto al mensile." }),
            Seller(PremiumRights(new SubscriptionPlan { Name = "Banco", SortOrder = 20, Tier = SubscriptionTier.Seller, Cycle = BillingCycle.Monthly, Price = 9.99m,
                Description = "Per chi vende a mercatini e fiere: vetrina del banco, più avvisi \"Sono qui\" e crediti promozione ogni mese, più tutti i vantaggi Premium." })),
            Store(PremiumRights(new SubscriptionPlan { Name = "Negozio", SortOrder = 30, Tier = SubscriptionTier.Store, Cycle = BillingCycle.Monthly, Price = 19m,
                Description = "Scheda in evidenza e punto d'incontro consigliato, QR per i clienti, tessera fedeltà con timbri, statistiche e riepilogo mensile, eventi e tornei." })),
            Pro(Store(PremiumRights(new SubscriptionPlan { Name = "Negozio Pro", SortOrder = 40, Tier = SubscriptionTier.StorePro, Cycle = BillingCycle.Monthly, Price = 39m,
                Description = "Tutto il piano Negozio più domanda locale, lista acquisti, prenota e ritira e crediti promozione ogni mese." }))),
            Store(PremiumRights(new SubscriptionPlan { Name = "Negozio Annuale", SortOrder = 90, Tier = SubscriptionTier.Store, Cycle = BillingCycle.Annual, Price = 99m, IsActive = false,
                Description = "Vecchio piano Negozio: resta valido fino alla scadenza, poi si passa al nuovo listino." })),
        };

        private static SubscriptionPlan Seller(SubscriptionPlan p)
        {
            p.MaxBroadcastsPerDay = 8;
            p.MonthlyPromotionCredits = 2;
            p.HasSellerShowcase = true;
            return p;
        }

        private static SubscriptionPlan Store(SubscriptionPlan p)
        {
            p.MaxBroadcastsPerDay = 8;
            p.HasFeaturedStore = true;
            p.HasLoyalty = true;
            return p;
        }

        private static SubscriptionPlan Pro(SubscriptionPlan p)
        {
            p.MonthlyPromotionCredits = 4;
            p.HasLocalDemand = p.HasBuylist = p.HasReservations = true;
            return p;
        }

        /// <summary>Copia diritti, limiti e testo sul piano salvato (il prezzo resta quello in vendita).</summary>
        public static void ApplyRights(SubscriptionPlan source, SubscriptionPlan target)
        {
            target.Description = source.Description;
            target.Tier = source.Tier;
            target.SortOrder = source.SortOrder;
            target.MaxCards = source.MaxCards; target.MaxWishlistItems = source.MaxWishlistItems;
            target.MaxActiveTradeOffers = source.MaxActiveTradeOffers; target.MaxDailyMessages = source.MaxDailyMessages;
            target.MaxBroadcastsPerDay = source.MaxBroadcastsPerDay; target.MaxRadiusKm = source.MaxRadiusKm;
            target.MaxActiveReservations = source.MaxActiveReservations; target.MonthlyPromotionCredits = source.MonthlyPromotionCredits;
            target.CanSearchAdvanced = source.CanSearchAdvanced; target.CanSearchGeographic = source.CanSearchGeographic;
            target.CanExportCollection = source.CanExportCollection; target.CanViewStatistics = source.CanViewStatistics;
            target.CanSaveSearches = source.CanSaveSearches; target.HasPriorityListing = source.HasPriorityListing;
            target.HasVerifiedBadge = source.HasVerifiedBadge; target.HasWishlistAlerts = source.HasWishlistAlerts;
            target.HasInstantAlerts = source.HasInstantAlerts; target.HasEarlyAccess = source.HasEarlyAccess;
            target.HasPriceAlerts = source.HasPriceAlerts; target.HasFeaturedStore = source.HasFeaturedStore;
            target.HasLocalDemand = source.HasLocalDemand; target.HasBuylist = source.HasBuylist;
            target.HasReservations = source.HasReservations; target.HasLoyalty = source.HasLoyalty; target.HasSellerShowcase = source.HasSellerShowcase;
            target.HidesBrandSponsors = source.HidesBrandSponsors;
            if (!source.IsActive) target.IsActive = false;
        }
    }
}
