import { RADIUS_MAX, RADIUS_MIN, RADIUS_STEP } from '../../hooks/useSearchRadius';

interface RadiusSliderProps {
  id: string;
  value: number;
  onChange: (km: number) => void;
}

/** Cursore della distanza, uguale in tutte le sezioni: a destra il valore scelto. */
export default function RadiusSlider({ id, value, onChange }: RadiusSliderProps) {
  return (
    <div className="flex items-center gap-3">
      <input
        id={id}
        type="range"
        min={RADIUS_MIN}
        max={RADIUS_MAX}
        step={RADIUS_STEP}
        value={value}
        onChange={(e) => onChange(Number(e.target.value))}
        aria-valuetext={`${value} km`}
        className="flex-1 accent-primary"
      />
      <span className="text-xs font-bold text-primary w-14 text-right tabular-nums" aria-hidden="true">{value} km</span>
    </div>
  );
}
