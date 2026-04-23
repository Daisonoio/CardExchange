import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { MapPin, Navigation, Loader2, Settings, Sparkles } from 'lucide-react';
import { wishlist } from '../api';
import { useAuth } from '../context/AuthContext';
import { useGame } from '../context/GameContext';
import { useGeolocation } from '../hooks/useGeolocation';
import CardCarousel from '../components/cards/CardCarousel';
import type { CarouselCard } from '../components/cards/CardCarousel';
import Button from '../components/ui/Button';
import EmptyState from '../components/ui/EmptyState';

export default function HomePage() {
  const { user } = useAuth();
  const { selectedGameId } = useGame();
  const navigate = useNavigate();
  const { position, isLoading: geoLoading, requestPosition } = useGeolocation();

  const [topCards, setTopCards] = useState<CarouselCard[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  const hasProfileLocation = !!(user?.location?.latitude && user?.location?.longitude);

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
  }, [user, position, hasProfileLocation, selectedGameId]);

  const handleCardClick = (card: CarouselCard) => {
    navigate(`/collection/${card.ownerId}`, {
      state: { preselectCardId: card.cardId, preselectCardInfoId: card.cardInfoId },
    });
  };

  if (!user) return null;

  const showLocationPrompt = !hasProfileLocation && !position && !isLoading;

  return (
    <div>
      {/* Hero banner */}
      <div
        className="rounded-2xl overflow-hidden mb-6 relative"
        style={{ background: 'linear-gradient(135deg, #1a3461 0%, #2a4d8f 100%)', minHeight: '140px' }}
      >
        <div className="p-5 pr-28">
          <p className="text-white/70 text-xs font-medium mb-1 uppercase tracking-wider">Benvenuto</p>
          <h1 className="text-white text-2xl font-bold leading-tight">
            Ciao, {user.firstName}!
          </h1>
    
        </div>
        {/* Decorative circles */}
        <div
          className="absolute -right-8 -top-8 w-36 h-36 rounded-full opacity-20"
          style={{ background: '#f5b800' }}
        />
        <div
          className="absolute right-8 top-10 w-20 h-20 rounded-full opacity-10"
          style={{ background: '#ffffff' }}
        />
      </div>

      {/* Location prompt */}
      {showLocationPrompt && (
        <div className="bg-white rounded-2xl p-5 shadow-sm border border-border/50 mb-4">
          <div className="flex items-center gap-2 mb-2">
            <MapPin size={16} style={{ color: '#1a3461' }} />
            <h2 className="text-sm font-bold text-text">Imposta la tua posizione</h2>
          </div>
          <p className="text-sm text-text-secondary mb-4">
            Per vedere le carte disponibili nelle tue vicinanze, devi impostare la tua posizione.
          </p>
          <div className="space-y-2.5">
            <Button onClick={() => navigate('/profile/edit')} variant="outline" className="w-full">
              <Settings size={14} />
              Imposta nel profilo
            </Button>
            <Button onClick={requestPosition} className="w-full" isLoading={geoLoading}>
              <Navigation size={14} />
              Usa posizione attuale
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
            <h2 className="text-base font-bold text-text">Nelle vicinanze</h2>
            <span className="text-xs text-text-muted">{topCards.length} risultati</span>
          </div>
          <div className="w-full -mx-4" style={{ width: 'calc(100% + 2rem)' }}>
            <CardCarousel cards={topCards} onCardClick={handleCardClick} />
          </div>
        </div>
      )}

      {/* No matches */}
      {!isLoading && !showLocationPrompt && (hasProfileLocation || position) && topCards.length === 0 && (
        <EmptyState
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
        />
      )}
    </div>
  );
}
