import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CalendarCheck } from 'lucide-react';
import { auth } from '../../api';
import { useAuth } from '../../context/AuthContext';
import { MINIMUM_AGE, ageFromIsoDate, todayIso } from '../../utils/age';
import Button from '../ui/Button';
import Input from '../ui/Input';

/**
 * Account creati prima del controllo dell'età: prima di continuare va indicata la data di nascita.
 * Sotto l'età minima il server sospende l'account e l'utente viene disconnesso.
 */
export default function AgeGate() {
  const { t } = useTranslation('auth');
  const { refreshUser, logout } = useAuth();
  const [birthDate, setBirthDate] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [blocked, setBlocked] = useState<string | null>(null);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!birthDate) return;
    // Il server sospende l'account sotto l'età minima: si avvisa prima, ma la decisione resta del server
    if (ageFromIsoDate(birthDate) < MINIMUM_AGE && !confirm(t('ageGate.underage', { age: MINIMUM_AGE }))) return;
    setBusy(true);
    setError(null);
    try {
      await auth.setBirthDate(birthDate);
      await refreshUser();
    } catch (err) {
      const data = (err as { response?: { status?: number; data?: { message?: string; code?: string } } })?.response;
      if (data?.status === 403 && data.data?.code === 'UNDERAGE') setBlocked(data.data.message ?? t('ageGate.underage', { age: MINIMUM_AGE }));
      else setError(data?.data?.message ?? t('ageGate.failed'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center px-4 bg-gradient-to-b from-primary/5 to-surface-dark">
      <div className="w-full max-w-sm bg-white rounded-2xl p-6 shadow-sm border border-border/50 space-y-4">
        <div className="w-12 h-12 rounded-2xl bg-primary/10 text-primary flex items-center justify-center">
          <CalendarCheck size={24} />
        </div>
        <h1 className="text-xl font-bold text-text">{t('ageGate.title')}</h1>
        {blocked ? (
          <>
            <p className="px-4 py-3 rounded-xl bg-red-50 text-danger text-sm">{blocked}</p>
            <Button className="w-full" onClick={logout}>{t('ageGate.logout')}</Button>
          </>
        ) : (
          <form onSubmit={submit} className="space-y-4">
            <p className="text-sm text-text-secondary">{t('ageGate.body', { age: MINIMUM_AGE })}</p>
            {error && <p className="px-4 py-3 rounded-xl bg-red-50 text-danger text-sm">{error}</p>}
            <Input label={t('register.birthDate')} type="date" value={birthDate} max={todayIso()} min="1900-01-01"
              onChange={(e) => setBirthDate(e.target.value)} autoComplete="bday" required />
            <Button type="submit" className="w-full" isLoading={busy} disabled={!birthDate}>{t('ageGate.submit')}</Button>
            <button type="button" onClick={logout} className="w-full text-sm text-text-secondary hover:underline">{t('ageGate.logout')}</button>
          </form>
        )}
      </div>
    </div>
  );
}
