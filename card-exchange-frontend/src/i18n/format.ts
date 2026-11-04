import { useTranslation } from 'react-i18next';
import { currentLocale } from './index';

/** Formattazione nella lingua corrente. Nei componenti usare useFormat(), che si aggiorna al cambio lingua. */
export const formatEuro = (v: number, opts?: Intl.NumberFormatOptions) =>
  v.toLocaleString(currentLocale(), { style: 'currency', currency: 'EUR', ...opts });
export const formatNumber = (v: number, opts?: Intl.NumberFormatOptions) => v.toLocaleString(currentLocale(), opts);
export const formatDate = (d: string | number | Date, opts: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'long', year: 'numeric' }) =>
  new Date(d).toLocaleDateString(currentLocale(), opts);
export const formatDateTime = (d: string | number | Date, opts: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }) =>
  new Date(d).toLocaleString(currentLocale(), opts);
export const formatTime = (d: string | number | Date, opts: Intl.DateTimeFormatOptions = { hour: '2-digit', minute: '2-digit' }) =>
  new Date(d).toLocaleTimeString(currentLocale(), opts);

export function useFormat() {
  useTranslation(); // ri-render al cambio lingua
  return { euro: formatEuro, number: formatNumber, date: formatDate, dateTime: formatDateTime, time: formatTime, locale: currentLocale() };
}
