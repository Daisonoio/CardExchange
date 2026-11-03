import { useState } from 'react';
import { MailWarning } from 'lucide-react';
import { auth } from '../../api';
import { useAuth } from '../../context/AuthContext';
import { apiErrorMessage } from '../../utils/apiError';

/** Avviso per gli utenti che non hanno ancora confermato l'indirizzo email. */
export default function VerifyEmailBanner() {
  const { user } = useAuth();
  const [state, setState] = useState<'idle' | 'sending' | 'sent' | 'error'>('idle');
  const [message, setMessage] = useState('');

  if (!user || user.emailConfirmed !== false) return null;

  const resend = async () => {
    setState('sending');
    try {
      const { data } = await auth.resendVerification();
      setState('sent');
      setMessage(data.message);
    } catch (err) {
      setState('error');
      setMessage(apiErrorMessage(err, 'Invio non riuscito, riprova più tardi.'));
    }
  };

  return (
    <div className="mb-4 flex items-start gap-3 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
      <MailWarning size={18} className="mt-0.5 shrink-0" />
      <div className="flex-1">
        <p>
          Conferma il tuo indirizzo email (<span className="font-semibold">{user.email}</span>) per proteggere l'account e ricevere gli avvisi.
        </p>
        {state === 'sent' || state === 'error' ? (
          <p className="mt-1 text-xs">{message}</p>
        ) : (
          <button
            onClick={resend}
            disabled={state === 'sending'}
            className="mt-1 text-xs font-semibold underline disabled:opacity-60"
          >
            {state === 'sending' ? 'Invio in corso…' : 'Invia di nuovo il link'}
          </button>
        )}
      </div>
    </div>
  );
}
