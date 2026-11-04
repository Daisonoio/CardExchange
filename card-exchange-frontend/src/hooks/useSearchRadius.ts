import { useCallback, useSyncExternalStore } from 'react';
import { useAuth } from '../context/AuthContext';

/** Limiti del raggio di ricerca, uguali in tutto il sito (profilo, Esplora, Matchmaking). */
export const RADIUS_MIN = 5;
export const RADIUS_MAX = 200;
export const RADIUS_STEP = 5;
const DEFAULT_RADIUS = 50;

const storageKey = (userId: number) => `search-radius:${userId}`;
const listeners = new Set<() => void>();
const memory = new Map<number, number>();

export const clampRadius = (km: number) =>
  Math.min(RADIUS_MAX, Math.max(RADIUS_MIN, Math.round(km / RADIUS_STEP) * RADIUS_STEP));

function read(userId: number): number | null {
  if (memory.has(userId)) return memory.get(userId)!;
  try {
    const raw = localStorage.getItem(storageKey(userId));
    const n = raw == null ? NaN : Number(raw);
    if (Number.isFinite(n)) {
      memory.set(userId, clampRadius(n));
      return memory.get(userId)!;
    }
  } catch { /* archiviazione non disponibile: vale solo in memoria */ }
  return null;
}

function subscribe(cb: () => void) {
  listeners.add(cb);
  // Allinea anche le altre schede aperte
  const onStorage = (e: StorageEvent) => {
    if (e.key?.startsWith('search-radius:')) {
      memory.clear();
      cb();
    }
  };
  window.addEventListener('storage', onStorage);
  return () => {
    listeners.delete(cb);
    window.removeEventListener('storage', onStorage);
  };
}

/**
 * Raggio di ricerca condiviso da tutte le sezioni: cambiandolo in una pagina
 * cambia anche nelle altre e resta salvato. Finché l'utente non lo modifica
 * vale quello impostato nel profilo.
 */
export function useSearchRadius(): [number, (km: number) => void] {
  const { user } = useAuth();
  const userId = user?.id ?? 0;
  const profileRadius = clampRadius(user?.location?.maxDistanceKm || DEFAULT_RADIUS);

  const stored = useSyncExternalStore(subscribe, () => read(userId), () => null);
  const radius = stored ?? profileRadius;

  const setRadius = useCallback((km: number) => {
    const value = clampRadius(km);
    memory.set(userId, value);
    try { localStorage.setItem(storageKey(userId), String(value)); } catch { /* solo in memoria */ }
    listeners.forEach((l) => l());
  }, [userId]);

  return [radius, setRadius];
}

/** Da chiamare quando l'utente salva un nuovo raggio nel profilo: diventa il valore usato ovunque. */
export function resetSearchRadius(userId: number) {
  memory.delete(userId);
  try { localStorage.removeItem(storageKey(userId)); } catch { /* niente da fare */ }
  listeners.forEach((l) => l());
}
