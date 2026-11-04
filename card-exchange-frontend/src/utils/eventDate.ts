import { formatDateTime } from '../i18n/format';

/** Data breve di un evento, es. "ven 14 nov, 20:30" (nella lingua corrente). */
export const formatEventDate = (iso: string) =>
  formatDateTime(iso, { weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
