import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import Input from '../components/ui/Input';
import Button from '../components/ui/Button';
import { Layers } from 'lucide-react';

export default function RegisterPage() {
  const { register } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState({
    email: '',
    username: '',
    firstName: '',
    lastName: '',
    password: '',
    confirmPassword: '',
  });
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (form.password !== form.confirmPassword) {
      setError('Le password non coincidono');
      return;
    }

    if (form.password.length < 8) {
      setError('La password deve avere almeno 8 caratteri');
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
      });
      navigate('/explore');
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg || 'Errore durante la registrazione');
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
          <div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-primary/10 mb-4">
            <Layers size={32} className="text-primary" />
          </div>
          <h1 className="text-2xl font-bold text-text">Crea Account</h1>
          <p className="text-sm text-text-secondary mt-1">Unisciti alla community</p>
        </div>

        <form onSubmit={handleSubmit} className="bg-white rounded-2xl p-6 shadow-sm border border-border/50 space-y-3">
          {error && (
            <div className="px-4 py-3 rounded-xl bg-red-50 text-danger text-sm">{error}</div>
          )}

          <div className="grid grid-cols-2 gap-3">
            <Input label="Nome" value={form.firstName} onChange={set('firstName')} required />
            <Input label="Cognome" value={form.lastName} onChange={set('lastName')} required />
          </div>

          <Input
            label="Username"
            value={form.username}
            onChange={set('username')}
            placeholder="il_tuo_nick"
            autoComplete="username"
            required
          />

          <Input
            label="Email"
            type="email"
            value={form.email}
            onChange={set('email')}
            placeholder="mario@email.com"
            autoComplete="email"
            required
          />

          <Input
            label="Password"
            type="password"
            value={form.password}
            onChange={set('password')}
            placeholder="Almeno 8 caratteri"
            autoComplete="new-password"
            required
          />

          <Input
            label="Conferma Password"
            type="password"
            value={form.confirmPassword}
            onChange={set('confirmPassword')}
            autoComplete="new-password"
            required
          />

          <Button type="submit" isLoading={isLoading} className="w-full">
            Registrati
          </Button>
        </form>

        <p className="text-center text-sm text-text-secondary mt-6">
          Hai già un account?{' '}
          <Link to="/login" className="text-primary font-semibold hover:underline">
            Accedi
          </Link>
        </p>
      </div>
    </div>
  );
}
