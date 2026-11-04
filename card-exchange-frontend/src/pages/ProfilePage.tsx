import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext';
import {
  Star, ArrowLeftRight, MessageCircle, MapPin, LogOut,
  ChevronRight, Pencil, Library, Heart, Trophy, Ban, ShieldCheck, Crown, Receipt, BarChart3, Search,
} from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import ReviewsSheet from '../components/users/ReviewsSheet';
import BlockedUsersSheet from '../components/users/BlockedUsersSheet';
import { hasPermission, hasRole } from '../utils/permissions';
import PushToggle from '../components/notifications/PushToggle';
import LanguageSwitcher from '../components/ui/LanguageSwitcher';
import InviteCard from '../components/users/InviteCard';

export default function ProfilePage() {
  const { t } = useTranslation('profile');
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [showReviews, setShowReviews] = useState(false);
  const [showBlocked, setShowBlocked] = useState(false);

  if (!user) return null;

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const initials = `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`.toUpperCase();

  const menuItems = [
    { label: t('menu.editProfile'), icon: Pencil, to: '/profile/edit' },
    { label: t('menu.myPlan'), icon: Crown, to: '/premium' },
    { label: t('menu.stats'), icon: BarChart3, to: '/stats' },
    { label: t('menu.advancedSearch'), icon: Search, to: '/search' },
    { label: t('menu.myCards'), icon: Library, to: '/collection' },
    { label: t('menu.wishlist'), icon: Heart, to: '/wishlist' },
    { label: t('menu.myTrades'), icon: ArrowLeftRight, to: '/trades' },
    { label: t('menu.reviews'), icon: Trophy, to: null, action: () => setShowReviews(true) },
    { label: t('menu.blockedUsers'), icon: Ban, to: null, action: () => setShowBlocked(true) },
    ...(hasPermission('ADMIN.REPORTS.MANAGE') ? [{ label: t('menu.moderation'), icon: ShieldCheck, to: '/moderation' }] : []),
    ...(hasRole('Admin', 'SuperAdmin') ? [{ label: t('menu.subscriptions'), icon: Receipt, to: '/admin/billing' }] : []),
  ] as { label: string; icon: typeof Star; to: string | null; action?: () => void }[];

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
          { icon: Star, value: (user.reputationScore ?? 0).toFixed(1), label: t('stats.reputation'), color: '#f5b800' },
          { icon: ArrowLeftRight, value: user.totalTradesCompleted ?? 0, label: t('stats.trades'), color: '#1a3461' },
          { icon: MessageCircle, value: user.totalReviewsReceived ?? 0, label: t('stats.reviews'), color: '#10b981' },
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

      <LanguageSwitcher />
      <PushToggle />
      <InviteCard />

      {/* Menu list */}
      <div className="bg-white rounded-2xl shadow-sm border border-border/50 overflow-hidden mb-4">
        {menuItems.map(({ label, icon: Icon, to, action }, idx) => (
          <button
            key={label}
            onClick={() => (action ? action() : to && navigate(to))}
            className={`w-full flex items-center gap-3 px-4 py-4 transition-colors text-left hover:bg-surface-dark ${
              idx < menuItems.length - 1 ? 'border-b border-border/50' : ''
            }`}
          >
            <span
              className="w-9 h-9 rounded-full border-2 border-border flex items-center justify-center shrink-0"
              style={{ color: '#1a3461' }}
            >
              <Icon size={16} />
            </span>
            <span className="flex-1 text-sm font-medium text-text">{label}</span>
            <ChevronRight size={16} className="text-text-muted" />
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
          <span className="flex-1 text-sm font-medium text-danger">{t('logout')}</span>
          <ChevronRight size={16} className="text-danger/40" />
        </button>
      </div>
      <ReviewsSheet userId={user.id} open={showReviews} onClose={() => setShowReviews(false)} />
      <BlockedUsersSheet open={showBlocked} onClose={() => setShowBlocked(false)} />
    </div>
  );
}
