using CardExchange.Core.Entities;

namespace CardExchange.Core.Rules
{
    /// <summary>
    /// Prezzo di riferimento in euro di una copia fisica, in base alla finitura.
    /// </summary>
    public static class CardPricing
    {
        /// <summary>
        /// Per le finiture non normali usa il prezzo foil quando disponibile, altrimenti il prezzo base.
        /// Restituisce <paramref name="fallback"/> (es. il valore stimato inserito dall'utente) se il catalogo non ha prezzi.
        /// </summary>
        public static decimal? ReferencePriceEur(CardInfo? info, CardFinish finish, decimal? fallback = null)
        {
            if (info == null) return fallback;
            var price = finish != CardFinish.Normal && info.PriceEurFoil.HasValue
                ? info.PriceEurFoil
                : info.PriceEur;
            return price ?? fallback;
        }
    }
}
