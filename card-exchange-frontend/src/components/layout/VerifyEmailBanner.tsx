import { useEffect, useState } from 'react';
import { MailWarning, X } from 'lucide-react';
import { Trans, useTranslation } from 'react-i18next';
import { auth } from '../../api';
import { useAuth } from '../../context/AuthContext';
import { apiErrorMessage } from '../../utils/apiError';

/** Dopo la chiusura l'avviso torna dopo qualche giorno, così la conferma non viene dimenticata. */
const SNOOZE_MS = 3 * 24 * 60 * 60 * 1000;
const snoozeKey = (userId: number) => `verify-email-snoozed:${userId}`;

function isSnoozed(userId: number): boolean {
  try {
    const until = Number(localStorage.getItem(snoozeKey(userId)));
    return Number.isFinite(until) && until > Date.now();
  } catch {
    return false;
  }
}

/**
 * Avviso flottante e chiudibile per chi non ha ancora confermato l'email: non occupa spazio nella pagina.
 * In basso sopra la barra di navigazione su mobile, in basso a destra su desktop.
 */
export default function VerifyEmailBanner() {
  const { t } = useTranslation('layout');
  const { user } = useAuth();
  const [state, setState] = useState<'idle' | 'sending' | 'sent' | 'error'>('idle');
  const [message, setMessage] = useState('');
  const [dismissed, setDismissed] = useState(() => (user ? isSnoozed(user.id) : false));
  const [visible, setVisible] = useState(false);

  const show = !!user && user.emailConfirmed === false && !dismissed;

  // Compare poco dopo il caricamento della pagina, con una breve animazione
  useEffect(() => {
    if (!show) return;
    const id = setTimeout(() => setVisible(true), 600);
    return () => clearTimeout(id);
  }, [show]);

  const close = () => {
    setVisible(false);
    if (user) {
      try { localStorage.setItem(snoozeKey(user.id), String(Date.now() + SNOOZE_MS)); } catch { /* resta chiuso solo in questa sessione */ }
    }
    setTimeout(() => setDismissed(true), 250);
  };

  // Dopo il reinvio riuscito l'avviso si chiude da solo
  useEffect(() => {
    if (state !== 'sent') return;
    const id = setTimeout(close, 4000);
    return () => clearTimeout(id);
  }, [state]); // eslint-disable-line react-hooks/exhaustive-deps

  if (!show || !user) return null;

  const resend = async () => {
    setState('sending');
    try {
      const { data } = await auth.resendVerification();
      setState('sent');
      setMessage(data.message);
    } catch (err) {
      setState('error');
      setMessage(apiErrorMessage(err, t('verifyEmail.sendFailed')));
    }
  };

  return (
    <div
      role="status"
      aria-live="polite"
      className={`fixed z-40 left-3 right-3 bottom-[calc(6.5rem+env(safe-area-inset-bottom))] md:left-auto md:right-6 md:bottom-6 md:w-96
        transition-all duration-300 motion-reduce:transition-none ${visible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-3 pointer-events-none'}`}
    >
      <div className="flex items-start gap-3 rounded-2xl border border-amber-200 bg-amber-50 p-3 pr-2 text-sm text-amber-900 shadow-lg shadow-amber-900/10">
        <MailWarning size={18} className="mt-0.5 shrink-0" />
        <div className="flex-1 min-w-0">
          <p className="font-semibold">{t('verifyEmail.title')}</p>
          <p className="text-xs mt-0.5 break-words">
            <Trans
              t={t}
              i18nKey="verifyEmail.message"
              values={{ email: user.email }}
              components={{ email: <span className="font-semibold" /> }}
            />
          </p>
          {state === 'sent' || state === 'error' ? (
            <p className="mt-1.5 text-xs font-medium">{message}</p>
          ) : (
            <button
              onClick={resend}
              disabled={state === 'sending'}
              className="mt-1.5 text-xs font-semibold underline underline-offset-2 disabled:opacity-60"
            >
              {state === 'sending' ? t('verifyEmail.sending') : t('verifyEmail.resend')}
            </button>
          )}
        </div>
        <button
          onClick={close}
          aria-label={t('verifyEmail.close')}
          className="w-9 h-9 -mt-1 rounded-full flex items-center justify-center shrink-0 hover:bg-amber-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-amber-700"
        >
          <X size={16} />
        </button>
      </div>
    </div>
  );
}
