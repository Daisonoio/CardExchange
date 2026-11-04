import { Languages } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { SUPPORTED_LANGUAGES } from '../../i18n';

/** Scelta della lingua dell'app (salvata nel browser). */
export default function LanguageSwitcher() {
  const { t, i18n } = useTranslation();
  const current = (i18n.resolvedLanguage ?? 'it').split('-')[0];
  return (
    <div className="bg-white rounded-2xl shadow-sm border border-border/50 px-4 py-3 mb-4 flex items-center gap-3">
      <span className="w-9 h-9 rounded-full border-2 border-border flex items-center justify-center shrink-0 text-primary">
        <Languages size={16} />
      </span>
      <span className="flex-1 text-sm font-medium text-text">{t('language.title')}</span>
      <div className="flex rounded-lg bg-surface-dark p-0.5 text-xs" role="group" aria-label={t('language.description')}>
        {SUPPORTED_LANGUAGES.map((l) => (
          <button
            key={l.code}
            onClick={() => i18n.changeLanguage(l.code)}
            aria-pressed={current === l.code}
            lang={l.code}
            className={`px-2.5 py-1 rounded-md font-medium ${current === l.code ? 'bg-white shadow-sm text-text' : 'text-text-secondary'}`}
          >
            {l.label}
          </button>
        ))}
      </div>
    </div>
  );
}
