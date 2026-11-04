import { useEffect, useState } from 'react';
import { Navigate, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Compass, Library, MapPin, Plus, UserCircle } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import './proto.css';

export type ThemeChoice = 'auto' | 'dark' | 'light';
const THEME_KEY = 'cx-theme';

function readTheme(): ThemeChoice {
  try {
    const v = localStorage.getItem(THEME_KEY);
    return v === 'dark' || v === 'light' ? v : 'auto';
  } catch {
    return 'auto';
  }
}

/** Contesto dell'anteprima: tema scelto e funzione per cambiarlo, passati alle pagine via Outlet. */
export interface ProtoContext {
  theme: ThemeChoice;
  setTheme: (t: ThemeChoice) => void;
}

/**
 * Contenitore dell'anteprima della nuova grafica (/preview): stesso login dell'app, tema proprio,
 * barra in basso con il "+" centrale per aggiungere una carta.
 */
export default function ProtoShell() {
  const { t } = useTranslation('proto');
  const { isAuthenticated, isLoading } = useAuth();
  const navigate = useNavigate();
  const [theme, setThemeState] = useState<ThemeChoice>(readTheme);

  const setTheme = (next: ThemeChoice) => {
    setThemeState(next);
    try { localStorage.setItem(THEME_KEY, next); } catch { /* preferenza non salvata: resta per la sessione */ }
  };

  // Colore della barra del browser coerente con il tema dell'anteprima
  useEffect(() => {
    const meta = document.querySelector('meta[name="theme-color"]');
    const previous = meta?.getAttribute('content');
    const dark = theme === 'dark' || (theme === 'auto' && !window.matchMedia('(prefers-color-scheme: light)').matches);
    meta?.setAttribute('content', dark ? '#0d0b1e' : '#f3f2fa');
    return () => { if (previous) meta?.setAttribute('content', previous); };
  }, [theme]);

  if (isLoading) return <div className="cx" />;
  if (!isAuthenticated) return <Navigate to="/login" replace />;

  const item = (to: string, Icon: typeof Compass, label: string, end = false) => (
    <NavLink to={to} end={end}
      className={({ isActive }) => `flex flex-col items-center gap-0.5 flex-1 py-2 text-[10px] font-semibold ${isActive ? 'cx-accent-text' : 'cx-muted'}`}>
      <Icon size={22} strokeWidth={2} />
      {label}
    </NavLink>
  );

  return (
    <div className="cx" data-theme={theme === 'auto' ? undefined : theme}>
      <div className="max-w-md mx-auto min-h-dvh pb-24">
        <Outlet context={{ theme, setTheme } satisfies ProtoContext} />
      </div>
      <nav className="fixed bottom-0 inset-x-0 z-[500]" aria-label={t('nav.label')}>
        <div className="max-w-md mx-auto px-3 pb-[max(env(safe-area-inset-bottom),10px)]">
          <div className="cx-glass rounded-[26px] flex items-center px-2" style={{ boxShadow: 'var(--cx-shadow)', border: '1px solid var(--cx-border)' }}>
            {item('/preview', Compass, t('nav.explore'), true)}
            {item('/preview/map', MapPin, t('nav.nearby'))}
            <button onClick={() => navigate('/collection?add=1')} aria-label={t('nav.add')}
              className="cx-btn w-14 h-14 -mt-7 flex items-center justify-center shrink-0 mx-1" style={{ boxShadow: '0 8px 24px rgba(200,245,60,0.35)' }}>
              <Plus size={28} strokeWidth={2.6} />
            </button>
            {item('/preview/collection', Library, t('nav.collection'))}
            {item('/profile', UserCircle, t('nav.profile'))}
          </div>
        </div>
      </nav>
    </div>
  );
}
