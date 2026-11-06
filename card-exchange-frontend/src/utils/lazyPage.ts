import { lazy, type ComponentType } from 'react';

const RELOAD_KEY = 'ce.chunkReloadAt';

/**
 * Pagina caricata solo quando serve. Dopo un nuovo rilascio i vecchi file non esistono più:
 * se il caricamento fallisce si ricarica la pagina una volta (al massimo ogni 30 secondi) per prendere la versione nuova.
 */
// eslint-disable-next-line @typescript-eslint/no-explicit-any -- come la firma di React.lazy
export function lazyPage<T extends ComponentType<any>>(load: () => Promise<{ default: T }>) {
  return lazy(async () => {
    try {
      return await load();
    } catch (err) {
      let last = 0;
      try { last = Number(sessionStorage.getItem(RELOAD_KEY) ?? 0); } catch { /* ignorato */ }
      if (Date.now() - last > 30_000) {
        try { sessionStorage.setItem(RELOAD_KEY, String(Date.now())); } catch { /* ignorato */ }
        window.location.reload();
        return new Promise<{ default: T }>(() => {}); // la pagina si sta ricaricando
      }
      throw err;
    }
  });
}
