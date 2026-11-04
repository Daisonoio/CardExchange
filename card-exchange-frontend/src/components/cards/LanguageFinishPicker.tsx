import { useTranslation } from 'react-i18next';
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
  const { t } = useTranslation('cards');
  return (
    <div className="space-y-3">
      <div>
        <label htmlFor="card-language" className="text-xs font-semibold text-text-secondary block mb-1.5">
          {anyOption ? t('picker.languageOptional') : t('picker.language')}
        </label>
        <select
          id="card-language"
          value={language}
          onChange={(e) => onLanguageChange(e.target.value)}
          className="w-full px-3 py-2.5 rounded-xl border border-border bg-white text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
        >
          <option value="">{anyOption ? t('picker.anyLanguage') : t('picker.notSpecified')}</option>
          {CARD_LANGUAGES.map((l) => (
            <option key={l.code} value={l.code}>{l.label}</option>
          ))}
        </select>
        {!anyOption && !language && (
          <p className="text-[10px] text-text-muted mt-1">
            {t('picker.languageHint')}
          </p>
        )}
      </div>

      <div>
        <span className="text-xs font-semibold text-text-secondary block mb-1.5">
          {anyOption ? t('picker.finishOptional') : t('picker.finish')}
        </span>
        <div className="grid grid-cols-2 gap-1.5">
          {anyOption && (
            <button type="button" onClick={() => onFinishChange(undefined)} className={chip(finish === undefined)}>
              {t('picker.any')}
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
