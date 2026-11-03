import { CARD_LANGUAGES, FINISH_LABELS } from '../../types';
import type { CardFinish } from '../../types';

interface Props {
  /** Codice lingua; '' = non specificata (carta) o qualsiasi (wishlist). */
  language: string;
  onLanguageChange: (code: string) => void;
  /** undefined = qualsiasi (solo con anyOption). */
  finish: CardFinish | undefined;
  onFinishChange: (finish: CardFinish | undefined) => void;
  /** Modalità wishlist: mostra "Qualsiasi" per lingua e finitura. */
  anyOption?: boolean;
}

const chip = (active: boolean) =>
  `px-3 py-2 rounded-xl text-xs font-medium border transition-colors ${
    active ? 'border-primary bg-primary/10 text-primary' : 'border-border text-text-secondary hover:bg-surface-dark'
  }`;

/** Selettore di lingua e finitura condiviso tra collezione e wishlist. */
export default function LanguageFinishPicker({ language, onLanguageChange, finish, onFinishChange, anyOption }: Props) {
  return (
    <div className="space-y-3">
      <div>
        <label htmlFor="card-language" className="text-xs font-semibold text-text-secondary block mb-1.5">
          Lingua{anyOption ? ' (opzionale)' : ''}
        </label>
        <select
          id="card-language"
          value={language}
          onChange={(e) => onLanguageChange(e.target.value)}
          className="w-full px-3 py-2.5 rounded-xl border border-border bg-white text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
        >
          <option value="">{anyOption ? 'Qualsiasi lingua' : 'Non specificata'}</option>
          {CARD_LANGUAGES.map((l) => (
            <option key={l.code} value={l.code}>{l.label}</option>
          ))}
        </select>
        {!anyOption && !language && (
          <p className="text-[10px] text-text-muted mt-1">
            Indica la lingua: chi cerca una lingua precisa troverà solo le carte con lingua specificata.
          </p>
        )}
      </div>

      <div>
        <span className="text-xs font-semibold text-text-secondary block mb-1.5">
          Finitura{anyOption ? ' (opzionale)' : ''}
        </span>
        <div className="grid grid-cols-2 gap-1.5">
          {anyOption && (
            <button type="button" onClick={() => onFinishChange(undefined)} className={chip(finish === undefined)}>
              Qualsiasi
            </button>
          )}
          {([1, 2, 3, 4] as CardFinish[]).map((f) => (
            <button key={f} type="button" onClick={() => onFinishChange(f)} className={chip(finish === f)}>
              {FINISH_LABELS[f]}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}
