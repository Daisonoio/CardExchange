import { Outlet, Navigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import TopBar from './TopBar';
import BottomNav from './BottomNav';

export default function Layout() {
  const { isAuthenticated, isLoading } = useAuth();

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

  return (
    <div className="flex flex-col min-h-screen">
      <TopBar />
      <main className="flex-1 pb-20 md:pb-4">
        <div className="max-w-1xl mx-auto px-8 py-4">
          <Outlet />
        </div>
      </main>
      <BottomNav />
    </div>
  );
}
