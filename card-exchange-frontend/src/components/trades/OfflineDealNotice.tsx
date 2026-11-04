import { Info } from 'lucide-react';

/** Promemoria: CardExchange mette in contatto, ma accordi e pagamenti avvengono direttamente tra gli utenti. */
export default function OfflineDealNotice({ className = '' }: { className?: string }) {
  return (
    <p className={`flex items-start gap-2 text-[11px] text-text-secondary bg-surface-dark rounded-xl p-2.5 ${className}`}>
      <Info size={14} className="shrink-0 mt-px text-primary" />
      <span>
        CardExchange non gestisce pagamenti e non conclude vendite: prezzi e condizioni li concordate voi, e gli eventuali
        pagamenti avvengono direttamente tra di voi, fuori dall'app.
      </span>
    </p>
  );
}
