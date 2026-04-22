import { useState, useCallback, useEffect } from 'react';
import { useLocation, NavLink, useNavigate } from 'react-router-dom';
import {
  Menu, X, Layers, Bell, MessageSquare, Sparkles,
  ArrowLeftRight, LogOut,
} from 'lucide-react';
import { useAuth } from '../../context/AuthContext';
import { useNotifications } from '../../hooks/useNotifications';
import { messages } from '../../api';
import NotificationCenter from '../notifications/NotificationCenter';

const PAGE_TITLES: Record<string, string> = {
  '/home': 'CardExchange',
  '/explore': 'Esplora',
  '/collection': 'Collezione',
  '/wishlist': 'Wishlist',
  '/profile': 'Profilo',
  '/trades': 'Scambi',
  '/matchmaking': 'Matchmaking',
  '/chat': 'Chat',
  '/profile/edit': 'Modifica Profilo',
};

const SIDEBAR_LINKS = [
  { to: '/trades', icon: ArrowLeftRight, label: 'Scambi' },
  { to: '/matchmaking', icon: Sparkles, label: 'Matchmaking' },
  { to: '/chat', icon: MessageSquare, label: 'Chat' },
];

export default function MobileHeader() {
  const { user, logout } = useAuth();
  const { unreadCount, setUnreadCount } = useNotifications(!!user);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [showNotif, setShowNotif] = useState(false);
  const [chatUnread, setChatUnread] = useState(0);
  const location = useLocation();
  const navigate = useNavigate();

  const fetchChat = useCallback(async () => {
    if (!user) return;
    try {
      const { data } = await messages.getUnreadCount();
      setChatUnread(data?.unreadCount ?? 0);
    } catch {}
  }, [user]);

  useEffect(() => {
    fetchChat();
    const id = setInterval(fetchChat, 30_000);
    return () => clearInterval(id);
  }, [fetchChat]);

  useEffect(() => {
    setDrawerOpen(false);
  }, [location.pathname]);

  const title = PAGE_TITLES[location.pathname] ?? 'CardExchange';
  const totalBadge = chatUnread + unreadCount;

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <>
      {/* Header bar */}
      <header
        className="fixed top-0 left-0 right-0 z-50 h-14 flex items-center justify-between px-4 md:hidden"
        style={{ background: '#ffffff', borderBottom: '1px solid #dde4ee' }}
      >
        {/* Hamburger with badge */}
        <button
          onClick={() => setDrawerOpen(true)}
          className="w-9 h-9 flex items-center justify-center rounded-lg text-text relative"
          aria-label="Menu"
        >
          <Menu size={22} />
          {chatUnread > 0 && (
            <span
              className="absolute -top-0.5 -right-0.5 min-w-[16px] h-4 text-white text-[9px] font-bold rounded-full flex items-center justify-center px-1"
              style={{ background: '#ef4444' }}
            >
              {chatUnread > 99 ? '99+' : chatUnread}
            </span>
          )}
        </button>

        <div className="flex items-center gap-2 font-bold text-lg" style={{ color: '#1a3461' }}>
          <Layers size={20} />
          <span>{title}</span>
        </div>

        <button
          onClick={() => setShowNotif(true)}
          className="w-9 h-9 flex items-center justify-center rounded-lg relative"
          style={{ color: '#1a3461' }}
          aria-label="Notifiche"
        >
          <Bell size={20} />
          {unreadCount > 0 && (
            <span
              className="absolute -top-0.5 -right-0.5 min-w-[16px] h-4 text-white text-[9px] font-bold rounded-full flex items-center justify-center px-1"
              style={{ background: '#ef4444' }}
            >
              {unreadCount > 99 ? '99+' : unreadCount}
            </span>
          )}
        </button>
      </header>

      {/* Overlay */}
      {drawerOpen && (
        <div
          className="fixed inset-0 z-50 bg-black/40 md:hidden"
          onClick={() => setDrawerOpen(false)}
        />
      )}

      {/* Sidebar drawer */}
      <aside
        className="fixed top-0 left-0 h-full w-72 z-50 flex flex-col md:hidden transition-transform duration-300"
        style={{
          background: '#1a3461',
          transform: drawerOpen ? 'translateX(0)' : 'translateX(-100%)',
        }}
      >
        {/* Drawer header */}
        <div className="flex items-center justify-between px-5 pt-10 pb-6 border-b border-white/10">
          <div className="flex items-center gap-2 text-white font-bold text-lg">
            <Layers size={22} />
            <span>CardExchange</span>
          </div>
          <button
            onClick={() => setDrawerOpen(false)}
            className="text-white/60 hover:text-white transition-colors"
          >
            <X size={20} />
          </button>
        </div>

        {/* User info */}
        {user && (
          <div className="px-5 py-4 border-b border-white/10">
            <div className="w-12 h-12 rounded-full flex items-center justify-center mb-2 text-sm font-bold text-white" style={{ background: '#f5b800', color: '#1a3461' }}>
              {user.firstName?.[0]}{user.lastName?.[0]}
            </div>
            <p className="text-white font-semibold">{user.firstName} {user.lastName}</p>
            <p className="text-white/50 text-sm">@{user.username}</p>
          </div>
        )}

        {/* Nav links */}
        <nav className="flex-1 px-3 py-4 space-y-1 overflow-y-auto">
          {SIDEBAR_LINKS.map(({ to, icon: Icon, label }) => (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) =>
                `flex items-center gap-3 px-4 py-3 rounded-xl text-sm font-medium transition-colors ${
                  isActive
                    ? 'bg-white/15 text-white'
                    : 'text-white/60 hover:bg-white/10 hover:text-white'
                }`
              }
            >
              <Icon size={18} />
              <span>{label}</span>
              {to === '/chat' && chatUnread > 0 && (
                <span className="ml-auto min-w-[20px] h-5 bg-secondary text-primary text-[10px] font-bold rounded-full flex items-center justify-center px-1">
                  {chatUnread}
                </span>
              )}
            </NavLink>
          ))}
        </nav>

        {/* Logout */}
        <div className="px-3 pb-6 border-t border-white/10 pt-4">
          <button
            onClick={handleLogout}
            className="flex items-center gap-3 px-4 py-3 rounded-xl text-sm font-medium text-white/60 hover:text-white hover:bg-white/10 transition-colors w-full"
          >
            <LogOut size={18} />
            <span>Esci</span>
          </button>
        </div>
      </aside>

      <NotificationCenter
        open={showNotif}
        onClose={() => setShowNotif(false)}
        onUnreadCountChange={setUnreadCount}
      />
    </>
  );
}
