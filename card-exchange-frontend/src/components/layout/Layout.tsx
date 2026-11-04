import { Outlet, Navigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import TopBar from './TopBar';
import BottomNav from './BottomNav';
import GameSelector from './GameSelector';
import MobileHeader from './MobileHeader';
import VerifyEmailBanner from './VerifyEmailBanner';
import AgeGate from './AgeGate';

export default function Layout() {
  const { isAuthenticated, isLoading, user } = useAuth();

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="w-8 h-8 border-3 border-primary border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  // Account creati prima del controllo dell'età: data di nascita obbligatoria prima di continuare
  if (user?.birthDateRequired) {
    return <AgeGate />;
  }

  return (
    <div className="flex flex-col min-h-screen">
      {/* Sfondo decorativo dell'app: i due cerchi del marchio, fissi e dietro a tutto */}
      <div aria-hidden="true" className="fixed inset-0 -z-10 overflow-hidden pointer-events-none">
        {/* Sotto la barra in alto e i filtri dei giochi, così si vedono interi */}
        <div className="absolute top-[7rem] -right-40 w-[22rem] h-[22rem] rounded-full opacity-[0.16] md:-right-24 md:w-[30rem] md:h-[30rem]" style={{ background: '#f5b800' }} />
        <div className="absolute top-[18rem] -right-6 w-48 h-48 rounded-full opacity-[0.08] md:top-[22rem] md:right-56 md:w-64 md:h-64" style={{ background: '#1a3461' }} />
      </div>
      {/* Desktop nav */}
      <TopBar />
      {/* Mobile top bar with hamburger */}
      <MobileHeader />
      <GameSelector />
      <main className="flex-1 pb-20 md:pb-4 pt-14 md:pt-0">
        <div className="max-w-2xl mx-auto px-4 py-4">
          <Outlet />
        </div>
      </main>
      <BottomNav />
      {/* Avviso flottante: non occupa spazio nella pagina */}
      <VerifyEmailBanner />
    </div>
  );
}
