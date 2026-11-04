import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Loader2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { auth } from '../api';
import { useAuth } from '../context/AuthContext';
import AuthShell from '../components/layout/AuthShell';
import { apiErrorMessage } from '../utils/apiError';

export default function VerifyEmailPage() {
  const { t } = useTranslation('auth');
  const [params] = useSearchParams();
  const token = params.get('token') ?? '';
  const { isAuthenticated, refreshUser } = useAuth();
  const [status, setStatus] = useState<'loading' | 'ok' | 'error'>(token ? 'loading' : 'error');
  const [message, setMessage] = useState(token ? '' : t('verify.missingToken'));
  const started = useRef(false);

  useEffect(() => {
    // Il token è monouso: evita la doppia chiamata di React StrictMode
    if (!token || started.current) return;
    started.current = true;
    auth.verifyEmail(token)
      .then(({ data }) => {
        setStatus('ok');
        setMessage(data.message);
        if (isAuthenticated) refreshUser();
      })
      .catch((err) => {
        setStatus('error');
        setMessage(apiErrorMessage(err, t('verify.failed')));
      });
  }, [token, isAuthenticated, refreshUser, t]);

  return (
    <AuthShell
      subtitle={t('verify.subtitle')}
      footer={
        <Link to={isAuthenticated ? '/home' : '/login'} className="text-secondary font-semibold hover:underline">
          {isAuthenticated ? t('verify.goHome') : t('goToLogin')}
        </Link>
      }
    >
      {status === 'loading' && (
        <div className="flex items-center gap-2 text-sm text-text-secondary">
          <Loader2 size={16} className="animate-spin" /> {t('verify.verifying')}
        </div>
      )}
      {status === 'ok' && <div className="px-4 py-3 rounded-xl bg-green-50 text-green-700 text-sm">{message}</div>}
      {status === 'error' && (
        <div className="space-y-2">
          <div className="px-4 py-3 rounded-xl bg-red-50 text-danger text-sm">{message}</div>
          {isAuthenticated && (
            <p className="text-xs text-text-secondary">{t('verify.resendHint')}</p>
          )}
        </div>
      )}
    </AuthShell>
  );
}
