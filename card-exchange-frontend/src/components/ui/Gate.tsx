import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Lock } from 'lucide-react';
import { useEntitlements } from '../../hooks/useEntitlements';

/** Piano che include ciascuna funzione, per l'invito all'upgrade. */
const PLAN_FOR: Record<string, 'premium' | 'store' | 'storePro' | 'seller'> = {
  LocalDemand: 'storePro', Buylist: 'storePro', Reservations: 'storePro',
  FeaturedStore: 'store', SellerShowcase: 'seller',
};

/**
 * Mostra la funzione se il piano la include, altrimenti l'invito al piano giusto (o `fallback`).
 * Il server controlla comunque ogni richiesta: questo serve solo a non mostrare porte chiuse.
 */
export default function Gate({ capability, children, fallback }: { capability: string; children: ReactNode; fallback?: ReactNode }) {
  const { t } = useTranslation('premium');
  const { entitlements, has } = useEntitlements();
  if (!entitlements) return null;
  if (has(capability)) return <>{children}</>;
  if (fallback !== undefined) return <>{fallback}</>;
  const plan = PLAN_FOR[capability] ?? 'premium';
  return (
    <Link to={plan === 'store' || plan === 'storePro' ? '/premium?for=store' : '/premium'}
      className="flex items-center gap-2 p-3 rounded-xl bg-surface-dark text-sm text-text-secondary hover:text-text">
      <Lock size={16} className="shrink-0 text-primary" /> {t(`gate.${plan}`)}
    </Link>
  );
}

/** Etichetta "Pro"/"Premium" accanto a una funzione non inclusa nel piano. */
export function PlanTag({ capability }: { capability: string }) {
  const { t } = useTranslation('premium');
  const { entitlements, has } = useEntitlements();
  if (!entitlements || has(capability)) return null;
  const plan = PLAN_FOR[capability] ?? 'premium';
  return <span className="ml-1.5 align-middle text-[10px] font-semibold px-1.5 py-0.5 rounded bg-secondary/20 text-amber-800">{t(`tag.${plan}`)}</span>;
}
