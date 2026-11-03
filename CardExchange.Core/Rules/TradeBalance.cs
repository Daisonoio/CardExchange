namespace CardExchange.Core.Rules
{
    public enum TradeVerdict
    {
        /// <summary>Differenza entro la tolleranza.</summary>
        Fair,
        /// <summary>Chi propone dà più di quanto riceve.</summary>
        FavorsReceiver,
        /// <summary>Chi propone riceve più di quanto dà.</summary>
        FavorsProposer
    }

    public record TradeBalanceResult(decimal OfferedTotal, decimal RequestedTotal, decimal Difference, decimal Tolerance, TradeVerdict Verdict);

    /// <summary>Una carta che il proponente può aggiungere all'offerta.</summary>
    public record OfferCandidate(int CardId, decimal UnitValue, int AvailableQuantity, int WishlistPriority);

    /// <summary>
    /// Valutazione dell'equilibrio di uno scambio e costruzione di una proposta bilanciata.
    /// </summary>
    public static class TradeBalance
    {
        /// <summary>Tolleranza: il 10% del lato più ricco, mai meno di 1 €.</summary>
        public static decimal ToleranceFor(decimal offeredTotal, decimal requestedTotal) =>
            Math.Max(1m, Math.Max(offeredTotal, requestedTotal) * 0.10m);

        public static TradeBalanceResult Evaluate(decimal offeredTotal, decimal requestedTotal)
        {
            var difference = offeredTotal - requestedTotal;
            var tolerance = ToleranceFor(offeredTotal, requestedTotal);
            var verdict = Math.Abs(difference) <= tolerance ? TradeVerdict.Fair
                : difference > 0 ? TradeVerdict.FavorsReceiver
                : TradeVerdict.FavorsProposer;
            return new TradeBalanceResult(offeredTotal, requestedTotal, difference, tolerance, verdict);
        }

        /// <summary>
        /// Sceglie le copie (con quantità) da offrire per avvicinarsi al valore <paramref name="target"/>.
        /// Criteri, in ordine: restare entro la tolleranza; preferire le carte che l'altro cerca con priorità alta;
        /// avvicinarsi il più possibile al target; usare meno carte.
        /// Con pochi pezzi prova tutte le combinazioni, altrimenti confronta più strategie greedy.
        /// </summary>
        public static IReadOnlyList<(int CardId, int Quantity)> SuggestOffer(IEnumerable<OfferCandidate> candidates, decimal target)
        {
            var units = candidates
                .Where(c => c.UnitValue > 0 && c.AvailableQuantity > 0)
                .SelectMany(c => Enumerable.Repeat(c, Math.Min(c.AvailableQuantity, 20)))
                .ToList();
            if (units.Count == 0 || target <= 0) return Array.Empty<(int, int)>();

            var tolerance = ToleranceFor(target, target);
            List<OfferCandidate> best;

            if (units.Count <= ExhaustiveLimit)
            {
                best = new List<OfferCandidate>();
                var bestScore = Score(best, target, tolerance);
                for (int mask = 1; mask < (1 << units.Count); mask++)
                {
                    var subset = new List<OfferCandidate>();
                    for (int k = 0; k < units.Count; k++)
                        if ((mask & (1 << k)) != 0) subset.Add(units[k]);
                    var score = Score(subset, target, tolerance);
                    if (score.CompareTo(bestScore) < 0) { best = subset; bestScore = score; }
                }
            }
            else
            {
                var strategies = new[]
                {
                    units.OrderBy(u => u.WishlistPriority).ThenByDescending(u => u.UnitValue).ToList(),
                    units.OrderByDescending(u => u.UnitValue).ToList(),
                    units.OrderBy(u => Math.Abs(target - u.UnitValue)).ToList(),
                };
                best = new List<OfferCandidate>();
                foreach (var ordered in strategies)
                {
                    var pick = Greedy(ordered, target, tolerance);
                    if (Score(pick, target, tolerance).CompareTo(Score(best, target, tolerance)) < 0) best = pick;
                }
            }

            // Una proposta vuota o più lontana dal target del "non offrire nulla" non è utile
            if (best.Count == 0 || Math.Abs(target - best.Sum(u => u.UnitValue)) >= target)
                return Array.Empty<(int, int)>();

            return best
                .GroupBy(c => c.CardId)
                .Select(g => (g.Key, g.Count()))
                .ToList();
        }

        private const int ExhaustiveLimit = 14;

        /// <summary>Punteggio da minimizzare (confronto lessicografico).</summary>
        private static (int OutsideTolerance, int PrioritySum, decimal Distance, int Count) Score(
            List<OfferCandidate> subset, decimal target, decimal tolerance)
        {
            var distance = Math.Abs(target - subset.Sum(u => u.UnitValue));
            var within = subset.Count > 0 && distance <= tolerance;
            // La priorità conta solo tra proposte già accettabili; fuori tolleranza vince la più vicina
            return (within ? 0 : 1, within ? subset.Sum(u => u.WishlistPriority) : 0, distance, subset.Count);
        }

        private static List<OfferCandidate> Greedy(List<OfferCandidate> ordered, decimal target, decimal tolerance)
        {
            var chosen = new List<OfferCandidate>();
            decimal total = 0;
            foreach (var unit in ordered)
            {
                if (total >= target - tolerance) break;
                if (total + unit.UnitValue <= target + tolerance)
                {
                    chosen.Add(unit);
                    total += unit.UnitValue;
                }
            }
            return chosen;
        }
    }
}
