import { Fragment, Suspense } from 'react';
import { lazyPage } from './utils/lazyPage';
import PageLoader from './components/ui/PageLoader';
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
const ForgotPasswordPage = lazyPage(() => import('./pages/ForgotPasswordPage'));
const ResetPasswordPage = lazyPage(() => import('./pages/ResetPasswordPage'));
const VerifyEmailPage = lazyPage(() => import('./pages/VerifyEmailPage'));
const ExplorePage = lazyPage(() => import('./pages/ExplorePage'));
const CollectionPage = lazyPage(() => import('./pages/CollectionPage'));
const WishlistPage = lazyPage(() => import('./pages/WishlistPage'));
const ProfilePage = lazyPage(() => import('./pages/ProfilePage'));
const EditProfilePage = lazyPage(() => import('./pages/EditProfilePage'));
import HomePage from './pages/HomePage';
const UserCollectionPage = lazyPage(() => import('./pages/UserCollectionPage'));
const TradesPage = lazyPage(() => import('./pages/TradesPage'));
const TradeRequestPage = lazyPage(() => import('./pages/TradeRequestPage'));
const ChatPage = lazyPage(() => import('./pages/ChatPage'));
const MatchmakingPage = lazyPage(() => import('./pages/MatchmakingPage'));
const ModerationPage = lazyPage(() => import('./pages/ModerationPage'));
const PremiumPage = lazyPage(() => import('./pages/PremiumPage'));
const StatsPage = lazyPage(() => import('./pages/StatsPage'));
const SearchPage = lazyPage(() => import('./pages/SearchPage'));
const AdminBillingPage = lazyPage(() => import('./pages/AdminBillingPage'));
const ProtoShell = lazyPage(() => import('./proto/ProtoShell'));
const ExplorePreview = lazyPage(() => import('./proto/ExplorePreview'));
const CardPreview = lazyPage(() => import('./proto/CardPreview'));
const MapPreview = lazyPage(() => import('./proto/MapPreview'));
const CollectionPreview = lazyPage(() => import('./proto/CollectionPreview'));
const LocalPage = lazyPage(() => import('./pages/LocalPage'));
const StorePage = lazyPage(() => import('./pages/StorePage'));
const StoreResultsPage = lazyPage(() => import('./pages/StoreResultsPage'));
const StoreDemandPage = lazyPage(() => import('./pages/StoreDemandPage'));
const ReservationsPage = lazyPage(() => import('./pages/ReservationsPage'));
const StoreBuyListPage = lazyPage(() => import('./pages/StoreBuyListPage'));
const SellPage = lazyPage(() => import('./pages/SellPage'));
const PurchasesPage = lazyPage(() => import('./pages/PurchasesPage'));
const ShowcasePage = lazyPage(() => import('./pages/ShowcasePage'));
const MyShowcasePage = lazyPage(() => import('./pages/MyShowcasePage'));
const SponsorPage = lazyPage(() => import('./pages/SponsorPage'));
const AdminSponsorsPage = lazyPage(() => import('./pages/AdminSponsorsPage'));
const ExhibitorInvitePage = lazyPage(() => import('./pages/ExhibitorInvitePage'));
const PromotionsPage = lazyPage(() => import('./pages/PromotionsPage'));
const LoyaltyPage = lazyPage(() => import('./pages/LoyaltyPage'));
const StoreLoyaltyPage = lazyPage(() => import('./pages/StoreLoyaltyPage'));
const StoreEarningsPage = lazyPage(() => import('./pages/StoreEarningsPage'));
const StoreReservationsPage = lazyPage(() => import('./pages/StoreReservationsPage'));
const NotificationsGuidePage = lazyPage(() => import('./pages/NotificationsGuidePage'));
const LivePage = lazyPage(() => import('./pages/LivePage'));
const QrPage = lazyPage(() => import('./pages/QrPage'));
const LegalPage = lazyPage(() => import('./pages/legal/LegalPage'));
const AccountPrivacyPage = lazyPage(() => import('./pages/AccountPrivacyPage'));
const AdminMonitoringPage = lazyPage(() => import('./pages/AdminMonitoringPage'));
const AdminMetricsPage = lazyPage(() => import('./pages/AdminMetricsPage'));
const WelcomePage = lazyPage(() => import('./pages/WelcomePage'));
const StoreLinkPage = lazyPage(() => import('./pages/StoreLinkPage'));
const LiveComposePage = lazyPage(() => import('./pages/LiveComposePage'));
const FollowingPage = lazyPage(() => import('./pages/FollowingPage'));
const StoreFormPage = lazyPage(() => import('./pages/StoreFormPage'));
const EventPage = lazyPage(() => import('./pages/EventPage'));
const EventFormPage = lazyPage(() => import('./pages/EventFormPage'));

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
          <Suspense fallback={<PageLoader />}>
          <Routes>
            {/* Public routes */}
            <Route path="/login" element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />
            {/* Link breve stampato nel QR del negozio */}
            <Route path="/s/:slug" element={<StoreLinkPage />} />
            {/* Documenti legali: pubblici */}
            <Route path="/privacy" element={<LegalPage doc="privacy" />} />
            <Route path="/terms" element={<LegalPage doc="terms" />} />
            <Route path="/cookies" element={<LegalPage doc="cookies" />} />
            {/* Guida alle notifiche (iPhone: aggiungere alla schermata Home) */}
            <Route path="/help/notifications" element={<NotificationsGuidePage />} />
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
              <Route path="/live/new" element={<LiveComposePage />} />
              <Route path="/live/:id" element={<LivePage />} />
              <Route path="/following" element={<FollowingPage />} />
              <Route path="/qr/:kind/:id?" element={<QrPage />} />
              <Route path="/profile/privacy" element={<AccountPrivacyPage />} />
              <Route path="/stores/:id/edit" element={<StoreFormPage />} />
              <Route path="/stores/:id/results" element={<StoreResultsPage />} />
              <Route path="/stores/:id/demand" element={<StoreDemandPage />} />
              <Route path="/stores/:id/reservations" element={<StoreReservationsPage />} />
              <Route path="/reservations" element={<ReservationsPage />} />
              <Route path="/stores/:id/buylist" element={<StoreBuyListPage />} />
              <Route path="/sell" element={<SellPage />} />
              <Route path="/purchases" element={<PurchasesPage />} />
              <Route path="/stores/:id/earnings" element={<StoreEarningsPage />} />
              <Route path="/stores/:id/loyalty" element={<StoreLoyaltyPage />} />
              <Route path="/loyalty" element={<LoyaltyPage />} />
              <Route path="/promotions" element={<PromotionsPage />} />
              <Route path="/exhibit/:token" element={<ExhibitorInvitePage />} />
              <Route path="/sponsors/:id" element={<SponsorPage />} />
              <Route path="/admin/sponsors" element={<AdminSponsorsPage />} />
              <Route path="/v/:slug" element={<ShowcasePage />} />
              <Route path="/showcase" element={<MyShowcasePage />} />
              <Route path="/profile" element={<ProfilePage />} />
              <Route path="/profile/edit" element={<EditProfilePage />} />
              <Route path="/moderation" element={<ModerationPage />} />
              <Route path="/premium" element={<PremiumPage />} />
              <Route path="/stats" element={<StatsPage />} />
              <Route path="/search" element={<SearchPage />} />
              <Route path="/admin/billing" element={<AdminBillingPage />} />
              <Route path="/admin/monitoring" element={<AdminMonitoringPage />} />
              <Route path="/admin/metrics" element={<AdminMetricsPage />} />
              <Route path="/welcome" element={<WelcomePage />} />
            </Route>

            {/* Anteprima della nuova grafica (stesso login, pagine reali) */}
            <Route path="/preview" element={<ProtoShell />}>
              <Route index element={<ExplorePreview />} />
              <Route path="map" element={<MapPreview />} />
              <Route path="card/:id" element={<CardPreview />} />
              <Route path="collection" element={<CollectionPreview />} />
            </Route>

            {/* Default redirect */}
            <Route path="*" element={<Navigate to="/home" replace />} />
          </Routes>
          </Suspense>
          </LanguageRoot>
          </GameProvider>
          </RealtimeProvider>
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
    </ErrorBoundary>
  );
}
