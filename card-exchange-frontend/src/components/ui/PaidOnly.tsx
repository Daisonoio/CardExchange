import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useEntitlements } from '../../hooks/useEntitlements';

/** Pagina dei piani a pagamento: con i piani spenti non esiste e si torna alla Home. */
export default function PaidOnly({ children }: { children: ReactNode }) {
  const { entitlements, paidPlans } = useEntitlements();
  if (!entitlements) return null;
  return paidPlans ? <>{children}</> : <Navigate to="/" replace />;
}
