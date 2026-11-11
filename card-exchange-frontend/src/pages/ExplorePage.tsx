import { useState, useEffect, useCallback, lazy, Suspense, useRef, Fragment } from 'react';
import { Compass, MapPin, Search, ArrowLeftRight, Loader2, Navigation, Map, Settings, SlidersHorizontal, ChevronRight } from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { cards, users, favorites } from '../api';
import { useAuth } from '../context/AuthContext';
import { useGame } from '../context/GameContext';
import type { Card, User } from '../types';
import { useGeolocation } from '../hooks/useGeolocation';
import CardItem from '../components/cards/CardItem';
import SponsoredSlotCard from '../components/promotions/SponsoredSlotCard';
import { useEntitlements } from '../hooks/useEntitlements';
import Button from '../components/ui/Button';
import EmptyState from '../components/ui/EmptyState';
import EmptyStateLink from '../components/ui/EmptyStateLink';
import { useSearchRadius } from '../hooks/useSearchRadius';
import RadiusSlider from '../components/ui/RadiusSlider';

const LocationPicker = lazy(() => import('../components/map/LocationPicker'));

type Tab = 'cards' | 'users' | 'zone';

export default function ExplorePage() {
  const { t } = useTranslation('explore');
  const { user: currentUser } = useAuth();
  const { selectedGameId } = useGame();
  const { paidPlans, has } = useEntitlements();
  const navigate = useNavigate();
  const [tab, setTab] = useState<Tab>('cards');
  const [availableCards, setAvailableCards] = useState<Card[]>([]);
  const [nearbyUsers, setNearbyUsers] = useState<User[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  // Raggio condiviso con le altre sezioni (Matchmaking ecc.), salvato tra una visita e l'altra
  const [effectiveRadius, setRadiusKm] = useSearchRadius();
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
  // Il cursore aggiorna subito il numero; la ricerca parte quando ci si ferma (niente richieste a ogni passo)
  const [queryRadius, setQueryRadius] = useState(effectiveRadius);
  useEffect(() => {
    const id = setTimeout(() => setQueryRadius(effectiveRadius), 300);
    return () => clearTimeout(id);
  }, [effectiveRadius]);

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
            const { data } = await cards.nearby(currentUser.id, queryRadius, undefined, selectedGameId, debouncedSearch || undefined);
            setAvailableCards(Array.isArray(data) ? data : (data as any).cards ?? []);
          } else if (position) {
            const { data } = await cards.nearby(currentUser.id, queryRadius, position, selectedGameId, debouncedSearch || undefined);
            setAvailableCards(Array.isArray(data) ? data : (data as any).cards ?? []);
          } else {
            setAvailableCards([]);
            setIsLoading(false);
            return;
          }
        } else if (tab === 'users') {
          if (hasProfileLocation && currentUser.location?.latitude && currentUser.location?.longitude) {
            const { data } = await users.nearby(currentUser.location.latitude, currentUser.location.longitude, queryRadius);
            setNearbyUsers(Array.isArray(data) ? data : (data as any).users ?? []);
          } else if (position) {
            const { data } = await users.nearby(position.latitude, position.longitude, queryRadius);
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
  }, [tab, currentUser, hasProfileLocation, position, queryRadius, selectedGameId, debouncedSearch]);

  // Zone search
  // Il raggio della zona è quello condiviso con Carte e Utenti
  const handleZoneLocationChange = useCallback((lat: number, lng: number, _radius: number, label: string) => {
    setZoneCoords({ lat, lng });
    setZoneLabel(label);
  }, []);

  const handleZoneSearch = useCallback(async (scroll = true) => {
    if (!zoneCoords || !currentUser) return;
    setZoneLoading(true);
    setZoneSearched(true);
    setZoneRadius(queryRadius);
    try {
      const { data } = await cards.nearby(currentUser.id, queryRadius, {
        latitude: zoneCoords.lat,
        longitude: zoneCoords.lng,
      }, selectedGameId);
      setZoneCards(Array.isArray(data) ? data : (data as any).cards ?? []);
    } catch (err) {
      console.error('Errore ricerca zona:', err);
      setZoneCards([]);
    } finally {
      setZoneLoading(false);
      if (scroll) {
        setTimeout(() => {
          zoneResultsRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }, 50);
      }
    }
  }, [zoneCoords, currentUser, queryRadius, selectedGameId]);

  // Cambiando la distanza dopo una ricerca di zona, i risultati si aggiornano da soli
  const zoneSearchRef = useRef(handleZoneSearch);
  useEffect(() => { zoneSearchRef.current = handleZoneSearch; }, [handleZoneSearch]);
  useEffect(() => {
    if (tab === 'zone' && zoneSearched) zoneSearchRef.current(false);
  }, [queryRadius]); // eslint-disable-line react-hooks/exhaustive-deps

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

  // Selettore della distanza condiviso da Carte, Zona e Utenti: il valore resta lo stesso cambiando sezione
  const radiusControl = (source: string) => (
    <div className="bg-white rounded-2xl max-w-lg p-4 mb-4 border border-border/50">
      <div className="flex items-center justify-between gap-2 mb-2">
        <label htmlFor="explore-radius" className="flex items-center gap-2 text-sm font-medium">
          <Navigation size={16} className="text-primary" />
          {t('cards.radius')}
        </label>
        <span className="text-xs text-accent font-medium truncate">{source}</span>
      </div>
      <RadiusSlider id="explore-radius" value={effectiveRadius} onChange={setRadiusKm} />
    </div>
  );

  // Location prompt shown when user has no location
  const locationPrompt = (
    <div className="bg-white rounded-2xl p-5 shadow-sm border border-border/50 mb-4">
      <div className="flex items-center gap-2 mb-2">
        <MapPin size={16} className="text-primary" />
        <h2 className="text-sm font-bold text-text">{t('locationPrompt.title')}</h2>
      </div>
      <p className="text-sm text-text-secondary mb-4">
        {t('locationPrompt.description')}
      </p>
      <div className="space-y-2.5">
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
  );

  return (
    <div>
      <div className="flex items-start justify-between gap-2 mb-4">
        <div>
          <h1 className="text-xl font-bold text-text mb-1">{t('title')}</h1>
          <p className="text-sm text-text-secondary">{t('subtitle')}</p>
        </div>
        {(paidPlans || has('AdvancedSearch')) && (
        <button
          onClick={() => navigate('/search')}
          className="shrink-0 inline-flex items-center gap-1 px-3 py-1.5 rounded-lg border border-border bg-white text-xs font-semibold text-primary"
        >
          <SlidersHorizontal size={13} /> {t('advancedSearch')}
        </button>
        )}
      </div>

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
          {t('tabs.cards')}
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
          {t('tabs.zone')}
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
          {t('tabs.users')}
        </button>
      </div>

      {/* ============ CARDS TAB ============ */}
      {tab === 'cards' && (
        <>
          {!hasLocation ? locationPrompt : (
            <>
              {radiusControl(hasProfileLocation ? t('cards.profileLocation') : t('cards.currentLocation'))}

              <div className="relative mb-4">
                <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
                <input
                  type="text"
                  placeholder={t('cards.searchPlaceholder')}
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
                  title={t('cards.emptyTitle')}
                  description={
                    searchTerm
                      ? t('cards.emptySearch', { term: searchTerm, radius: effectiveRadius })
                      : t('cards.emptyDescription', { radius: effectiveRadius })
                  }
                  action={!searchTerm && <EmptyStateLink to="/profile" label={t('emptyInvite')} />}
                />
              ) : (
                <div className="space-y-2">
                  <p className="text-xs text-text-muted mb-2">
                    {t('cards.count', { count: filteredCards.length, radius: effectiveRadius })}
                  </p>
                  {filteredCards.map((card, i) => (
                    <Fragment key={card.id}>
                      <CardItem card={card} showUser onClick={handleCardClick} />
                      {i === 7 && <SponsoredSlotCard placement="ExploreCards" gameId={selectedGameId} />}
                    </Fragment>
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
          {radiusControl(zoneLabel ? t('zone.center', { place: zoneLabel }) : hasProfileLocation ? t('cards.profileLocation') : t('zone.pickOnMap'))}
          <div className="bg-white rounded-2xl max-w-3xl mx-auto p-4 mb-4 border border-border/50">

            <Suspense fallback={
              <div className="flex justify-center py-12">
                <Loader2 size={28} className="animate-spin text-primary" />
              </div>
            }>
              <LocationPicker
                initialLat={currentUser?.location?.latitude ?? 45.0703}
                initialLng={currentUser?.location?.longitude ?? 7.6869}
                radius={effectiveRadius}
                showRadiusControl={false}
                onLocationChange={handleZoneLocationChange}
              />
            </Suspense>

            <Button
              onClick={() => handleZoneSearch()}
              className="w-full mt-4"
              size="lg"
              isLoading={zoneLoading}
              disabled={!zoneCoords}
            >
              <Search size={16} />
              {t('zone.search')}
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
                    placeholder={t('zone.filterPlaceholder')}
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
                  title={t('cards.emptyTitle')}
                  description={zoneLabel
                    ? t('zone.emptyDescriptionFrom', { radius: zoneRadius, place: zoneLabel })
                    : t('zone.emptyDescription', { radius: zoneRadius })}
                  action={<EmptyStateLink to="/profile" label={t('emptyInvite')} />}
                />
              ) : (
                <div className="space-y-2">
                  <p className="text-xs text-text-muted mb-2">
                    {t('zone.count', { count: filteredZoneCards.length, radius: zoneRadius })}
                    {zoneLabel && t('zone.fromPlace', { place: zoneLabel })}
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
          {hasLocation && radiusControl(hasProfileLocation ? t('cards.profileLocation') : t('cards.currentLocation'))}
          {!hasLocation ? locationPrompt : isLoading ? (
            <div className="flex justify-center py-12">
              <Loader2 size={28} className="animate-spin text-primary" />
            </div>
          ) : nearbyUsers.length === 0 ? (
            <EmptyState
              icon={MapPin}
              title={t('users.emptyTitle')}
              description={t('users.emptyDescription', { radius: effectiveRadius })}
              action={<EmptyStateLink to="/profile" label={t('emptyInvite')} />}
            />
          ) : (
            <div className="space-y-2">
              <p className="text-xs text-text-muted mb-2">
                {t('users.count', { count: nearbyUsers.length, radius: effectiveRadius })}
              </p>
              {nearbyUsers.map((user) => (
                <Link
                  key={user.id}
                  to={`/collection/${user.id}`}
                  aria-label={t('users.openCollection', { username: user.username })}
                  className="block bg-white rounded-2xl p-4 shadow-sm border border-border/50 hover:border-primary/30 hover:shadow-md active:scale-[0.99] transition-all focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary"
                >
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
                          {t('users.trades', { count: user.totalTradesCompleted ?? 0 })}
                        </span>
                        <span className="text-xs text-secondary font-medium">
                          {(user.reputationScore ?? 0).toFixed(1)} rep
                        </span>
                      </div>
                    </div>
                    <ChevronRight size={18} className="text-text-muted shrink-0" aria-hidden="true" />
                  </div>
                </Link>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}
