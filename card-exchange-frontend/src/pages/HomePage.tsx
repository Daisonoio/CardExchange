import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { MapPin, Navigation, Loader2, Settings, Sparkles } from 'lucide-react';
import { wishlist } from '../api';
import { useAuth } from '../context/AuthContext';
import { useGeolocation } from '../hooks/useGeolocation';
import CardCarousel from '../components/cards/CardCarousel';
import type { CarouselCard } from '../components/cards/CardCarousel';
import Button from '../components/ui/Button';
import EmptyState from '../components/ui/EmptyState';

export default function HomePage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const { position, isLoading: geoLoading, requestPosition } = useGeolocation();

  const [topCards, setTopCards] = useState<CarouselCard[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [usedLivePosition, setUsedLivePosition] = useState(false);

  const hasProfileLocation = !!(user?.location?.latitude && user?.location?.longitude);

  // Load top cards when we have a location source
  useEffect(() => {
    if (!user) return;

    const load = async () => {
      setIsLoading(true);
      setError(null);
      try {
        let params: Record<string, string | number> = {};

        if (hasProfileLocation) {
          // Use saved profile location
          params = {};
        } else if (position) {
          // Use live GPS coordinates
          params = { latitude: position.latitude, longitude: position.longitude };
          setUsedLivePosition(true);
        } else {
          // No location at all
          setIsLoading(false);
          return;
        }

        const { data } = await wishlist.topNearbyMatches(user.id, params);
        const cardsData = (data as any).cards ?? [];
        setTopCards(cardsData);
      } catch (err: any) {
        const msg = err?.response?.data?.message;
        if (msg === 'NO_LOCATION') {
          setError('NO_LOCATION');
        } else {
          console.error('Errore caricamento home:', err);
        }
      } finally {
        setIsLoading(false);
      }
    };

    load();
  }, [user, position, hasProfileLocation]);

  const handleCardClick = (card: CarouselCard) => {
    navigate(`/collection/${card.ownerId}`);
  };

  const handleUseLivePosition = () => {
    requestPosition();
  };

  if (!user) return null;

  // No location set at all
  const showLocationPrompt = !hasProfileLocation && !position && !isLoading;

  return (
    <div>
      {/* Welcome */}
      <div className="max-w-lg mx-auto mb-5">
        <h1 className="text-xl font-bold text-text">
          Ciao, {user.firstName}!
        </h1>
        <p className="text-sm text-text-secondary mt-0.5">
          Ecco le carte più ricercate disponibili vicino a te
        </p>
      </div>

      {/* Location prompt */}
      {showLocationPrompt && (
        <div className="max-w-lg mx-auto bg-white rounded-2xl p-5 shadow-sm border border-border/50 mb-4">
          <div className="flex items-center gap-2 mb-3">
            <MapPin size={18} className="text-primary" />
            <h2 className="text-sm font-bold">Imposta la tua posizione</h2>
          </div>
          <p className="text-sm text-text-secondary mb-4">
            Per vedere le carte disponibili nelle tue vicinanze, devi impostare la tua posizione.
          </p>
          <div className="space-y-2.5">
            <Button onClick={() => navigate('/profile/edit')} variant="outline" className="w-full">
              <Settings size={14} />
              Imposta nel profilo
            </Button>
            <Button onClick={handleUseLivePosition} className="w-full" isLoading={geoLoading}>
              <Navigation size={14} />
              Usa posizione attuale
            </Button>
          </div>
        </div>
      )}

      {/* Loading */}
      {isLoading && (
        <div className="flex justify-center py-16">
          <Loader2 size={28} className="animate-spin text-primary" />
        </div>
      )}

      {/* Carousel section — full width, dark bg inside component */}
      {!isLoading && !showLocationPrompt && topCards.length > 0 && (
        <div className="w-full mb-4 -mx-4 px-0" style={{ width: 'calc(100% + 2rem)' }}>
          <div className="max-w-lg mx-auto px-4 pb-2 flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Sparkles size={16} className="text-amber-400" />
              <h2 className="text-sm font-bold text-text">Carte cercate disponibili</h2>
            </div>
            <span className="text-xs text-text-muted">{topCards.length} risultati</span>
          </div>
          <CardCarousel cards={topCards} onCardClick={handleCardClick} />
        </div>
      )}

      {/* No matches */}
      {!isLoading && !showLocationPrompt && (hasProfileLocation || position) && topCards.length === 0 && (
        <div className="max-w-lg mx-auto"><EmptyState
          icon={Sparkles}
          title="Nessuna carta trovata"
          description="Non ci sono carte dalla tua wishlist disponibili nel raggio di ricerca. Prova ad aggiungere carte alla wishlist o ad aumentare il raggio."
          action={
            <div className="flex gap-2">
              <Button onClick={() => navigate('/wishlist')} size="sm" variant="outline">
                Wishlist
              </Button>
              <Button onClick={() => navigate('/explore')} size="sm">
                Esplora
              </Button>
            </div>
          }
        /></div>
      )}

      {/* Quick links */}
      <div className="max-w-lg mx-auto grid grid-cols-2 gap-3 mt-2">
        <button
          onClick={() => navigate('/explore')}
          className="bg-white rounded-2xl p-4 shadow-sm border border-border/50 text-left hover:bg-surface-dark transition"
        >
          <span className="text-2xl">🔍</span>
          <h3 className="text-sm font-bold mt-2">Esplora</h3>
          <p className="text-xs text-text-muted mt-0.5">Carte e utenti vicini</p>
        </button>
        <button
          onClick={() => navigate('/collection')}
          className="bg-white rounded-2xl p-4 shadow-sm border border-border/50 text-left hover:bg-surface-dark transition"
        >
          <span className="text-2xl">🃏</span>
          <h3 className="text-sm font-bold mt-2">Collezione</h3>
          <p className="text-xs text-text-muted mt-0.5">Le tue carte</p>
        </button>
        <button
          onClick={() => navigate('/wishlist')}
          className="bg-white rounded-2xl p-4 shadow-sm border border-border/50 text-left hover:bg-surface-dark transition"
        >
          <span className="text-2xl">❤️</span>
          <h3 className="text-sm font-bold mt-2">Wishlist</h3>
          <p className="text-xs text-text-muted mt-0.5">Carte che cerchi</p>
        </button>
        <button
          onClick={() => navigate('/events')}
          className="bg-white rounded-2xl p-4 shadow-sm border border-border/50 text-left hover:bg-surface-dark transition"
        >
          <span className="text-2xl">📅</span>
          <h3 className="text-sm font-bold mt-2">Eventi</h3>
          <p className="text-xs text-text-muted mt-0.5">Incontri e tornei</p>
        </button>
      </div>
    </div>
  );
}
