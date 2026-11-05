import { account } from '../api';
import i18n from '../i18n';

const KEY = 'ce.localeSynced';

/**
 * Comunica al server fuso orario e lingua del dispositivo (una volta per sessione, utente e combinazione),
 * così notifiche ed email arrivano nell'ora e nella lingua giuste.
 */
export function syncLocale(userId: number) {
  let tz: string | undefined;
  try {
    tz = Intl.DateTimeFormat().resolvedOptions().timeZone;
  } catch {
    tz = undefined;
  }
  const language = (i18n.resolvedLanguage ?? i18n.language ?? 'it').split('-')[0];
  const marker = `${userId}:${tz ?? ''}:${language}`;
  try {
    if (sessionStorage.getItem(KEY) === marker) return;
  } catch { /* storage non disponibile: si invia comunque */ }
  account.setLocale({ timeZone: tz, language }).then(() => {
    try { sessionStorage.setItem(KEY, marker); } catch { /* ignorato */ }
  }).catch(() => { /* si riprova alla prossima apertura */ });
}
