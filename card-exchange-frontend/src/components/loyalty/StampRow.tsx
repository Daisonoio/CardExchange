import { Stamp } from 'lucide-react';

/** Riga di timbri: pieni quelli raccolti, vuoti quelli che mancano. */
export default function StampRow({ stamps, required }: { stamps: number; required: number }) {
  const shown = Math.min(stamps, required);
  return (
    <div className="flex flex-wrap gap-1.5" aria-hidden="true">
      {Array.from({ length: required }, (_, i) => (
        <span key={i} className={`w-7 h-7 rounded-full grid place-items-center border-2 ${i < shown ? 'bg-amber-500 border-amber-500 text-white' : 'border-dashed border-border text-transparent'}`}>
          <Stamp size={14} />
        </span>
      ))}
    </div>
  );
}

