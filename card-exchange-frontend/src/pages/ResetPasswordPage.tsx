import { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { auth } from '../api';
import Input from '../components/ui/Input';
import Button from '../components/ui/Button';
import AuthShell from '../components/layout/AuthShell';
import { apiErrorMessage } from '../utils/apiError';

const PASSWORD_RULE = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$/;

export default function ResetPasswordPage() {
  const { t } = useTranslation('auth');
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const token = params.get('token') ?? '';
  const [form, setForm] = useState({ newPassword: '', confirmNewPassword: '' });
  const [error, setError] = useState('');
  const [done, setDone] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    if (!PASSWORD_RULE.test(form.newPassword)) {
      setError(t('reset.passwordRule'));
      return;
    }
    if (form.newPassword !== form.confirmNewPassword) {
      setError(t('passwordMismatch'));
      return;
    }
    setIsLoading(true);
    try {
      const { data } = await auth.resetPassword({ token, ...form });
      setDone(data.message);
    } catch (err) {
      setError(apiErrorMessage(err, t('reset.failed')));
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <AuthShell
      subtitle={t('reset.subtitle')}
      footer={<Link to="/login" className="text-secondary font-semibold hover:underline">{t('backToLogin')}</Link>}
    >
      {!token ? (
        <div className="space-y-3 text-sm">
          <div className="px-4 py-3 rounded-xl bg-red-50 text-danger">{t('reset.missingToken')}</div>
          <Link to="/forgot-password" className="text-primary font-semibold hover:underline">{t('reset.requestNewLink')}</Link>
        </div>
      ) : done ? (
        <div className="space-y-4">
          <div className="px-4 py-3 rounded-xl bg-green-50 text-green-700 text-sm">{done}</div>
          <Button className="w-full" onClick={() => navigate('/login')}>{t('goToLogin')}</Button>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="space-y-4">
          {error && <div className="px-4 py-3 rounded-xl bg-red-50 text-danger text-sm">{error}</div>}
          <Input
            label={t('reset.newPassword')}
            type="password"
            value={form.newPassword}
            onChange={(e) => setForm({ ...form, newPassword: e.target.value })}
            autoComplete="new-password"
            required
          />
          <Input
            label={t('reset.confirmNewPassword')}
            type="password"
            value={form.confirmNewPassword}
            onChange={(e) => setForm({ ...form, confirmNewPassword: e.target.value })}
            autoComplete="new-password"
            required
          />
          <Button type="submit" isLoading={isLoading} className="w-full">
            {t('reset.submit')}
          </Button>
          {/scaduto|expired/i.test(error) && (
            <Link to="/forgot-password" className="block text-center text-sm text-primary font-semibold hover:underline">
              {t('reset.requestNewLink')}
            </Link>
          )}
        </form>
      )}
    </AuthShell>
  );
}
