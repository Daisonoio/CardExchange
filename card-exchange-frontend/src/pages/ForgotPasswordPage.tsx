import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { auth } from '../api';
import Input from '../components/ui/Input';
import Button from '../components/ui/Button';
import AuthShell from '../components/layout/AuthShell';
import { apiErrorMessage } from '../utils/apiError';

export default function ForgotPasswordPage() {
  const { t } = useTranslation('auth');
  const [email, setEmail] = useState('');
  const [sentMessage, setSentMessage] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);
    try {
      const { data } = await auth.forgotPassword(email.trim());
      setSentMessage(data.message);
    } catch (err) {
      setError(apiErrorMessage(err, t('forgot.failed')));
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <AuthShell
      subtitle={t('forgot.subtitle')}
      footer={<Link to="/login" className="text-secondary font-semibold hover:underline">{t('backToLogin')}</Link>}
    >
      {sentMessage ? (
        <div className="px-4 py-3 rounded-xl bg-green-50 text-green-700 text-sm">{sentMessage}</div>
      ) : (
        <form onSubmit={handleSubmit} className="space-y-4">
          <p className="text-sm text-text-secondary">
            {t('forgot.intro')}
          </p>
          {error && <div className="px-4 py-3 rounded-xl bg-red-50 text-danger text-sm">{error}</div>}
          <Input
            label={t('email')}
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder={t('emailPlaceholder')}
            autoComplete="email"
            required
          />
          <Button type="submit" isLoading={isLoading} className="w-full">
            {t('forgot.submit')}
          </Button>
        </form>
      )}
    </AuthShell>
  );
}
