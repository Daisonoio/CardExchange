import { Scale, Sparkles } from 'lucide-react';
import type { TradeEvaluation } from '../../types';

const eur = (v: number) => `€${v.toFixed(2)}`;

/**
 * Valori di riferimento dello scambio: valore di mercato e prezzi indicati dai proprietari.
 * Solo informativo: gli utenti possono accordarsi liberamente, anche su scambi di valore diverso.
 */
export default function TradeBalanceMeter({ evaluation, loading, onSuggest, suggesting, suggestMessage }: {
  evaluation: TradeEvaluation | null;
  loading: boolean;
  onSuggest?: () => void;
  suggesting?: boolean;
  suggestMessage?: string | null;
}) {
  const rows = evaluation ? [
    { label: 'Valore di mercato', receive: evaluation.requestedTotal, give: evaluation.offeredTotal },
    { label: 'Prezzi indicati', receive: evaluation.requestedPriceTotal, give: evaluation.offeredPriceTotal },
  ] : [];
  const max = Math.max(0.01, ...rows.flatMap((r) => [r.receive, r.give]));
  const pricesDiffer = evaluation && (evaluation.requestedPriceTotal !== evaluation.requestedTotal || evaluation.offeredPriceTotal !== evaluation.offeredTotal);

  return (
    <section className="mb-5 rounded-2xl border border-border bg-white p-3 space-y-3" aria-live="polite">
      <div className="flex items-center gap-2">
        <Scale size={16} className="text-primary" />
        <h2 className="text-sm font-semibold text-text flex-1">Valori di riferimento</h2>
        {loading && <span className="text-[10px] text-text-muted">aggiornamento…</span>}
      </div>

      {rows.filter((_, i) => i === 0 || pricesDiffer).map((r) => (
        <div key={r.label} className="space-y-1 text-xs">
          <p className="font-medium text-text-secondary">{r.label}</p>
          {[
            { label: 'Ricevi', value: r.receive, color: 'bg-primary' },
            { label: 'Offri', value: r.give, color: 'bg-secondary' },
          ].map((row) => (
            <div key={row.label} className="flex items-center gap-2">
              <span className="w-12 text-text-muted">{row.label}</span>
              <div className="flex-1 h-2 rounded-full bg-surface-dark overflow-hidden">
                <div className={`h-full rounded-full ${row.color} transition-all`} style={{ width: `${(row.value / max) * 100}%` }} />
              </div>
              <span className="w-16 text-right font-semibold tabular-nums">{eur(row.value)}</span>
            </div>
          ))}
        </div>
      ))}

      {evaluation && evaluation.missingPriceCount > 0 && (
        <p className="text-[10px] text-text-muted">
          {evaluation.missingPriceCount} {evaluation.missingPriceCount === 1 ? 'carta non ha' : 'carte non hanno'} né prezzo indicato né valore di mercato.
        </p>
      )}
      <p className="text-[11px] text-text-muted">
        Il valore di mercato è un riferimento e i prezzi indicati dai proprietari sono trattabili: potete accordarvi liberamente,
        anche su scambi di valore diverso.
      </p>

      {onSuggest && (
        <button
          type="button"
          onClick={onSuggest}
          disabled={suggesting}
          className="w-full flex items-center justify-center gap-1.5 py-2 rounded-xl text-xs font-semibold border border-primary text-primary hover:bg-primary/5 disabled:opacity-60"
        >
          <Sparkles size={14} />
          {suggesting ? 'Calcolo in corso…' : 'Suggeriscimi cosa offrire'}
        </button>
      )}
      {suggestMessage && <p className="text-xs text-text-secondary">{suggestMessage}</p>}
    </section>
  );
}
