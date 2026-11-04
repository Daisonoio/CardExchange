import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { apiErrorMessage } from '../utils/apiError';
import { useAuth } from '../context/AuthContext';
import Input from '../components/ui/Input';
import Button from '../components/ui/Button';
import BrandLogo from '../components/brand/BrandLogo';

export default function LoginPage() {
  const { t } = useTranslation('auth');
  const { login } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState({ usernameOrEmail: '', password: '' });
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);
    try {
      await login(form);
      navigate('/home');
    } catch (err: unknown) {
      setError(apiErrorMessage(err, t('login.invalidCredentials')));
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex flex-col items-center justify-center px-4" style={{ background: 'linear-gradient(160deg, #1a3461 0%, #2a4d8f 60%, #eef2f7 100%)' }}>
      <div className="w-full max-w-sm">
        <div className="text-center mb-8">
          <h1 className="flex justify-center mb-2">
            <BrandLogo layout="stacked" theme="dark" height={150} />
          </h1>
          <p className="text-sm text-white/70 mt-1">{t('login.subtitle')}</p>
        </div>

        <form onSubmit={handleSubmit} className="bg-white rounded-2xl p-6 shadow-xl space-y-4">
          {error && (
            <div className="px-4 py-3 rounded-xl bg-red-50 text-danger text-sm">
              {error}
            </div>
          )}

          <Input
            label={t('login.usernameOrEmail')}
            value={form.usernameOrEmail}
            onChange={(e) => setForm({ ...form, usernameOrEmail: e.target.value })}
            placeholder={t('emailPlaceholder')}
            autoComplete="username"
            required
          />

          <Input
            label={t('password')}
            type="password"
            value={form.password}
            onChange={(e) => setForm({ ...form, password: e.target.value })}
            placeholder={t('login.passwordPlaceholder')}
            autoComplete="current-password"
            required
          />

          <div className="text-right -mt-2">
            <Link to="/forgot-password" className="text-xs text-primary font-medium hover:underline">
              {t('login.forgotPassword')}
            </Link>
          </div>

          <Button type="submit" isLoading={isLoading} className="w-full">
            {t('login.submit')}
          </Button>
        </form>

        <p className="text-center text-sm text-white/70 mt-6">
          {t('login.noAccount')}{' '}
          <Link to="/register" className="text-secondary font-semibold hover:underline">
            {t('login.register')}
          </Link>
        </p>
      </div>
    </div>
  );
}
