/** Estrae un messaggio leggibile da un errore Axios del backend (message o primo errore di validazione). */
export function apiErrorMessage(err: unknown, fallback: string): string {
  const data = (err as { response?: { data?: { message?: string; errors?: Record<string, string[]> } } })?.response?.data;
  const firstValidationError = data?.errors ? Object.values(data.errors).flat()[0] : undefined;
  return data?.message || firstValidationError || fallback;
}
