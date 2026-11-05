import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { referrals, stores } from '../api';
import { clearStoredReferral, getStoredReferral } from '../utils/referral';
import { clearStoredStoreSlug, getStoredStoreSlug } from '../utils/storeAttribution';
import { MINIMUM_AGE, ageFromIsoDate, todayIso } from '../utils/age';
import { USERNAME_RULE, passwordProblem } from '../utils/accountRules';
import { apiErrorMessage } from '../utils/apiError';
import { Trans, useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext';
import Input from '../components/ui/Input';
import Button from '../components/ui/Button';
import BrandLogo from '../components/brand/BrandLogo';
import LegalLinks from '../components/layout/LegalLinks';

export default function RegisterPage() {
  const { t } = useTranslation('auth');
  const { register } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState({
    email: '',
    username: '',
    firstName: '',
    lastName: '',
    password: '',
    confirmPassword: '',
    birthDate: '',
  });
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  // Invito: il codice è stato salvato all'apertura del link (?ref=)
  const [referralCode] = useState(() => getStoredReferral());
  // Arrivo dal QR di un negozio (/s/:slug): ci si iscrive "da" quel negozio e lo si segue
  const [storeSlug] = useState(() => getStoredStoreSlug());
  const [acceptTerms, setAcceptTerms] = useState(false);
  const { t: tLegal } = useTranslation('legal');
  const [fromStore, setFromStore] = useState<string | null>(null);
  useEffect(() => {
    if (!storeSlug) return;
    stores.bySlug(storeSlug).then(({ data }) => setFromStore(data.name)).catch(() => clearStoredStoreSlug());
  }, [storeSlug]);
  const [invitedBy, setInvitedBy] = useState<string | null>(null);

  useEffect(() => {
    if (!referralCode) return;
    referrals.lookup(referralCode).then(({ data }) => setInvitedBy(data.username)).catch(() => clearStoredReferral());
  }, [referralCode]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (form.password !== form.confirmPassword) {
      setError(t('passwordMismatch'));
      return;
    }

    if (!USERNAME_RULE.test(form.username)) {
      setError(t('register.usernameRule'));
      return;
    }

    const problem = passwordProblem(form.password);
    if (problem) {
      setError(t(`register.passwordProblem.${problem}`));
      return;
    }

    if (!form.birthDate) {
      setError(t('register.birthDateRequired'));
      return;
    }
    if (ageFromIsoDate(form.birthDate) < MINIMUM_AGE) {
      setError(t('register.underage', { age: MINIMUM_AGE }));
      return;
    }

    setIsLoading(true);
    try {
      await register({
        email: form.email,
        username: form.username,
        firstName: form.firstName,
        lastName: form.lastName,
        password: form.password,
        confirmPassword: form.confirmPassword,
        birthDate: form.birthDate,
        referralCode: invitedBy && referralCode ? referralCode : undefined,
        storeSlug: fromStore && storeSlug ? storeSlug : undefined,
        acceptTerms,
      });
      clearStoredReferral();
      clearStoredStoreSlug();
      navigate('/home');
    } catch (err: unknown) {
      // Il server spiega il motivo (email già usata, password non valida…): si mostra quello
      setError(apiErrorMessage(err, t('register.failed')));
    } finally {
      setIsLoading(false);
    }
  };

  const set = (field: string) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm({ ...form, [field]: e.target.value });

  return (
    <div className="min-h-screen flex flex-col items-center justify-center px-4 py-8 bg-gradient-to-b from-primary/5 to-surface-dark">
      <div className="w-full max-w-sm">
        <div className="text-center mb-6">
          <div className="flex justify-center mb-4">
            <BrandLogo layout="horizontal" theme="light" height={44} />
          </div>
          <h1 className="text-2xl font-bold text-text">{t('register.title')}</h1>
          <p className="text-sm text-text-secondary mt-1">{t('register.subtitle')}</p>
          {fromStore && (
            <p className="mt-3 inline-block px-3 py-1.5 rounded-full bg-primary/10 text-sm text-text">
              <Trans t={t} i18nKey="register.fromStore" values={{ store: fromStore }} components={{ strong: <strong /> }} />
            </p>
          )}
          {invitedBy && (
            <p className="mt-3 inline-block px-3 py-1.5 rounded-full bg-secondary/20 text-sm text-text">
              <Trans t={t} i18nKey="register.invitedBy" values={{ username: invitedBy }} components={{ strong: <strong /> }} />
            </p>
          )}
        </div>

        <form onSubmit={handleSubmit} className="bg-white rounded-2xl p-6 shadow-sm border border-border/50 space-y-3">
          {error && (
            <div className="px-4 py-3 rounded-xl bg-red-50 text-danger text-sm">{error}</div>
          )}

          <div className="grid grid-cols-2 gap-3">
            <Input label={t('register.firstName')} value={form.firstName} onChange={set('firstName')} required />
            <Input label={t('register.lastName')} value={form.lastName} onChange={set('lastName')} required />
          </div>

          <div>
            <Input
              label={t('register.birthDate')}
              type="date"
              value={form.birthDate}
              onChange={set('birthDate')}
              max={todayIso()}
              min="1900-01-01"
              autoComplete="bday"
              required
            />
            <p className="text-[11px] text-text-muted mt-1">{t('register.birthDateHint', { age: MINIMUM_AGE })}</p>
          </div>

          <Input
            label={t('register.username')}
            value={form.username}
            onChange={set('username')}
            placeholder={t('register.usernamePlaceholder')}
            autoComplete="username"
            required
          />
          <p className="text-[11px] text-text-muted -mt-2">{t('register.usernameHint')}</p>

          <Input
            label={t('email')}
            type="email"
            value={form.email}
            onChange={set('email')}
            placeholder={t('emailPlaceholder')}
            autoComplete="email"
            required
          />

          <Input
            label={t('password')}
            type="password"
            value={form.password}
            onChange={set('password')}
            placeholder={t('register.passwordPlaceholder')}
            autoComplete="new-password"
            required
          />
          <p className="text-[11px] text-text-muted -mt-2">{t('register.passwordHint')}</p>

          <Input
            label={t('register.confirmPassword')}
            type="password"
            value={form.confirmPassword}
            onChange={set('confirmPassword')}
            autoComplete="new-password"
            required
          />

          <label htmlFor="accept-terms" className="flex items-start gap-2.5 text-sm text-text-secondary">
            <input
              id="accept-terms"
              type="checkbox"
              checked={acceptTerms}
              onChange={(e) => setAcceptTerms(e.target.checked)}
              required
              className="mt-0.5 w-4 h-4 accent-primary shrink-0"
            />
            <span>
              <Trans
                t={tLegal}
                i18nKey="register.accept"
                components={{
                  terms: <Link to="/terms" target="_blank" className="text-primary font-semibold hover:underline" />,
                  privacy: <Link to="/privacy" target="_blank" className="text-primary font-semibold hover:underline" />,
                }}
              />
            </span>
          </label>

          <Button type="submit" isLoading={isLoading} className="w-full" disabled={!acceptTerms}>
            {t('register.submit')}
          </Button>
        </form>

        <p className="text-center text-sm text-text-secondary mt-6">
          {t('register.haveAccount')}{' '}
          <Link to="/login" className="text-primary font-semibold hover:underline">
            {t('register.login')}
          </Link>
        </p>
        <LegalLinks />
      </div>
    </div>
  );
}
