using CardExchange.Core.Entities;

namespace CardExchange.Core.Rules
{
    /// <summary>Una copia posseduta, ridotta ai dati che servono alla valutazione.</summary>
    public record HoldingLine(int CardInfoId, CardFinish Finish, CardCondition Condition, int Quantity);

    /// <summary>Prezzo di mercato di una carta in un giorno (storico giornaliero).</summary>
    public record PricePoint(int CardInfoId, DateTime Date, decimal? PriceEur, decimal? PriceEurFoil);

    public record ValuePoint(DateTime Date, decimal Value);

    /// <summary>
    /// Andamento nel tempo del valore di mercato delle carte possedute oggi. È un riferimento: non dice a quanto
    /// si scambieranno le carte, ma come si è mosso il loro valore.
    /// </summary>
    public static class CollectionValuation
    {
        /// <summary>
        /// Un punto per giorno da <paramref name="from"/> a <paramref name="to"/> (date UTC, estremi inclusi).
        /// Per ogni carta si usa l'ultimo prezzo noto fino a quel giorno; prima del primo prezzo noto si usa il primo,
        /// così l'ingresso di una carta nello storico non appare come un aumento di valore.
        /// Le carte senza alcun prezzo non contano.
        /// </summary>
        public static IReadOnlyList<ValuePoint> Series(IEnumerable<HoldingLine> holdings, IEnumerable<PricePoint> history,
            DateTime from, DateTime to)
        {
            from = from.Date;
            to = to.Date;
            if (to < from) return Array.Empty<ValuePoint>();

            var lines = holdings.Where(h => h.Quantity > 0).ToList();
            var byCard = history
                .GroupBy(p => p.CardInfoId)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Date).ToList());

            var result = new List<ValuePoint>();
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                decimal total = 0;
                foreach (var line in lines)
                {
                    if (!byCard.TryGetValue(line.CardInfoId, out var points) || points.Count == 0) continue;
                    var point = LastOnOrBefore(points, day) ?? points[0];
                    var unit = UnitPrice(point, line.Finish);
                    if (unit.HasValue)
                        total += Math.Round(unit.Value * CardPricing.ConditionFactor(line.Condition), 2) * line.Quantity;
                }
                result.Add(new ValuePoint(day, total));
            }
            return result;
        }

        /// <summary>Prezzo per la finitura, con la stessa regola di <see cref="CardPricing.ReferencePriceEur"/>.</summary>
        public static decimal? UnitPrice(PricePoint p, CardFinish finish) =>
            finish != CardFinish.Normal && p.PriceEurFoil.HasValue ? p.PriceEurFoil : p.PriceEur;

        private static PricePoint? LastOnOrBefore(List<PricePoint> sorted, DateTime day)
        {
            PricePoint? found = null;
            foreach (var p in sorted)
            {
                if (p.Date.Date > day) break;
                found = p;
            }
            return found;
        }
    }
}
