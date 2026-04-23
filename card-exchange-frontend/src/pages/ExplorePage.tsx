import { useState, useEffect, useCallback, lazy, Suspense, useRef } from 'react';
import { Compass, MapPin, Search, ArrowLeftRight, Loader2, Navigation, Map, Settings } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { cards, users, favorites } from '../api';
import { useAuth } from '../context/AuthContext';
import { useGame } from '../context/GameContext';
import type { Card, User } from '../types';
import { useGeolocation } from '../hooks/useGeolocation';
import CardItem from '../components/cards/CardItem';
import Button from '../components/ui/Button';
import EmptyState from '../components/ui/EmptyState';

const LocationPicker = lazy(() => import('../components/map/LocationPicker'));

type Tab = 'cards' | 'users' | 'zone';

export default function ExplorePage() {
  const { user: currentUser } = useAuth();
  const { selectedGameId } = useGame();
  const navigate = useNavigate();
  const [tab, setTab] = useState<Tab>('cards');
  const [availableCards, setAvailableCards] = useState<Card[]>([]);
  const [nearbyUsers, setNearbyUsers] = useState<User[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [radiusKm, setRadiusKm] = useState<number | null>(null);
  const { position, isLoading: geoLoading, requestPosition } = useGeolocation();

  // Zone search state
  const [zoneCoords, setZoneCoords] = useState<{ lat: number; lng: number } | null>(null);
  const [zoneRadius, setZoneRadius] = useState(20);
  const [zoneLabel, setZoneLabel] = useState('');
  const [zoneCards, setZoneCards] = useState<Card[]>([]);
  const [zoneLoading, setZoneLoading] = useState(false);
  const [zoneSearched, setZoneSearched] = useState(false);
  const [zoneSearchTerm, setZoneSearchTerm] = useState('');
  const zoneResultsRef = useRef<HTMLDivElement>(null);

  // Favorites
  const [favoriteIds, setFavoriteIds] = useState<Set<number>>(new Set());

  const hasProfileLocation = !!(currentUser?.location?.latitude && currentUser?.location?.longitude);
  const profileRadius = currentUser?.location?.maxDistanceKm ?? 50;

  // Initialize radius from profile setting
  useEffect(() => {
    if (radiusKm === null && currentUser) {
      setRadiusKm(profileRadius);
    }
  }, [currentUser, profileRadius, radiusKm]);

  const effectiveRadius = radiusKm ?? profileRadius;

  // Load favorite card IDs
  useEffect(() => {
    if (!currentUser) return;
    favorites.getCardIds()
      .then(({ data }) => {
        const ids = data?.cardIds ?? (data as any)?.CardIds ?? [];
        setFavoriteIds(new Set(ids));
      })
      .catch(() => {});
  }, [currentUser]);

  // Debounce search term
  useEffect(() => {
    const id = setTimeout(() => setDebouncedSearch(searchTerm), 350);
    return () => clearTimeout(id);
  }, [searchTerm]);

  // Load cards — always filtered by radius
  useEffect(() => {
    if (tab === 'zone') return;
    if (!currentUser) return;

    const load = async () => {
      setIsLoading(true);
      try {
        if (tab === 'cards') {
          if (hasProfileLocation) {
            const { data } = await cards.nearby(currentUser.id, effectiveRadius, undefined, selectedGameId, debouncedSearch || undefined);
            setAvailableCards(Array.isArray(data) ? data : (data as any).cards ?? []);
          } else if (position) {
            const { data } = await cards.nearby(currentUser.id, effectiveRadius, position, selectedGameId, debouncedSearch || undefined);
            setAvailableCards(Array.isArray(data) ? data : (data as any).cards ?? []);
          } else {
            setAvailableCards([]);
            setIsLoading(false);
            return;
          }
        } else if (tab === 'users') {
          if (hasProfileLocation && currentUser.location?.latitude && currentUser.location?.longitude) {
            const { data } = await users.nearby(currentUser.location.latitude, currentUser.location.longitude, effectiveRadius);
            setNearbyUsers(Array.isArray(data) ? data : (data as any).users ?? []);
          } else if (position) {
            const { data } = await users.nearby(position.latitude, position.longitude, effectiveRadius);
            setNearbyUsers(Array.isArray(data) ? data : (data as any).users ?? []);
          } else {
            setNearbyUsers([]);
            setIsLoading(false);
            return;
          }
        }
      } catch (err) {
        console.error('Errore caricamento:', err);
      } finally {
        setIsLoading(false);
      }
    };
    load();
  }, [tab, currentUser, hasProfileLocation, position, effectiveRadius, selectedGameId, debouncedSearch]);

  // Zone search
  const handleZoneLocationChange = useCallback((lat: number, lng: number, radius: number, label: string) => {
    setZoneCoords({ lat, lng });
    setZoneRadius(radius);
    setZoneLabel(label);
  }, []);

  const handleZoneSearch = async () => {
    if (!zoneCoords || !currentUser) return;
    setZoneLoading(true);
    setZoneSearched(true);
    try {
      const { data } = await cards.nearby(currentUser.id, zoneRadius, {
        latitude: zoneCoords.lat,
        longitude: zoneCoords.lng,
      }, selectedGameId);
      setZoneCards(Array.isArray(data) ? data : (data as any).cards ?? []);
    } catch (err) {
      console.error('Errore ricerca zona:', err);
      setZoneCards([]);
    } finally {
      setZoneLoading(false);
      setTimeout(() => {
        zoneResultsRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }, 50);
    }
  };

  // Local filter only while debounce is pending
  const filteredCardsUnsorted = searchTerm && searchTerm !== debouncedSearch
    ? availableCards.filter((c) =>
        (c.cardName || c.cardInfo?.name || '').toLowerCase().includes(searchTerm.toLowerCase()) ||
        (c.cardSetName || c.cardInfo?.cardSet?.name || '').toLowerCase().includes(searchTerm.toLowerCase())
      )
    : availableCards;

  // Favorites first
  const filteredCards = [...filteredCardsUnsorted].sort((a, b) => {
    const aFav = favoriteIds.has(a.id) ? 0 : 1;
    const bFav = favoriteIds.has(b.id) ? 0 : 1;
    return aFav - bFav;
  });

  const filteredZoneCards = zoneSearchTerm
    ? zoneCards.filter((c) =>
        (c.cardName || c.cardInfo?.name || '').toLowerCase().includes(zoneSearchTerm.toLowerCase()) ||
        (c.cardSetName || c.cardInfo?.cardSet?.name || '').toLowerCase().includes(zoneSearchTerm.toLowerCase())
      )
    : zoneCards;

  const handleCardClick = useCallback((card: Card) => {
    navigate(`/collection/${card.userId}`, {
      state: { preselectCardId: card.id, preselectCardInfoId: card.cardInfoId },
    });
  }, [navigate]);

  const hasLocation = hasProfileLocation || !!position;

  // Location prompt shown when user has no location
  const locationPrompt = (
    <div className="bg-white rounded-2xl p-5 shadow-sm border border-border/50 mb-4">
      <div className="flex items-center gap-2 mb-2">
        <MapPin size={16} className="text-primary" />
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
  );

  return (
    <div>
      <h1 className="text-xl font-bold text-text mb-1">Esplora</h1>
      <p className="text-sm text-text-secondary mb-4">Trova carte e collezionisti</p>

      {/* Tabs */}
      <div className="flex gap-1.5 mb-3">
        <button
          onClick={() => setTab('cards')}
          className={`flex-1 flex items-center justify-center gap-1 py-1.5 rounded-lg text-[11px] sm:text-xs font-semibold transition-colors ${
            tab === 'cards'
              ? 'bg-primary text-white shadow-sm'
              : 'bg-white text-text-secondary border border-border'
          }`}
        >
          <ArrowLeftRight size={12} />
          Carte
        </button>
        <button
          onClick={() => setTab('zone')}
          className={`flex-1 flex items-center justify-center gap-1 py-1.5 rounded-lg text-[11px] sm:text-xs font-semibold transition-colors ${
            tab === 'zone'
              ? 'bg-primary text-white shadow-sm'
              : 'bg-white text-text-secondary border border-border'
          }`}
        >
          <Map size={12} />
          Zona
        </button>
        <button
          onClick={() => setTab('users')}
          className={`flex-1 flex items-center justify-center gap-1 py-1.5 rounded-lg text-[11px] sm:text-xs font-semibold transition-colors ${
            tab === 'users'
              ? 'bg-primary text-white shadow-sm'
              : 'bg-white text-text-secondary border border-border'
          }`}
        >
          <MapPin size={12} />
          Utenti
        </button>
      </div>

      {/* ============ CARDS TAB ============ */}
      {tab === 'cards' && (
        <>
          {!hasLocation ? locationPrompt : (
            <>
              {/* Radius control */}
              <div className="bg-white rounded-2xl max-w-lg p-4 mb-4 border border-border/50">
                <div className="flex items-center justify-between mb-2">
                  <div className="flex items-center gap-2">
                    <Navigation size={16} className="text-primary" />
                    <span className="text-sm font-medium">Raggio di ricerca</span>
                  </div>
                  <span className="text-xs text-accent font-medium">
                    {hasProfileLocation ? 'Posizione profilo' : 'Posizione attuale'}
                  </span>
                </div>
                <div className="flex items-center gap-3">
                  <input
                    type="range"
                    min={5}
                    max={200}
                    step={5}
                    value={effectiveRadius}
                    onChange={(e) => setRadiusKm(Number(e.target.value))}
                    className="flex-1 accent-primary"
                  />
                  <span className="text-xs font-bold text-primary w-12 text-right">{effectiveRadius} km</span>
                </div>
              </div>

              <div className="relative mb-4">
                <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
                <input
                  type="text"
                  placeholder="Cerca tra le carte nelle vicinanze..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  className="w-full pl-9 pr-4 py-2.5 rounded-xl border border-border bg-white text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                />
              </div>

              {isLoading ? (
                <div className="flex justify-center py-12">
                  <Loader2 size={28} className="animate-spin text-primary" />
                </div>
              ) : filteredCards.length === 0 ? (
                <EmptyState
                  icon={Compass}
                  title="Nessuna carta trovata"
                  description={
                    searchTerm
                      ? `Nessuna carta "${searchTerm}" trovata entro ${effectiveRadius} km`
                      : `Nessuna carta disponibile entro ${effectiveRadius} km. Prova ad aumentare il raggio.`
                  }
                />
              ) : (
                <div className="space-y-2">
                  <p className="text-xs text-text-muted mb-2">
                    {filteredCards.length} carte disponibili entro {effectiveRadius} km
                  </p>
                  {filteredCards.map((card) => (
                    <CardItem key={card.id} card={card} showUser onClick={handleCardClick} />
                  ))}
                </div>
              )}
            </>
          )}
        </>
      )}

      {/* ============ ZONE SEARCH TAB ============ */}
      {tab === 'zone' && (
        <><div>
          <div className="bg-white rounded-2xl max-w-3xl mx-auto p-4 mb-4 border border-border/50">

            <Suspense fallback={
              <div className="flex justify-center py-12">
                <Loader2 size={28} className="animate-spin text-primary" />
              </div>
            }>
              <LocationPicker
                initialLat={currentUser?.location?.latitude ?? 45.0703}
                initialLng={currentUser?.location?.longitude ?? 7.6869}
                initialRadius={zoneRadius}
                onLocationChange={handleZoneLocationChange}
              />
            </Suspense>

            <Button
              onClick={handleZoneSearch}
              className="w-full mt-4"
              size="lg"
              isLoading={zoneLoading}
              disabled={!zoneCoords}
            >
              <Search size={16} />
              Cerca carte in questa zona
            </Button>
          </div>

          {/* Zone results */}
          {zoneSearched && (
            <>
              <div ref={zoneResultsRef} />
              {zoneCards.length > 0 && (
                <div className="max-w-sm relative mb-4">
                  <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
                  <input
                    type="text"
                    placeholder="Filtra risultati..."
                    value={zoneSearchTerm}
                    onChange={(e) => setZoneSearchTerm(e.target.value)}
                    className="w-full pl-9 pr-4 py-2.5 rounded-xl border border-border bg-white text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                  />
                </div>
              )}

              {zoneLoading ? (
                <div className="flex justify-center py-12">
                  <Loader2 size={28} className="animate-spin text-primary" />
                </div>
              ) : filteredZoneCards.length === 0 ? (
                <EmptyState
                  icon={Map}
                  title="Nessuna carta trovata"
                  description={`Nessuna carta disponibile entro ${zoneRadius} km${zoneLabel ? ` da ${zoneLabel}` : ''}. Prova ad aumentare il raggio.`}
                />
              ) : (
                <div className="space-y-2">
                  <p className="text-xs text-text-muted mb-2">
                    {filteredZoneCards.length} carte trovate entro {zoneRadius} km
                    {zoneLabel && ` da ${zoneLabel}`}
                  </p>
                  {filteredZoneCards.map((card) => (
                    <CardItem key={card.id} card={card} showUser onClick={handleCardClick} />
                  ))}
                </div>
              )}
            </>
          )}
          </div>
        </>
      )}

      {/* ============ USERS TAB ============ */}
      {tab === 'users' && (
        <>
          {!hasLocation ? locationPrompt : isLoading ? (
            <div className="flex justify-center py-12">
              <Loader2 size={28} className="animate-spin text-primary" />
            </div>
          ) : nearbyUsers.length === 0 ? (
            <EmptyState
              icon={MapPin}
              title="Nessun utente trovato"
              description={`Nessun collezionista trovato entro ${effectiveRadius} km. Prova ad aumentare il raggio.`}
            />
          ) : (
            <div className="space-y-2">
              <p className="text-xs text-text-muted mb-2">
                {nearbyUsers.length} utenti entro {effectiveRadius} km
              </p>
              {nearbyUsers.map((user) => (
                <div key={user.id} className="bg-white rounded-2xl p-4 shadow-sm border border-border/50">
                  <div className="flex items-center gap-3">
                    <div className="w-12 h-12 rounded-full bg-primary/10 flex items-center justify-center shrink-0">
                      {user.avatarUrl ? (
                        <img src={user.avatarUrl} alt={user.username} className="w-12 h-12 rounded-full object-cover" />
                      ) : (
                        <span className="text-lg font-bold text-primary">
                          {user.username[0].toUpperCase()}
                        </span>
                      )}
                    </div>
                    <div className="min-w-0 flex-1">
                      <h3 className="text-sm font-semibold truncate">@{user.username}</h3>
                      <p className="text-xs text-text-secondary">
                        {user.location?.city && `${user.location.city}, ${user.location.country}`}
                      </p>
                      <div className="flex items-center gap-3 mt-1">
                        <span className="text-xs text-text-muted">
                          {user.totalTradesCompleted ?? 0} scambi
                        </span>
                        <span className="text-xs text-secondary font-medium">
                          {(user.reputationScore ?? 0).toFixed(1)} rep
                        </span>
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}
