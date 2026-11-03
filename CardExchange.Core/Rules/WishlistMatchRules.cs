using CardExchange.Core.Entities;

namespace CardExchange.Core.Rules
{
    /// <summary>
    /// Regole uniche per decidere se una copia fisica soddisfa una voce di wishlist.
    /// Usate da matchmaking e match della wishlist: modificarle solo qui.
    /// </summary>
    public static class WishlistMatchRules
    {
        /// <summary>
        /// Una carta soddisfa la wishlist se:
        /// - la condizione è uguale o migliore di quella richiesta (Mint=1 ... Damaged=8);
        /// - la lingua coincide, quando richiesta (una carta senza lingua indicata non soddisfa una lingua specifica);
        /// - la finitura coincide, quando richiesta;
        /// - il valore stimato non supera il prezzo massimo, quando entrambi sono noti.
        /// La corrispondenza della carta (CardInfoId) è responsabilità del chiamante.
        /// </summary>
        public static bool Satisfies(Card card, WishlistItem wish)
        {
            if (wish.PreferredCondition.HasValue && card.Condition > wish.PreferredCondition.Value)
                return false;
            if (wish.PreferredLanguage != null && card.Language != wish.PreferredLanguage)
                return false;
            if (wish.PreferredFinish.HasValue && card.Finish != wish.PreferredFinish.Value)
                return false;
            if (wish.MaxPrice.HasValue && card.EstimatedValue.HasValue && card.EstimatedValue.Value > wish.MaxPrice.Value)
                return false;
            return true;
        }
    }
}
