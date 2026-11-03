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

        /// <summary>
        /// Percentuale del prezzo di mercato (riferito a Near Mint) per ogni condizione.
        /// Stima indicativa in linea con le prassi dei marketplace europei.
        /// </summary>
        public static decimal ConditionFactor(CardCondition condition) => condition switch
        {
            CardCondition.Mint => 1.00m,
            CardCondition.NearMint => 1.00m,
            CardCondition.Excellent => 0.90m,
            CardCondition.Good => 0.80m,
            CardCondition.LightlyPlayed => 0.70m,
            CardCondition.ModeratelyPlayed => 0.60m,
            CardCondition.HeavilyPlayed => 0.45m,
            CardCondition.Damaged => 0.30m,
            _ => 1.00m
        };

        /// <summary>
        /// Valore stimato in euro di una singola copia: prezzo di mercato per la sua finitura
        /// (o il valore inserito dall'utente se il catalogo non ha prezzi) corretto per la condizione.
        /// Null se non c'è nessun prezzo disponibile.
        /// </summary>
        public static decimal? CopyValueEur(Card card)
        {
            var basePrice = ReferencePriceEur(card.CardInfo, card.Finish, card.EstimatedValue);
            return basePrice.HasValue ? Math.Round(basePrice.Value * ConditionFactor(card.Condition), 2) : null;
        }
    }
}
