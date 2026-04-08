import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { Compass, Library, Heart, Sparkles, ArrowLeftRight, UserCircle, LogOut, Layers } from 'lucide-react';

const NAV_ITEMS = [
  { to: '/explore', icon: Compass, label: 'Esplora' },
  { to: '/collection', icon: Library, label: 'Collezione' },
  { to: '/wishlist', icon: Heart, label: 'Ricerca' },
  { to: '/matchmaking', icon: Sparkles, label: 'Match' },
  { to: '/trades', icon: ArrowLeftRight, label: 'Scambi' },
  { to: '/profile', icon: UserCircle, label: 'Profilo' },
];

export default function TopBar() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <header className="hidden md:block sticky top-0 z-50 bg-white/80 backdrop-blur-lg border-b border-border">
      <div className="max-w-7xl mx-auto px-4 flex items-center justify-between h-16">
        <NavLink to="/" className="flex items-center gap-2 font-bold text-xl text-primary">
          <Layers size={26} />
          <span>CardExchange</span>
        </NavLink>

        <nav className="flex items-center gap-1">
          {NAV_ITEMS.map(({ to, icon: Icon, label }) => (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) =>
                `flex items-center gap-2 px-3 py-2 rounded-lg text-sm font-medium transition-colors ${
                  isActive
                    ? 'bg-primary/10 text-primary'
                    : 'text-text-secondary hover:bg-surface-dark hover:text-text'
                }`
              }
            >
              <Icon size={18} />
              <span>{label}</span>
            </NavLink>
          ))}
        </nav>

        <div className="flex items-center gap-3">
          {user && (
            <span className="text-sm text-text-secondary">
              {user.username}
            </span>
          )}
          <button
            onClick={handleLogout}
            className="p-2 rounded-lg text-text-muted hover:text-danger hover:bg-red-50 transition-colors"
            title="Esci"
          >
            <LogOut size={18} />
          </button>
        </div>
      </div>
    </header>
  );
}
