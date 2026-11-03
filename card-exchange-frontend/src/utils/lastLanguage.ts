// Ricorda l'ultima lingua scelta inserendo carte (comodità per singolo utente/browser).
const KEY = 'cardexchange:lastCardLanguage';

export function readLastLanguage(): string {
  try {
    return localStorage.getItem(KEY) ?? '';
  } catch {
    return '';
  }
}

export function saveLastLanguage(code: string) {
  try {
    if (code) localStorage.setItem(KEY, code);
  } catch {
    // storage non disponibile (es. navigazione privata): si ignora
  }
}
