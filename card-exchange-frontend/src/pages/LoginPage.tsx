import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import Input from '../components/ui/Input';
import Button from '../components/ui/Button';
import { Layers } from 'lucide-react';

export default function LoginPage() {
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
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg || 'Credenziali non valide');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex flex-col items-center justify-center px-4" style={{ background: 'linear-gradient(160deg, #1a3461 0%, #2a4d8f 60%, #eef2f7 100%)' }}>
      <div className="w-full max-w-sm">
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl mb-4" style={{ background: '#f5b800' }}>
            <Layers size={32} style={{ color: '#1a3461' }} />
          </div>
          <h1 className="text-2xl font-bold text-white">CardExchange</h1>
          <p className="text-sm text-white/70 mt-1">Accedi al tuo account</p>
        </div>

        <form onSubmit={handleSubmit} className="bg-white rounded-2xl p-6 shadow-xl space-y-4">
          {error && (
            <div className="px-4 py-3 rounded-xl bg-red-50 text-danger text-sm">
              {error}
            </div>
          )}

          <Input
            label="Email o Username"
            value={form.usernameOrEmail}
            onChange={(e) => setForm({ ...form, usernameOrEmail: e.target.value })}
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

        <p className="text-center text-sm text-white/70 mt-6">
          Non hai un account?{' '}
          <Link to="/register" className="text-secondary font-semibold hover:underline">
            Registrati
          </Link>
        </p>
      </div>
    </div>
  );
}
