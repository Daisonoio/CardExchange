import { Fragment } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from './context/AuthContext';
import { GameProvider } from './context/GameContext';
import { RealtimeProvider } from './context/RealtimeContext';
import ErrorBoundary from './components/ErrorBoundary';
import Layout from './components/layout/Layout';
import LoginPage from './pages/LoginPage';
import RegisterPage from './pages/RegisterPage';
import ForgotPasswordPage from './pages/ForgotPasswordPage';
import ResetPasswordPage from './pages/ResetPasswordPage';
import VerifyEmailPage from './pages/VerifyEmailPage';
import ExplorePage from './pages/ExplorePage';
import CollectionPage from './pages/CollectionPage';
import WishlistPage from './pages/WishlistPage';
import ProfilePage from './pages/ProfilePage';
import EditProfilePage from './pages/EditProfilePage';
import HomePage from './pages/HomePage';
import UserCollectionPage from './pages/UserCollectionPage';
import TradesPage from './pages/TradesPage';
import TradeRequestPage from './pages/TradeRequestPage';
import ChatPage from './pages/ChatPage';
import MatchmakingPage from './pages/MatchmakingPage';
import ModerationPage from './pages/ModerationPage';
import PremiumPage from './pages/PremiumPage';
import StatsPage from './pages/StatsPage';
import SearchPage from './pages/SearchPage';
import AdminBillingPage from './pages/AdminBillingPage';
import LocalPage from './pages/LocalPage';
import StorePage from './pages/StorePage';
import StoreFormPage from './pages/StoreFormPage';
import EventPage from './pages/EventPage';
import EventFormPage from './pages/EventFormPage';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: 1, staleTime: 30_000 },
  },
});

/** Al cambio lingua si rimonta l'interfaccia, così si aggiornano anche le etichette calcolate fuori dai componenti. */
function LanguageRoot({ children }: { children: React.ReactNode }) {
  const { i18n } = useTranslation();
  return <Fragment key={i18n.resolvedLanguage}>{children}</Fragment>;
}

export default function App() {
  return (
    <ErrorBoundary>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider>
          <RealtimeProvider>
          <GameProvider>
          <LanguageRoot>
          <Routes>
            {/* Public routes */}
            <Route path="/login" element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />
            <Route path="/forgot-password" element={<ForgotPasswordPage />} />
            <Route path="/reset-password" element={<ResetPasswordPage />} />
            <Route path="/verify-email" element={<VerifyEmailPage />} />

            {/* Protected routes */}
            <Route element={<Layout />}>
              <Route path="/home" element={<HomePage />} />
              <Route path="/explore" element={<ExplorePage />} />
              <Route path="/collection" element={<CollectionPage />} />
              <Route path="/collection/:userId" element={<UserCollectionPage />} />
              <Route path="/matchmaking" element={<MatchmakingPage />} />
              <Route path="/trades" element={<TradesPage />} />
              <Route path="/trades/new/:userId" element={<TradeRequestPage />} />
              <Route path="/chat" element={<ChatPage />} />
              <Route path="/wishlist" element={<WishlistPage />} />
              <Route path="/events" element={<Navigate to="/local?tab=events" replace />} />
              <Route path="/local" element={<LocalPage />} />
              <Route path="/events/new" element={<EventFormPage />} />
              <Route path="/events/:id" element={<EventPage />} />
              <Route path="/stores/new" element={<StoreFormPage />} />
              <Route path="/stores/:id" element={<StorePage />} />
              <Route path="/stores/:id/edit" element={<StoreFormPage />} />
              <Route path="/profile" element={<ProfilePage />} />
              <Route path="/profile/edit" element={<EditProfilePage />} />
              <Route path="/moderation" element={<ModerationPage />} />
              <Route path="/premium" element={<PremiumPage />} />
              <Route path="/stats" element={<StatsPage />} />
              <Route path="/search" element={<SearchPage />} />
              <Route path="/admin/billing" element={<AdminBillingPage />} />
            </Route>

            {/* Default redirect */}
            <Route path="*" element={<Navigate to="/home" replace />} />
          </Routes>
          </LanguageRoot>
          </GameProvider>
          </RealtimeProvider>
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
    </ErrorBoundary>
  );
}
