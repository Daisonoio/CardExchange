import { useEffect, useState } from 'react';
import { me } from '../api';
import type { Entitlements } from '../types';
import { useAuth } from '../context/AuthContext';

let cached: { userId: number; at: number; data: Entitlements } | null = null;
const listeners = new Set<(e: Entitlements | null) => void>();
const TTL = 60_000;

/** Ricarica i diritti (es. dopo un acquisto). */
export function refreshEntitlements() {
  cached = null;
  me.entitlements().then(({ data }) => listeners.forEach((l) => l(data))).catch(() => {});
}

/** Diritti effettivi dell'utente: piano, funzioni incluse, limiti (una sola richiesta condivisa, 1 minuto di cache). */
export function useEntitlements() {
  const { user } = useAuth();
  const [data, setData] = useState<Entitlements | null>(cached && user && cached.userId === user.id ? cached.data : null);

  useEffect(() => {
    if (!user) return;
    listeners.add(setData);
    if (!cached || cached.userId !== user.id || Date.now() - cached.at > TTL) {
      me.entitlements().then(({ data: e }) => {
        cached = { userId: user.id, at: Date.now(), data: e };
        listeners.forEach((l) => l(e));
      }).catch(() => {});
    }
    return () => { listeners.delete(setData); };
  }, [user]);

  return {
    entitlements: data,
    /** Piani a pagamento visibili (false finché non si accendono dal server). */
    paidPlans: !!data?.paidPlans,
    profileType: data?.profileType ?? 'Collector',
    has: (capability: string) => !!data?.capabilities.includes(capability),
    /** null = illimitato; undefined = non ancora caricato */
    limit: (name: string) => (data ? data.limits[name] : undefined),
  };
}
