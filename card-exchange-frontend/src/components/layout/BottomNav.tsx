import { useState } from 'react';
import { NavLink } from 'react-router-dom';
import { Home, Compass, Library, Heart, ArrowLeftRight, Bell } from 'lucide-react';
import { useAuth } from '../../context/AuthContext';
import { useNotifications } from '../../hooks/useNotifications';
import NotificationCenter from '../notifications/NotificationCenter';

const NAV_ITEMS = [
  { to: '/explore', icon: Compass, label: 'Esplora' },
  { to: '/collection', icon: Library, label: 'Collezione' },
  { to: '/wishlist', icon: Heart, label: 'Ricerca' },
  { to: '/trades', icon: ArrowLeftRight, label: 'Scambi' },
];

export default function BottomNav() {
  const { user } = useAuth();
  const { unreadCount, setUnreadCount } = useNotifications(!!user);
  const [showNotifications, setShowNotifications] = useState(false);

  return (
    <>
      <nav className="fixed bottom-0 left-0 right-0 z-50 bg-white border-t border-border safe-bottom md:hidden">
        <div className="flex justify-around items-center h-16">
          {NAV_ITEMS.map(({ to, icon: Icon, label }) => (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) =>
                `flex flex-col items-center gap-0.5 px-2 py-1 text-xs transition-colors ${
                  isActive
                    ? 'text-primary font-semibold'
                    : 'text-text-muted hover:text-text-secondary'
                }`
              }
            >
              <Icon size={22} strokeWidth={1.8} />
              <span>{label}</span>
            </NavLink>
          ))}
          {/* Notification bell */}
          <button
            onClick={() => setShowNotifications(true)}
            className="flex flex-col items-center gap-0.5 px-2 py-1 text-xs text-text-muted hover:text-text-secondary transition-colors relative"
          >
            <div className="relative">
              <Bell size={22} strokeWidth={1.8} />
              {unreadCount > 0 && (
                <span className="absolute -top-1 -right-1.5 min-w-[16px] h-4 bg-red-500 text-white text-[10px] font-bold rounded-full flex items-center justify-center px-1">
                  {unreadCount > 99 ? '99+' : unreadCount}
                </span>
              )}
            </div>
            <span>Notifiche</span>
          </button>
        </div>
      </nav>

      <NotificationCenter
        open={showNotifications}
        onClose={() => setShowNotifications(false)}
        onUnreadCountChange={setUnreadCount}
      />
    </>
  );
}
