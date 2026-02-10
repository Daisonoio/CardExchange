import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import Input from '../components/ui/Input';
import Button from '../components/ui/Button';
import { Layers } from 'lucide-react';

export default function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState({ emailOrUsername: '', password: '' });
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);
    try {
      await login(form);
      navigate('/explore');
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg || 'Credenziali non valide');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex flex-col items-center justify-center px-4 bg-gradient-to-b from-primary/5 to-surface-dark">
      <div className="w-full max-w-sm">
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-primary/10 mb-4">
            <Layers size={32} className="text-primary" />
          </div>
          <h1 className="text-2xl font-bold text-text">CardExchange</h1>
          <p className="text-sm text-text-secondary mt-1">Accedi al tuo account</p>
        </div>

        <form onSubmit={handleSubmit} className="bg-white rounded-2xl p-6 shadow-sm border border-border/50 space-y-4">
          {error && (
            <div className="px-4 py-3 rounded-xl bg-red-50 text-danger text-sm">
              {error}
            </div>
          )}

          <Input
            label="Email o Username"
            value={form.emailOrUsername}
            onChange={(e) => setForm({ ...form, emailOrUsername: e.target.value })}
            placeholder="mario@email.com"
            autoComplete="username"
            required
          />

          <Input
            label="Password"
            type="password"
            value={form.password}
            onChange={(e) => setForm({ ...form, password: e.target.value })}
            placeholder="La tua password"
            autoComplete="current-password"
            required
          />

          <Button type="submit" isLoading={isLoading} className="w-full">
            Accedi
          </Button>
        </form>

        <p className="text-center text-sm text-text-secondary mt-6">
          Non hai un account?{' '}
          <Link to="/register" className="text-primary font-semibold hover:underline">
            Registrati
          </Link>
        </p>
      </div>
    </div>
  );
}
