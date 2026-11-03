import { FINISH_LABELS, finishValue, languageLabel, languageShort } from '../../types';
import type { CardFinish } from '../../types';

/** Etichette compatte di lingua e finitura (la finitura "Normale" non viene mostrata). */
export default function CardVariantBadges({ language, finish, className = '' }: {
  language?: string | null;
  finish?: CardFinish | string | null;
  className?: string;
}) {
  const short = languageShort(language);
  const f = finishValue(finish);
  if (!short && (!f || f === 1)) return null;
  return (
    <span className={`inline-flex items-center gap-1 ${className}`}>
      {short && (
        <span title={languageLabel(language)} className="px-1 py-px rounded bg-gray-100 text-[9px] font-bold text-text-secondary">
          {short}
        </span>
      )}
      {f && f !== 1 && (
        <span className="px-1 py-px rounded bg-gradient-to-r from-amber-100 to-fuchsia-100 text-[9px] font-bold text-fuchsia-700">
          {FINISH_LABELS[f]}
        </span>
      )}
    </span>
  );
}
