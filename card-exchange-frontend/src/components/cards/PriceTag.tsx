/** Prezzo di una copia: "prezzo indicato" dal proprietario oppure "valore di mercato" come riferimento. */
export default function PriceTag({ askingPrice, marketValue, compact = false }: {
  askingPrice?: number | null;
  marketValue?: number | null;
  compact?: boolean;
}) {
  const eur = (v: number) => `€${v.toFixed(2)}`;
  if (askingPrice != null) {
    return (
      <span className={`inline-flex items-baseline gap-1 flex-wrap ${compact ? 'text-[10px]' : 'text-xs'}`}>
        <span className="font-bold text-accent">{eur(askingPrice)}</span>
        <span className="text-text-muted">{compact ? 'indicato' : 'prezzo indicato'}</span>
        {marketValue != null && marketValue !== askingPrice && (
          <span className="text-text-muted">· mercato {eur(marketValue)}</span>
        )}
      </span>
    );
  }
  if (marketValue != null && marketValue > 0) {
    return (
      <span className={`inline-flex items-baseline gap-1 ${compact ? 'text-[10px]' : 'text-xs'}`}>
        <span className="font-bold text-accent">{eur(marketValue)}</span>
        <span className="text-text-muted">{compact ? 'mercato' : 'valore di mercato'}</span>
      </span>
    );
  }
  return null;
}
