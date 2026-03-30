import { useAuth } from '../context/AuthContext';
import { Star, ArrowLeftRight, MessageCircle, MapPin, LogOut, ChevronRight } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import Button from '../components/ui/Button';

export default function ProfilePage() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  if (!user) return null;

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const initials = `${user.firstName[0]}${user.lastName[0]}`.toUpperCase();

  return (
    <div className="max-w-lg mx-auto">
      {/* Profile card */}
      <div className="bg-white rounded-2xl p-6 shadow-sm border border-border/50 mb-4">
        <div className="flex items-center gap-4">
          <div className="w-16 h-16 rounded-full bg-gradient-to-br from-primary to-primary-dark flex items-center justify-center shrink-0">
            {user.avatarUrl ? (
              <img src={user.avatarUrl} alt={user.username} className="w-16 h-16 rounded-full object-cover" />
            ) : (
              <span className="text-xl font-bold text-white">{initials}</span>
            )}
          </div>
          <div className="min-w-0">
            <h2 className="text-lg font-bold truncate">{user.firstName} {user.lastName}</h2>
            <p className="text-sm text-text-secondary">@{user.username}</p>
            {user.location && (
              <p className="text-xs text-text-muted flex items-center gap-1 mt-1">
                <MapPin size={12} />
                {user.location.city}, {user.location.country}
              </p>
            )}
          </div>
        </div>

        {user.bio && (
          <p className="text-sm text-text-secondary mt-3">{user.bio}</p>
        )}

        {/* Stats */}
        <div className="grid grid-cols-3 gap-3 mt-5">
          <div className="bg-surface-dark rounded-xl p-3 text-center">
            <div className="flex items-center justify-center gap-1 text-secondary">
              <Star size={14} />
              <span className="text-lg font-bold">{(user.reputationScore ?? 0).toFixed(1)}</span>
            </div>
            <p className="text-xs text-text-muted mt-0.5">Reputazione</p>
          </div>
          <div className="bg-surface-dark rounded-xl p-3 text-center">
            <div className="flex items-center justify-center gap-1 text-primary">
              <ArrowLeftRight size={14} />
              <span className="text-lg font-bold">{user.totalTradesCompleted ?? 0}</span>
            </div>
            <p className="text-xs text-text-muted mt-0.5">Scambi</p>
          </div>
          <div className="bg-surface-dark rounded-xl p-3 text-center">
            <div className="flex items-center justify-center gap-1 text-accent">
              <MessageCircle size={14} />
              <span className="text-lg font-bold">{user.totalReviewsReceived ?? 0}</span>
            </div>
            <p className="text-xs text-text-muted mt-0.5">Recensioni</p>
          </div>
        </div>
      </div>

      {/* Menu sections */}
      <div className="bg-white rounded-2xl overflow-hidden shadow-sm border border-border/50 mb-4">
        {[
          { label: 'Le mie Carte', to: '/collection', icon: '🃏' },
          { label: 'Le mie Ricerche', to: '/wishlist', icon: '❤️' },
          { label: 'I miei Scambi', to: '/trades', icon: '🔄' },
          { label: 'I miei Eventi', to: '/events', icon: '📅' },
        ].map((item) => (
          <button
            key={item.to}
            onClick={() => navigate(item.to)}
            className="w-full flex items-center justify-between px-4 py-3.5 hover:bg-surface-dark transition-colors border-b border-border/50 last:border-0"
          >
            <span className="flex items-center gap-3 text-sm font-medium">
              <span className="text-base">{item.icon}</span>
              {item.label}
            </span>
            <ChevronRight size={16} className="text-text-muted" />
          </button>
        ))}
      </div>

      {/* Logout */}
      <Button onClick={handleLogout} variant="outline" className="w-full" size="lg">
        <LogOut size={16} />
        Esci dall'account
      </Button>
    </div>
  );
}
