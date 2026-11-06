using System.Text.RegularExpressions;
using CardExchange.Core.Entities;

namespace CardExchange.Core.Rules
{
    /// <summary>Tariffe, limiti e revisione automatica delle promozioni. Le tariffe sono in crediti (1 credito = 4 € a listino).</summary>
    public static class PromotionRules
    {
        public const int MinRadiusKm = 5;
        public const int MaxRadiusKm = 50;
        public const decimal CreditPriceEur = 4m;
        /// <summary>Al massimo 2 notifiche sponsorizzate a settimana per persona.</summary>
        public const int MaxSponsoredPerWeek = 2;
        public static readonly TimeSpan CapWindow = TimeSpan.FromDays(7);
        public const int MaxFeaturedDays = 14;
        /// <summary>Durata di uno spazio sponsorizzato locale.</summary>
        public const int SlotDays = 7;

        public static bool IsSlot(PromotionType t) => t is PromotionType.StoreSlot or PromotionType.ShowcaseSlot;
        /// <summary>Sopra 2 segnalazioni ogni 1.000 consegne (e almeno 3 segnalazioni) la promozione si ferma.</summary>
        public const int ReportsToPause = 3;
        public const double ReportsPerThousandAlarm = 2;

        public static int ClampRadius(int km) => Math.Clamp(km, MinRadiusKm, MaxRadiusKm);

        /// <summary>Prezzo in crediti: evento in evidenza 3–5 (12–20 €), "Sono qui" sponsorizzato 1–3 (4–12 €), secondo il raggio.</summary>
        public static int PriceCredits(PromotionType type, int radiusKm)
        {
            var step = radiusKm <= 10 ? 0 : radiusKm <= 25 ? 1 : 2;
            return type switch
            {
                PromotionType.FeaturedEvent => 3 + step,
                PromotionType.SponsoredBroadcast => 1 + step,
                PromotionType.StoreSlot or PromotionType.ShowcaseSlot => 2 + step,
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
        }

        public static decimal PriceEur(PromotionType type, int radiusKm) => PriceCredits(type, radiusKm) * CreditPriceEur;

        private static readonly Regex Link = new(@"https?://|www\.|\b[a-z0-9-]+\.(com|it|net|org|io|shop|store|ly|me)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Phone = new(@"(\+?\d[\s.-]?){9,}", RegexOptions.Compiled);
        private static readonly string[] Banned =
        {
            "whatsapp", "telegram", "paypal", "bonifico", "pagamento anticipato", "spedizione gratuita", "contanti subito",
            "scommess", "casino", "crypto", "bitcoin", "vietato ai minori"
        };

        /// <summary>Motivo del rifiuto, oppure null se il testo va bene. Niente link esterni, contatti o pagamenti fuori dall'app.</summary>
        public static string? Review(params string?[] texts)
        {
            var text = string.Join(" ", texts.Where(t => !string.IsNullOrWhiteSpace(t)));
            if (Link.IsMatch(text)) return "Le promozioni non possono contenere link esterni";
            if (Phone.IsMatch(text)) return "Le promozioni non possono contenere numeri di telefono";
            var lower = text.ToLowerInvariant();
            var word = Banned.FirstOrDefault(lower.Contains);
            return word == null ? null : "Il testo contiene parole non ammesse nelle promozioni";
        }

        /// <summary>Troppe segnalazioni rispetto alle consegne: la promozione si ferma.</summary>
        public static bool ShouldPause(int reports, int reach) =>
            reports >= ReportsToPause && reports * 1000.0 / Math.Max(1, reach) > ReportsPerThousandAlarm;
    }
}
