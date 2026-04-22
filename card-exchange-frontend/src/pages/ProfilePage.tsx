import { useAuth } from '../context/AuthContext';
import {
  Star, ArrowLeftRight, MessageCircle, MapPin, LogOut,
  ChevronRight, Pencil, Library, Heart, Trophy,
} from 'lucide-react';
import { useNavigate } from 'react-router-dom';

export default function ProfilePage() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  if (!user) return null;

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const initials = `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`.toUpperCase();

  const menuItems = [
    { label: 'Modifica Profilo', icon: Pencil, to: '/profile/edit' },
    { label: 'Le mie Carte', icon: Library, to: '/collection' },
    { label: 'Wishlist', icon: Heart, to: '/wishlist' },
    { label: 'I miei Scambi', icon: ArrowLeftRight, to: '/trades' },
    { label: 'Recensioni ricevute', icon: Trophy, to: null },
  ];

  return (
    <div className="max-w-lg mx-auto">
      {/* User identity */}
      <div className="flex items-center gap-4 mb-6 px-1">
        <div
          className="w-16 h-16 rounded-full flex items-center justify-center shrink-0 text-xl font-bold overflow-hidden"
          style={{ background: '#1a3461', color: '#f5b800' }}
        >
          {user.avatarUrl ? (
            <img src={user.avatarUrl} alt={user.username} className="w-full h-full object-cover" />
          ) : (
            <span>{initials}</span>
          )}
        </div>
        <div className="min-w-0">
          <h2 className="text-xl font-bold text-text">{user.firstName} {user.lastName}</h2>
          <p className="text-sm text-text-secondary">@{user.username}</p>
          {user.location && (
            <p className="text-xs text-text-muted flex items-center gap-1 mt-0.5">
              <MapPin size={11} />
              {user.location.city}, {user.location.country}
            </p>
          )}
        </div>
      </div>

      {/* Stats row */}
      <div className="grid grid-cols-3 gap-3 mb-6">
        {[
          { icon: Star, value: (user.reputationScore ?? 0).toFixed(1), label: 'Reputazione', color: '#f5b800' },
          { icon: ArrowLeftRight, value: user.totalTradesCompleted ?? 0, label: 'Scambi', color: '#1a3461' },
          { icon: MessageCircle, value: user.totalReviewsReceived ?? 0, label: 'Recensioni', color: '#10b981' },
        ].map(({ icon: Icon, value, label, color }) => (
          <div key={label} className="bg-white rounded-2xl p-3 text-center shadow-sm border border-border/50">
            <div className="flex items-center justify-center gap-1 mb-0.5" style={{ color }}>
              <Icon size={14} />
              <span className="text-lg font-bold text-text">{value}</span>
            </div>
            <p className="text-[11px] text-text-muted">{label}</p>
          </div>
        ))}
      </div>

      {/* Menu list */}
      <div className="bg-white rounded-2xl shadow-sm border border-border/50 overflow-hidden mb-4">
        {menuItems.map(({ label, icon: Icon, to }, idx) => (
          <button
            key={label}
            onClick={() => to && navigate(to)}
            disabled={!to}
            className={`w-full flex items-center gap-3 px-4 py-4 transition-colors text-left ${
              idx < menuItems.length - 1 ? 'border-b border-border/50' : ''
            } ${to ? 'hover:bg-surface-dark' : 'cursor-default opacity-50'}`}
          >
            <span
              className="w-9 h-9 rounded-full border-2 border-border flex items-center justify-center shrink-0"
              style={{ color: '#1a3461' }}
            >
              <Icon size={16} />
            </span>
            <span className="flex-1 text-sm font-medium text-text">{label}</span>
            {to && <ChevronRight size={16} className="text-text-muted" />}
          </button>
        ))}
      </div>

      {/* Logout */}
      <div className="bg-white rounded-2xl shadow-sm border border-border/50 overflow-hidden mb-6">
        <button
          onClick={handleLogout}
          className="w-full flex items-center gap-3 px-4 py-4 hover:bg-red-50 transition-colors text-left"
        >
          <span className="w-9 h-9 rounded-full border-2 border-danger/40 flex items-center justify-center shrink-0 text-danger">
            <LogOut size={16} />
          </span>
          <span className="flex-1 text-sm font-medium text-danger">Esci dall'account</span>
          <ChevronRight size={16} className="text-danger/40" />
        </button>
      </div>
    </div>
  );
}
