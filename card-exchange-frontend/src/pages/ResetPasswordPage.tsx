import { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { auth } from '../api';
import Input from '../components/ui/Input';
import Button from '../components/ui/Button';
import AuthShell from '../components/layout/AuthShell';
import { apiErrorMessage } from '../utils/apiError';

const PASSWORD_RULE = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$/;

export default function ResetPasswordPage() {
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
      setError('La password deve avere almeno 8 caratteri, con una maiuscola, una minuscola, un numero e un carattere speciale (@$!%*?&).');
      return;
    }
    if (form.newPassword !== form.confirmNewPassword) {
      setError('Le password non coincidono');
      return;
    }
    setIsLoading(true);
    try {
      const { data } = await auth.resetPassword({ token, ...form });
      setDone(data.message);
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossibile reimpostare la password. Riprova.'));
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <AuthShell
      subtitle="Imposta una nuova password"
      footer={<Link to="/login" className="text-secondary font-semibold hover:underline">Torna al login</Link>}
    >
      {!token ? (
        <div className="space-y-3 text-sm">
          <div className="px-4 py-3 rounded-xl bg-red-50 text-danger">Link non valido: manca il codice di reset.</div>
          <Link to="/forgot-password" className="text-primary font-semibold hover:underline">Richiedi un nuovo link</Link>
        </div>
      ) : done ? (
        <div className="space-y-4">
          <div className="px-4 py-3 rounded-xl bg-green-50 text-green-700 text-sm">{done}</div>
          <Button className="w-full" onClick={() => navigate('/login')}>Vai al login</Button>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="space-y-4">
          {error && <div className="px-4 py-3 rounded-xl bg-red-50 text-danger text-sm">{error}</div>}
          <Input
            label="Nuova password"
            type="password"
            value={form.newPassword}
            onChange={(e) => setForm({ ...form, newPassword: e.target.value })}
            autoComplete="new-password"
            required
          />
          <Input
            label="Conferma nuova password"
            type="password"
            value={form.confirmNewPassword}
            onChange={(e) => setForm({ ...form, confirmNewPassword: e.target.value })}
            autoComplete="new-password"
            required
          />
          <Button type="submit" isLoading={isLoading} className="w-full">
            Salva nuova password
          </Button>
          {error.includes('scaduto') && (
            <Link to="/forgot-password" className="block text-center text-sm text-primary font-semibold hover:underline">
              Richiedi un nuovo link
            </Link>
          )}
        </form>
      )}
    </AuthShell>
  );
}
