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
        `relative flex flex-col items-center justify-center gap-0.5 flex-1 py-2 transition-colors ${
          isActive ? 'text-primary' : 'text-text-secondary hover:text-text'
        }`
      }
    >
      {({ isActive }) => (
        <>
          {/* Voce attiva: trattino oro sopra l'icona */}
          {isActive && <span aria-hidden="true" className="absolute top-0 w-7 h-[3px] rounded-full bg-primary" />}
          <Icon size={22} strokeWidth={isActive ? 2.2 : 1.7} />
          <span className={`text-[10px] ${isActive ? 'font-bold' : 'font-semibold'}`}>{label}</span>
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
      <nav className="fixed bottom-0 left-0 right-0 z-50 safe-bottom md:hidden" style={{ background: '#0e1830', borderTop: '1px solid #1e2b46' }}>
        <div className="flex items-center h-16 px-2">
          {LEFT_ITEMS.map(({ to, icon, key }) => <NavItemLink key={to} to={to} icon={icon} label={t(key)} />)}

          {/* Home: pulsante rotondo più grande, sporge sopra la barra */}
          <div className="flex-1 flex justify-center">
            <NavLink
              to="/home"
              aria-label={t('nav.home')}
              className={({ isActive }) =>
                `-mt-7 w-16 h-16 rounded-full flex items-center justify-center bg-primary text-on-primary
                 ring-4 ring-[#0e1830] transition-all active:scale-95
                 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary
                 ${isActive ? 'shadow-[0_6px_22px_rgba(245,184,46,0.55)]' : 'shadow-[0_6px_18px_rgba(245,184,46,0.25)] hover:bg-primary-light'}`
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
