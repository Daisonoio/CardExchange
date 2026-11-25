import i18n from '../i18n';

/**
 * Estrae un messaggio leggibile da un errore Axios del backend (message o primo errore di validazione).
 * Se il server non risponde o va in errore senza spiegazioni, lo dice: non si mostra il messaggio di ripiego
 * (es. "Credenziali non valide" quando in realtà il server è irraggiungibile).
 */
export function apiErrorMessage(err: unknown, fallback: string): string {
  const response = (err as { response?: { status?: number; data?: { message?: string; errors?: Record<string, string[]> } } })?.response;
  const data = response?.data;
  const firstValidationError = data?.errors ? Object.values(data.errors).flat()[0] : undefined;
  const message = data?.message || firstValidationError;
  if (message) return message;
  const isAxios = !!(err as { isAxiosError?: boolean })?.isAxiosError;
  if (isAxios && (!response || (response.status ?? 0) >= 500)) return i18n.t('common:errors.serverUnreachable');
  return fallback;
}
