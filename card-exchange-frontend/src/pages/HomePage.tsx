import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { MapPin, Navigation, Loader2, Settings, Sparkles, X } from 'lucide-react';
import { wishlist } from '../api';
import { useAuth } from '../context/AuthContext';
import { useGame } from '../context/GameContext';
import { useGeolocation } from '../hooks/useGeolocation';
import CardCarousel from '../components/cards/CardCarousel';
import LiveStrip from '../components/social/LiveStrip';
import NearbyCardsGrid from '../components/home/NearbyCardsGrid';
import SponsoredEvents from '../components/promotions/SponsoredEvents';
import IosPushBanner from '../components/notifications/IosPushBanner';
import { useSearchRadius } from '../hooks/useSearchRadius';
import type { CarouselCard } from '../components/cards/CardCarousel';
import Button from '../components/ui/Button';
import EmptyState from '../components/ui/EmptyState';

export default function HomePage() {
  const { t } = useTranslation('home');
  const { user } = useAuth();
  const { selectedGameId, selectedGame, setSelectedGameId } = useGame();
  // Stesso raggio di Esplora e Matchmaking
  const [radiusKm] = useSearchRadius();
  const navigate = useNavigate();
  const { position, isLoading: geoLoading, requestPosition } = useGeolocation();

  const [topCards, setTopCards] = useState<CarouselCard[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  const hasProfileLocation = !!(user?.location?.latitude && user?.location?.longitude);
  // Tutte le carte vicine (sotto le carte della wishlist): undefined finché non arriva la prima pagina
  const [allTotal, setAllTotal] = useState<number | undefined>(undefined);
  const onAllTotal = useCallback((n: number) => setAllTotal(n), []);

  useEffect(() => {
    if (!user) return;

    const load = async () => {
      setIsLoading(true);
      try {
        let params: Record<string, string | number> = {};

        if (hasProfileLocation) {
          params = {};
        } else if (position) {
          params = { latitude: position.latitude, longitude: position.longitude };
        } else {
          setIsLoading(false);
          return;
        }

        if (selectedGameId) params.gameId = selectedGameId;
        params.radiusKm = radiusKm;
        const { data } = await wishlist.topNearbyMatches(user.id, params);
        const cardsData = (data as any).cards ?? [];
        setTopCards(cardsData);
      } catch (err: any) {
        const msg = err?.response?.data?.message;
        if (msg !== 'NO_LOCATION') {
          console.error('Errore caricamento home:', err);
        }
      } finally {
        setIsLoading(false);
      }
    };

    load();
  }, [user, position, hasProfileLocation, selectedGameId, radiusKm]);

  const handleCardClick = (card: CarouselCard) => {
    navigate(`/collection/${card.ownerId}`, {
      state: { preselectCardId: card.cardId, preselectCardInfoId: card.cardInfoId },
    });
  };

  if (!user) return null;

  const showLocationPrompt = !hasProfileLocation && !position && !isLoading;

  // Cosa sta filtrando la Home: va detto chiaramente, altrimenti sembra che manchino carte
  const filterBar = (
    <div className="flex flex-wrap items-center gap-2 mb-3 text-xs">
      <span className="text-text-muted">{t('nearby.scope', { km: radiusKm })}</span>
      {selectedGame && (
        <button
          type="button"
          onClick={() => setSelectedGameId(null)}
          className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full bg-primary/10 text-primary font-semibold hover:bg-primary/15"
          aria-label={t('nearby.clearGame', { game: selectedGame.name })}
        >
          {t('nearby.onlyGame', { game: selectedGame.name })} <X size={12} aria-hidden="true" />
        </button>
      )}
      <button type="button" onClick={() => navigate('/explore')} className="text-primary font-semibold hover:underline">
        {t('nearby.changeRadius')}
      </button>
    </div>
  );

  return (
    // Su schermi larghi "Live ora" sta in una colonna a destra: il carosello resta tutto nella finestra
    <div className="lg:grid lg:grid-cols-[minmax(0,1fr)_17rem] lg:gap-6 lg:items-start">
      <aside className="lg:order-2 lg:sticky lg:top-32">
        {/* Dove sono ora i profili che segui ("Sono qui") */}
        <LiveStrip coords={hasProfileLocation ? null : position} />
      </aside>
      <div className="lg:order-1 min-w-0">

      <IosPushBanner />

      {/* Eventi in evidenza vicino (sponsorizzati, sempre etichettati) */}
      {(hasProfileLocation || position) && (
        <SponsoredEvents coords={hasProfileLocation ? null : position} className="mb-4" />
      )}

      {/* Location prompt */}
      {showLocationPrompt && (
        <div className="bg-white rounded-2xl p-5 shadow-sm border border-border/50 mb-4">
          <div className="flex items-center gap-2 mb-2">
            <MapPin size={16} style={{ color: '#1a3461' }} />
            <h2 className="text-sm font-bold text-text">{t('locationPrompt.title')}</h2>
          </div>
          <p className="text-sm text-text-secondary mb-4">
            {t('locationPrompt.description')}
          </p>
          <div className="space-y-2.5">
            <Button onClick={() => navigate('/welcome')} className="w-full">
              <Sparkles size={14} />
              {t('locationPrompt.quickStart')}
            </Button>
            <Button onClick={() => navigate('/profile/edit')} variant="outline" className="w-full">
              <Settings size={14} />
              {t('locationPrompt.setInProfile')}
            </Button>
            <Button onClick={requestPosition} className="w-full" isLoading={geoLoading}>
              <Navigation size={14} />
              {t('locationPrompt.useCurrent')}
            </Button>
          </div>
        </div>
      )}

      {/* Loading */}
      {isLoading && (
        <div className="flex justify-center py-16">
          <Loader2 size={28} className="animate-spin" style={{ color: '#1a3461' }} />
        </div>
      )}

      {/* Cards nearby */}
      {!isLoading && !showLocationPrompt && topCards.length > 0 && (
        <div>
          <div className="flex items-center justify-between mb-3">
            <h2 className="text-base font-bold text-text">{t('nearby.title')}</h2>
            <span className="text-xs text-text-muted">{t('nearby.results', { count: topCards.length })}</span>
          </div>
          {filterBar}
          <div className="w-full -mx-4" style={{ width: 'calc(100% + 2rem)' }}>
            <CardCarousel cards={topCards} onCardClick={handleCardClick} />
          </div>
        </div>
      )}

      {/* No matches */}
      {!isLoading && !showLocationPrompt && (hasProfileLocation || position) && topCards.length === 0 && allTotal === 0 && (
        <EmptyState
          icon={Sparkles}
          title={t('empty.title')}
          description={selectedGame ? t('empty.descriptionGame', { game: selectedGame.name, km: radiusKm }) : t('empty.description')}
          action={
            <div className="flex flex-wrap justify-center gap-2">
              {selectedGame && (
                <Button onClick={() => setSelectedGameId(null)} size="sm">
                  {t('nearby.allGames')}
                </Button>
              )}
              <Button onClick={() => navigate('/wishlist')} size="sm" variant="outline">
                Wishlist
              </Button>
              <Button onClick={() => navigate('/explore')} size="sm">
                {t('common:nav.explore')}
              </Button>
            </div>
          }
        />
      )}

      {/* Tutte le carte vicine: negozi e privati insieme, dalla più vicina */}
      {!showLocationPrompt && (hasProfileLocation || position) && (
        <NearbyCardsGrid gameId={selectedGameId} radiusKm={radiusKm}
          coords={hasProfileLocation ? null : position} onTotal={onAllTotal} />
      )}
      </div>
    </div>
  );
}
