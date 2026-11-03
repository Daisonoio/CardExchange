import { Scale, Sparkles } from 'lucide-react';
import type { TradeEvaluation } from '../../types';

const eur = (v: number) => `€${v.toFixed(2)}`;

/** Indicatore di equilibrio dello scambio, con pulsante per la proposta automatica. */
export default function TradeBalanceMeter({ evaluation, loading, onSuggest, suggesting, suggestMessage }: {
  evaluation: TradeEvaluation | null;
  loading: boolean;
  onSuggest?: () => void;
  suggesting?: boolean;
  suggestMessage?: string | null;
}) {
  const offered = evaluation?.offeredTotal ?? 0;
  const requested = evaluation?.requestedTotal ?? 0;
  const max = Math.max(offered, requested, 0.01);
  const diff = Math.abs(evaluation?.difference ?? 0);

  const verdict = !evaluation ? null
    : evaluation.verdict === 'fair' ? { text: 'Scambio equilibrato', cls: 'text-green-700 bg-green-50 border-green-200' }
    : evaluation.verdict === 'favorsReceiver' ? { text: `Offri ${eur(diff)} in più di quanto ricevi`, cls: 'text-amber-800 bg-amber-50 border-amber-200' }
    : { text: `Chiedi ${eur(diff)} in più di quanto offri`, cls: 'text-amber-800 bg-amber-50 border-amber-200' };

  return (
    <section className="mb-5 rounded-2xl border border-border bg-white p-3 space-y-2.5" aria-live="polite">
      <div className="flex items-center gap-2">
        <Scale size={16} className="text-primary" />
        <h2 className="text-sm font-semibold text-text flex-1">Equilibrio dello scambio</h2>
        {loading && <span className="text-[10px] text-text-muted">aggiornamento…</span>}
      </div>

      <div className="space-y-1.5 text-xs">
        {[
          { label: 'Ricevi', value: requested, color: 'bg-primary' },
          { label: 'Offri', value: offered, color: 'bg-amber-500' },
        ].map((row) => (
          <div key={row.label} className="flex items-center gap-2">
            <span className="w-12 text-text-secondary">{row.label}</span>
            <div className="flex-1 h-2.5 rounded-full bg-surface-dark overflow-hidden">
              <div className={`h-full rounded-full ${row.color} transition-all`} style={{ width: `${(row.value / max) * 100}%` }} />
            </div>
            <span className="w-16 text-right font-semibold tabular-nums">{eur(row.value)}</span>
          </div>
        ))}
      </div>

      {verdict && (
        <p className={`text-xs font-medium px-3 py-2 rounded-xl border ${verdict.cls}`}>{verdict.text}</p>
      )}
      {evaluation && evaluation.missingPriceCount > 0 && (
        <p className="text-[10px] text-text-muted">
          {evaluation.missingPriceCount} {evaluation.missingPriceCount === 1 ? 'carta non ha' : 'carte non hanno'} un prezzo di mercato ed {evaluation.missingPriceCount === 1 ? 'è esclusa' : 'sono escluse'} dal calcolo.
        </p>
      )}
      <p className="text-[10px] text-text-muted">
        Prezzi di mercato (foil se la copia è foil) corretti per la condizione. Equo entro il 10% o 1 €.
      </p>

      {onSuggest && (
        <button
          type="button"
          onClick={onSuggest}
          disabled={suggesting}
          className="w-full flex items-center justify-center gap-1.5 py-2 rounded-xl text-xs font-semibold border border-primary text-primary hover:bg-primary/5 disabled:opacity-60"
        >
          <Sparkles size={14} />
          {suggesting ? 'Calcolo in corso…' : 'Proposta automatica bilanciata'}
        </button>
      )}
      {suggestMessage && <p className="text-xs text-text-secondary">{suggestMessage}</p>}
    </section>
  );
}
