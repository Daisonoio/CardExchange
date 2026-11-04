import { useState } from 'react';
import { NavLink } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Home, Compass, Library, Heart, UserCircle } from 'lucide-react';
import { useRealtime } from '../../context/RealtimeContext';
import NotificationCenter from '../notifications/NotificationCenter';

const NAV_ITEMS = [
  { to: '/home', icon: Home, key: 'nav.home' },
  { to: '/explore', icon: Compass, key: 'nav.explore' },
  { to: '/collection', icon: Library, key: 'nav.collection' },
  { to: '/wishlist', icon: Heart, key: 'nav.wishlist' },
  { to: '/profile', icon: UserCircle, key: 'nav.profile' },
] as const;

export default function BottomNav() {
  const { t } = useTranslation();
  const { setNotifUnread } = useRealtime();
  const [showNotifications, setShowNotifications] = useState(false);

  return (
    <>
      <nav className="fixed bottom-0 left-0 right-0 z-50 safe-bottom md:hidden" style={{ background: '#1a3461' }}>
        <div className="flex justify-around items-center h-16 px-2">
          {NAV_ITEMS.map(({ to, icon: Icon, key }) => (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) =>
                `flex flex-col items-center justify-center gap-0.5 flex-1 py-2 transition-colors ${
                  isActive ? 'text-secondary' : 'text-white/50 hover:text-white/80'
                }`
              }
            >
              {({ isActive }) => (
                <>
                  <Icon size={22} strokeWidth={isActive ? 2.2 : 1.6} />
                  <span className="text-[9px] font-medium">{t(key)}</span>
                </>
              )}
            </NavLink>
          ))}
        </div>
      </nav>

      <NotificationCenter
        open={showNotifications}
        onClose={() => setShowNotifications(false)}
        onUnreadCountChange={setNotifUnread}
      />
    </>
  );
}
