import { useState, useEffect, useCallback } from 'react';
import { NavLink } from 'react-router-dom';
import { Compass, Library, ArrowLeftRight, Bell, MessageSquare, Sparkles } from 'lucide-react';
import { useAuth } from '../../context/AuthContext';
import { useNotifications } from '../../hooks/useNotifications';
import { messages } from '../../api';
import NotificationCenter from '../notifications/NotificationCenter';

const NAV_ITEMS = [
  { to: '/explore', icon: Compass, label: 'Esplora' },
  { to: '/collection', icon: Library, label: 'Collezione' },
  { to: '/matchmaking', icon: Sparkles, label: 'Match' },
  { to: '/trades', icon: ArrowLeftRight, label: 'Scambi' },
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

  return (
    <>
      <nav className="fixed bottom-0 left-0 right-0 z-50 bg-white border-t border-border safe-bottom md:hidden">
        <div className="flex justify-around items-center h-16">
          {NAV_ITEMS.map(({ to, icon: Icon, label }) => (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) =>
                `flex flex-col items-center gap-0.5 px-1.5 py-1 text-[10px] transition-colors ${
                  isActive
                    ? 'text-primary font-semibold'
                    : 'text-text-muted hover:text-text-secondary'
                }`
              }
            >
              <Icon size={20} strokeWidth={1.8} />
              <span>{label}</span>
            </NavLink>
          ))}

          {/* Chat */}
          <NavLink
            to="/chat"
            className={({ isActive }) =>
              `flex flex-col items-center gap-0.5 px-1.5 py-1 text-[10px] transition-colors relative ${
                isActive ? 'text-primary font-semibold' : 'text-text-muted hover:text-text-secondary'
              }`
            }
          >
            <div className="relative">
              <MessageSquare size={20} strokeWidth={1.8} />
              {chatUnread > 0 && (
                <span className="absolute -top-1 -right-1.5 min-w-[14px] h-3.5 bg-primary text-white text-[9px] font-bold rounded-full flex items-center justify-center px-0.5">
                  {chatUnread > 99 ? '99+' : chatUnread}
                </span>
              )}
            </div>
            <span>Chat</span>
          </NavLink>

          {/* Notification bell */}
          <button
            onClick={() => setShowNotifications(true)}
            className="flex flex-col items-center gap-0.5 px-1.5 py-1 text-[10px] text-text-muted hover:text-text-secondary transition-colors relative"
          >
            <div className="relative">
              <Bell size={20} strokeWidth={1.8} />
              {unreadCount > 0 && (
                <span className="absolute -top-1 -right-1.5 min-w-[14px] h-3.5 bg-red-500 text-white text-[9px] font-bold rounded-full flex items-center justify-center px-0.5">
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
