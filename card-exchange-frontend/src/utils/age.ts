/** Età minima per usare CardExchange (consenso digitale in Italia). Il controllo vero è sul server. */
export const MINIMUM_AGE = 14;

/** Anni compiuti oggi per una data aaaa-mm-gg. */
export function ageFromIsoDate(iso: string, today = new Date()): number {
  const [y, m, d] = iso.split('-').map(Number);
  let age = today.getFullYear() - y;
  const beforeBirthday = today.getMonth() + 1 < m || (today.getMonth() + 1 === m && today.getDate() < d);
  if (beforeBirthday) age--;
  return age;
}

/** Data di oggi in formato aaaa-mm-gg (limite massimo per il campo data). */
export function todayIso(today = new Date()): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${today.getFullYear()}-${pad(today.getMonth() + 1)}-${pad(today.getDate())}`;
}
