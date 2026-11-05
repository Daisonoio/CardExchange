// Segnala al server gli errori del browser (monitoraggio). Senza dati personali: messaggio, stack, pagina.
const ENDPOINT = '/api/monitoring/client-errors';
const MAX_PER_SESSION = 20;
const sent = new Set<string>();

// Rumore noto dei browser, non errori dell'app
const IGNORED = [/ResizeObserver loop/i, /Script error\.?$/i, /Non-Error promise rejection captured/i];

export function reportClientError(message: string, stack?: string, kind = 'error') {
  try {
    const msg = String(message || 'Errore sconosciuto').slice(0, 500);
    if (IGNORED.some((r) => r.test(msg))) return;
    if (sent.has(msg) || sent.size >= MAX_PER_SESSION) return;
    sent.add(msg);
    const body = JSON.stringify({
      message: msg,
      stack: stack?.slice(0, 4000),
      // Solo il percorso, senza parametri (che potrebbero contenere dati)
      path: window.location.pathname.slice(0, 300),
      kind,
    });
    if (navigator.sendBeacon?.(ENDPOINT, new Blob([body], { type: 'application/json' }))) return;
    void fetch(ENDPOINT, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body, keepalive: true }).catch(() => undefined);
  } catch {
    // il monitoraggio non deve mai rompere l'app
  }
}

export function installErrorReporting() {
  window.addEventListener('error', (e) => reportClientError(e.message, e.error?.stack, 'error'));
  window.addEventListener('unhandledrejection', (e) => {
    const reason = e.reason as { message?: string; stack?: string } | string | undefined;
    // Le richieste API fallite sono già gestite e mostrate: non sono errori dell'app
    if (typeof reason === 'object' && reason && 'isAxiosError' in reason) return;
    reportClientError(typeof reason === 'string' ? reason : reason?.message ?? 'Promise rifiutata', typeof reason === 'object' ? reason?.stack : undefined, 'unhandledrejection');
  });
}
