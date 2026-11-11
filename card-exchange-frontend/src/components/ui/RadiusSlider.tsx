import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Lock } from 'lucide-react';
import { RADIUS_MAX, RADIUS_MIN, RADIUS_STEP } from '../../hooks/useSearchRadius';
import { useEntitlements } from '../../hooks/useEntitlements';

interface RadiusSliderProps {
  id: string;
  value: number;
  onChange: (km: number) => void;
}

/**
 * Cursore della distanza, uguale in tutte le sezioni: a destra il valore scelto.
 * Con i piani a pagamento accesi, oltre il raggio del piano la parte del cursore è bloccata con l'invito al Premium;
 * con i piani spenti il cursore va sempre a fine corsa.
 */
export default function RadiusSlider({ id, value, onChange }: RadiusSliderProps) {
  const { t } = useTranslation('premium');
  const { limit, paidPlans } = useEntitlements();
  const planMax = limit('RadiusKm');
  // Con i piani spenti il cursore arriva sempre a fine corsa: il limite lo applica il server, senza mostrarlo
  const max = paidPlans && typeof planMax === 'number' ? Math.min(RADIUS_MAX, planMax) : RADIUS_MAX;
  const shown = Math.min(value, max);
  const lockedPct = ((RADIUS_MAX - max) / (RADIUS_MAX - RADIUS_MIN)) * 100;
  return (
    <div>
      <div className="flex items-center gap-3">
        <div className="relative flex-1 flex items-center">
          <input
            id={id}
            type="range"
            min={RADIUS_MIN}
            max={RADIUS_MAX}
            step={RADIUS_STEP}
            value={shown}
            onChange={(e) => onChange(Math.min(max, Number(e.target.value)))}
            aria-valuetext={`${shown} km`}
            className="w-full accent-primary"
          />
          {lockedPct > 0 && (
            <span aria-hidden className="pointer-events-none absolute right-0 top-1/2 -translate-y-1/2 h-2 rounded-r-full bg-[repeating-linear-gradient(45deg,#e5e7eb_0_4px,#f3f4f6_4px_8px)]"
              style={{ width: `${lockedPct}%` }} />
          )}
        </div>
        <span className="text-xs font-bold text-primary w-14 text-right tabular-nums" aria-hidden="true">{shown} km</span>
      </div>
      {max < RADIUS_MAX && (
        <Link to="/premium" className="mt-1 inline-flex items-center gap-1 text-[11px] font-semibold text-amber-800">
          <Lock size={11} /> {t('radiusLocked', { km: max, max: RADIUS_MAX })}
        </Link>
      )}
    </div>
  );
}
