import { useState, useEffect, useCallback } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import {
  Compass, Library, Heart, Sparkles, ArrowLeftRight,
  UserCircle, LogOut, Layers, Bell, MessageSquare,
} from 'lucide-react';
import { useAuth } from '../../context/AuthContext';
import { useNotifications } from '../../hooks/useNotifications';
import { messages } from '../../api';
import NotificationCenter from '../notifications/NotificationCenter';

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
  const { unreadCount, setUnreadCount } = useNotifications(!!user);
  const [showNotif, setShowNotif] = useState(false);
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
    const id = setInterval(fetchChatUnread, 30_000);
    return () => clearInterval(id);
  }, [fetchChatUnread]);

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <>
      <header className="hidden md:flex sticky top-0 z-50 items-center border-b border-border h-16" style={{ background: '#1a3461' }}>
        <div className="max-w-7xl mx-auto px-4 flex items-center justify-between w-full">
          <NavLink to="/home" className="flex items-center gap-2 font-bold text-xl text-white shrink-0">
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

          <div className="flex items-center gap-1 shrink-0">
            {/* Chat icon */}
            <NavLink
              to="/chat"
              className={({ isActive }) =>
                `relative p-2 rounded-lg transition-colors ${
                  isActive
                    ? 'bg-white/15 text-white'
                    : 'text-white/60 hover:bg-white/10 hover:text-white'
                }`
              }
              title="Chat"
            >
              <MessageSquare size={20} />
              {chatUnread > 0 && (
                <span className="absolute -top-0.5 -right-0.5 min-w-[16px] h-4 bg-secondary text-primary text-[9px] font-bold rounded-full flex items-center justify-center px-1">
                  {chatUnread > 99 ? '99+' : chatUnread}
                </span>
              )}
            </NavLink>

            {/* Notifications bell */}
            <button
              onClick={() => setShowNotif((v) => !v)}
              className="relative p-2 rounded-lg text-white/60 hover:bg-white/10 hover:text-white transition-colors"
              title="Notifiche"
            >
              <Bell size={20} />
              {unreadCount > 0 && (
                <span className="absolute -top-0.5 -right-0.5 min-w-[16px] h-4 bg-red-500 text-white text-[9px] font-bold rounded-full flex items-center justify-center px-1">
                  {unreadCount > 99 ? '99+' : unreadCount}
                </span>
              )}
            </button>

            {/* User + logout */}
            {user && (
              <span className="text-sm text-white/60 px-2">
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

      {/* Desktop notification panel */}
      <NotificationCenter
        open={showNotif}
        onClose={() => setShowNotif(false)}
        onUnreadCountChange={setUnreadCount}
        variant="panel"
      />
    </>
  );
}
