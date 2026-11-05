import { account } from '../api';

const KEY = 'ce.timeZoneSynced';

/** Comunica al server il fuso orario del dispositivo (una volta per sessione e utente), per gli orari nelle notifiche. */
export function syncTimeZone(userId: number) {
  let tz: string | undefined;
  try {
    tz = Intl.DateTimeFormat().resolvedOptions().timeZone;
  } catch {
    return;
  }
  if (!tz) return;
  const marker = `${userId}:${tz}`;
  try {
    if (sessionStorage.getItem(KEY) === marker) return;
  } catch { /* storage non disponibile: si invia comunque */ }
  account.setTimeZone(tz).then(() => {
    try { sessionStorage.setItem(KEY, marker); } catch { /* ignorato */ }
  }).catch(() => { /* si riprova alla prossima apertura */ });
}
