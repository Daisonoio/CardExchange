import { Star } from 'lucide-react';
import { useTranslation } from 'react-i18next';

/** Stelle da 1 a 5; se onChange è presente diventano selezionabili. */
export default function StarRating({ value, onChange, size = 16 }: { value: number; onChange?: (v: number) => void; size?: number }) {
  const { t } = useTranslation('users');
  return (
    <div className="inline-flex items-center gap-0.5" role={onChange ? 'radiogroup' : 'img'} aria-label={t('stars.valueOutOfFive', { value })}>
      {[1, 2, 3, 4, 5].map((n) => {
        const icon = <Star size={size} className={n <= Math.round(value) ? 'fill-amber-400 text-amber-400' : 'text-gray-300'} />;
        return onChange ? (
          <button key={n} type="button" role="radio" aria-checked={n === value} aria-label={t('stars.option', { count: n })} onClick={() => onChange(n)} className="p-0.5">
            {icon}
          </button>
        ) : (
          <span key={n}>{icon}</span>
        );
      })}
    </div>
  );
}
