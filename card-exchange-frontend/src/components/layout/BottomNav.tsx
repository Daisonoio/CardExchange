import { NavLink } from 'react-router-dom';
import { Compass, Library, Heart, CalendarDays, UserCircle } from 'lucide-react';

const NAV_ITEMS = [
  { to: '/explore', icon: Compass, label: 'Esplora' },
  { to: '/collection', icon: Library, label: 'Collezione' },
  { to: '/wishlist', icon: Heart, label: 'Ricerca' },
  { to: '/events', icon: CalendarDays, label: 'Eventi' },
  { to: '/profile', icon: UserCircle, label: 'Profilo' },
];

export default function BottomNav() {
  return (
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
      </div>
    </nav>
  );
}
