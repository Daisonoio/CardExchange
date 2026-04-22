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
    <header className="hidden md:flex sticky top-0 z-50 items-center border-b border-border h-16" style={{ background: '#1a3461' }}>
      <div className="max-w-7xl mx-auto px-4 flex items-center justify-between w-full">
        <NavLink to="/home" className="flex items-center gap-2 font-bold text-xl text-white">
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
                    ? 'bg-white/15 text-white'
                    : 'text-white/60 hover:bg-white/10 hover:text-white'
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
            <span className="text-sm text-white/70">
              {user.username}
            </span>
          )}
          <button
            onClick={handleLogout}
            className="p-2 rounded-lg text-white/60 hover:text-white hover:bg-white/10 transition-colors"
            title="Esci"
          >
            <LogOut size={18} />
          </button>
        </div>
      </div>
    </header>
  );
}
