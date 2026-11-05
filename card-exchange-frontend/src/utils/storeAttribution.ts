// Negozio dal cui QR (/s/{slug}) arriva un visitatore: salvato per 30 giorni, così vale anche se
// la registrazione avviene più tardi o dopo altre pagine.
const KEY = 'cardexchange:store-slug';
const MAX_AGE_MS = 30 * 24 * 60 * 60 * 1000;
const FORMAT = /^[a-z0-9-]{1,80}$/;

export function saveStoreSlug(slug: string) {
  const value = slug.trim().toLowerCase();
  if (!FORMAT.test(value)) return;
  try {
    localStorage.setItem(KEY, JSON.stringify({ slug: value, savedAt: Date.now() }));
  } catch {
    // storage non disponibile: l'attribuzione vale solo per questa pagina
  }
}

export function getStoredStoreSlug(): string | null {
  try {
    const raw = localStorage.getItem(KEY);
    if (!raw) return null;
    const { slug, savedAt } = JSON.parse(raw) as { slug: string; savedAt: number };
    if (Date.now() - savedAt > MAX_AGE_MS || !FORMAT.test(slug)) {
      localStorage.removeItem(KEY);
      return null;
    }
    return slug;
  } catch {
    return null;
  }
}

export function clearStoredStoreSlug() {
  try {
    localStorage.removeItem(KEY);
  } catch {
    // ignorato
  }
}
