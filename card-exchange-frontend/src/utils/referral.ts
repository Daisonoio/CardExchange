// Codice invito (?ref=CODICE): salvato al primo caricamento di qualunque pagina, così sopravvive
// al passaggio per login o ad altre navigazioni prima della registrazione. Valido 30 giorni.
const KEY = 'cardexchange:referral';
const MAX_AGE_MS = 30 * 24 * 60 * 60 * 1000;
const FORMAT = /^[A-HJ-NP-Z2-9]{8}$/;

export function captureReferralFromUrl() {
  try {
    const code = new URLSearchParams(window.location.search).get('ref')?.trim().toUpperCase();
    if (code && FORMAT.test(code)) localStorage.setItem(KEY, JSON.stringify({ code, savedAt: Date.now() }));
  } catch {
    // storage non disponibile: l'invito vale solo per il link aperto
  }
}

export function getStoredReferral(): string | null {
  try {
    const raw = localStorage.getItem(KEY);
    if (!raw) return null;
    const { code, savedAt } = JSON.parse(raw) as { code: string; savedAt: number };
    if (Date.now() - savedAt > MAX_AGE_MS || !FORMAT.test(code)) {
      localStorage.removeItem(KEY);
      return null;
    }
    return code;
  } catch {
    return null;
  }
}

export function clearStoredReferral() {
  try {
    localStorage.removeItem(KEY);
  } catch {
    // ignorato
  }
}

/** Condivisione nativa (telefono) con ripiego sulla copia negli appunti. Restituisce come è andata. */
export async function shareLink(url: string, title: string, text: string): Promise<'shared' | 'copied' | 'failed'> {
  try {
    if (navigator.share) {
      await navigator.share({ title, text, url });
      return 'shared';
    }
    await navigator.clipboard.writeText(url);
    return 'copied';
  } catch (err) {
    if ((err as DOMException)?.name === 'AbortError') return 'failed';
    try {
      await navigator.clipboard.writeText(url);
      return 'copied';
    } catch {
      return 'failed';
    }
  }
}
