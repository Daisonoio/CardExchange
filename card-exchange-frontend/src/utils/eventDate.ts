/** Data breve di un evento, es. "ven 14 nov, 20:30". */
export const formatEventDate = (iso: string) =>
  new Date(iso).toLocaleString('it-IT', { weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
