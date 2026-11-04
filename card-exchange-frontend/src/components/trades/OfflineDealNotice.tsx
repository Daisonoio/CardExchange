import { Info } from 'lucide-react';
import { useTranslation } from 'react-i18next';

/** Promemoria: CardExchange mette in contatto, ma accordi e pagamenti avvengono direttamente tra gli utenti. */
export default function OfflineDealNotice({ className = '' }: { className?: string }) {
  const { t } = useTranslation('trades');
  return (
    <p className={`flex items-start gap-2 text-[11px] text-text-secondary bg-surface-dark rounded-xl p-2.5 ${className}`}>
      <Info size={14} className="shrink-0 mt-px text-primary" />
      <span>
        {t('offlineDealNotice.text')}
      </span>
    </p>
  );
}
