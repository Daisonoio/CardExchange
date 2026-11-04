import { useState } from 'react';
import { NavLink } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Home, Compass, Library, Heart, UserCircle } from 'lucide-react';
import { useRealtime } from '../../context/RealtimeContext';
import NotificationCenter from '../notifications/NotificationCenter';

// La Home sta al centro, in evidenza: due voci a sinistra e due a destra
const LEFT_ITEMS = [
  { to: '/explore', icon: Compass, key: 'nav.explore' },
  { to: '/collection', icon: Library, key: 'nav.collection' },
] as const;
const RIGHT_ITEMS = [
  { to: '/wishlist', icon: Heart, key: 'nav.wishlist' },
  { to: '/profile', icon: UserCircle, key: 'nav.profile' },
] as const;

type NavItem = (typeof LEFT_ITEMS)[number] | (typeof RIGHT_ITEMS)[number];

function NavItemLink({ to, icon: Icon, label }: { to: NavItem['to']; icon: NavItem['icon']; label: string }) {
  return (
    <NavLink
      to={to}
      // /collection/123 è la collezione di un altro utente: "Collezione" si accende solo sulla propria
      end={to === '/collection'}
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
  );
}

export default function BottomNav() {
  const { t } = useTranslation();
  const { setNotifUnread } = useRealtime();
  const [showNotifications, setShowNotifications] = useState(false);

  return (
    <>
      <nav className="fixed bottom-0 left-0 right-0 z-50 safe-bottom md:hidden" style={{ background: '#1a3461' }}>
        <div className="flex items-center h-16 px-2">
          {LEFT_ITEMS.map(({ to, icon, key }) => <NavItemLink key={to} to={to} icon={icon} label={t(key)} />)}

          {/* Home: pulsante rotondo più grande, sporge sopra la barra */}
          <div className="flex-1 flex justify-center">
            <NavLink
              to="/home"
              aria-label={t('nav.home')}
              className={({ isActive }) =>
                `-mt-7 w-16 h-16 rounded-full flex items-center justify-center shadow-lg shadow-black/30
                 ring-4 ring-[#1a3461] transition-transform active:scale-95
                 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-secondary
                 ${isActive ? 'bg-secondary text-[#1a3461]' : 'bg-white text-[#1a3461] hover:bg-secondary/90'}`
              }
            >
              {({ isActive }) => <Home size={28} strokeWidth={isActive ? 2.4 : 2} />}
            </NavLink>
          </div>

          {RIGHT_ITEMS.map(({ to, icon, key }) => <NavItemLink key={to} to={to} icon={icon} label={t(key)} />)}
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
