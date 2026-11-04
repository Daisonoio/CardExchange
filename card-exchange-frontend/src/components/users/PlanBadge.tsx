import { Crown, Store } from 'lucide-react';
import type { PlanBadge as Badge } from '../../types';

/** Badge del piano accanto al nome utente. Indica un abbonamento attivo, non una verifica d'identità. */
export default function PlanBadge({ badge, className = '' }: { badge?: Badge | null; className?: string }) {
  if (!badge) return null;
  const store = badge === 'Store';
  return (
    <span
      title={store ? 'Negozio con piano attivo' : 'Utente Premium'}
      className={`inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded-full text-[10px] font-semibold align-middle ${
        store ? 'bg-primary/10 text-primary' : 'bg-secondary/20 text-amber-800'
      } ${className}`}
    >
      {store ? <Store size={10} /> : <Crown size={10} />}
      {store ? 'Negozio' : 'Premium'}
    </span>
  );
}
