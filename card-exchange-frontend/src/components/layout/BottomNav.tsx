import { useState, useEffect, useCallback } from 'react';
import { NavLink } from 'react-router-dom';
import { Home, Compass, Library, Heart, UserCircle, MessageSquare, Bell } from 'lucide-react';
import { useAuth } from '../../context/AuthContext';
import { useNotifications } from '../../hooks/useNotifications';
import { messages } from '../../api';
import NotificationCenter from '../notifications/NotificationCenter';

const NAV_ITEMS = [
  { to: '/home', icon: Home, label: 'Home' },
  { to: '/explore', icon: Compass, label: 'Esplora' },
  { to: '/collection', icon: Library, label: 'Collezione' },
  { to: '/wishlist', icon: Heart, label: 'Wishlist' },
  { to: '/profile', icon: UserCircle, label: 'Profilo' },
];

export default function BottomNav() {
  const { user } = useAuth();
  const { unreadCount, setUnreadCount } = useNotifications(!!user);
  const [showNotifications, setShowNotifications] = useState(false);
  const [chatUnread, setChatUnread] = useState(0);

  const fetchChatUnread = useCallback(async () => {
    if (!user) return;
    try {
      const { data } = await messages.getUnreadCount();
      setChatUnread(data?.unreadCount ?? 0);
    } catch {}
  }, [user]);

  useEffect(() => {
    fetchChatUnread();
    const interval = setInterval(fetchChatUnread, 30_000);
    return () => clearInterval(interval);
  }, [fetchChatUnread]);

  const totalBadge = chatUnread + unreadCount;

  return (
    <>
      <nav className="fixed bottom-0 left-0 right-0 z-50 safe-bottom md:hidden" style={{ background: '#1a3461' }}>
        <div className="flex justify-around items-center h-16 px-2">
          {NAV_ITEMS.map(({ to, icon: Icon, label }) => (
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
                  <span className="text-[9px] font-medium">{label}</span>
                </>
              )}
            </NavLink>
          ))}
        </div>
      </nav>

      {/* Notification center triggered from sidebar/header */}
      <NotificationCenter
        open={showNotifications}
        onClose={() => setShowNotifications(false)}
        onUnreadCountChange={setUnreadCount}
      />
    </>
  );
}

/** Exported badge counts for the mobile header to consume */
export function useNavBadges() {
  const { user } = useAuth();
  const { unreadCount } = useNotifications(!!user);
  const [chatUnread, setChatUnread] = useState(0);

  const fetch = useCallback(async () => {
    if (!user) return;
    try {
      const { data } = await messages.getUnreadCount();
      setChatUnread(data?.unreadCount ?? 0);
    } catch {}
  }, [user]);

  useEffect(() => {
    fetch();
    const id = setInterval(fetch, 30_000);
    return () => clearInterval(id);
  }, [fetch]);

  return { chatUnread, notifUnread: unreadCount };
}
