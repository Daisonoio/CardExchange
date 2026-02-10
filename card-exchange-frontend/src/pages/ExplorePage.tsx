import { useState, useEffect } from 'react';
import { Compass, MapPin, Search, ArrowLeftRight, Loader2, Navigation } from 'lucide-react';
import { cards, users } from '../api';
import type { Card, User } from '../types';
import { useGeolocation } from '../hooks/useGeolocation';
import CardItem from '../components/cards/CardItem';
import Button from '../components/ui/Button';
import EmptyState from '../components/ui/EmptyState';

type Tab = 'cards' | 'users';

export default function ExplorePage() {
  const [tab, setTab] = useState<Tab>('cards');
  const [availableCards, setAvailableCards] = useState<Card[]>([]);
  const [nearbyUsers, setNearbyUsers] = useState<User[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [radiusKm, setRadiusKm] = useState(50);
  const [useLocation, setUseLocation] = useState(false);
  const { position, error: geoError, isLoading: geoLoading, requestPosition } = useGeolocation();

  // Load available cards
  useEffect(() => {
    const load = async () => {
      setIsLoading(true);
      try {
        if (tab === 'cards') {
          if (useLocation && position) {
            const { data } = await cards.nearby(position.latitude, position.longitude, radiusKm);
            setAvailableCards(data);
          } else {
            const { data } = await cards.getAll();
            setAvailableCards(data);
          }
        } else {
          if (position) {
            const { data } = await users.nearby(position.latitude, position.longitude, radiusKm);
            setNearbyUsers(data);
          }
        }
      } catch (err) {
        console.error('Errore caricamento:', err);
      } finally {
        setIsLoading(false);
      }
    };
    load();
  }, [tab, useLocation, position, radiusKm]);

  const handleEnableLocation = () => {
    requestPosition();
    setUseLocation(true);
  };

  const filteredCards = searchTerm
    ? availableCards.filter((c) =>
        c.cardInfo?.name?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        c.cardInfo?.cardSet?.name?.toLowerCase().includes(searchTerm.toLowerCase())
      )
    : availableCards;

  return (
    <div>
      <h1 className="text-xl font-bold text-text mb-1">Esplora</h1>
      <p className="text-sm text-text-secondary mb-4">Trova carte e collezionisti</p>

      {/* Tabs */}
      <div className="flex gap-2 mb-4">
        <button
          onClick={() => setTab('cards')}
          className={`flex-1 flex items-center justify-center gap-2 py-3 rounded-xl text-sm font-semibold transition-colors ${
            tab === 'cards'
              ? 'bg-primary text-white shadow-sm'
              : 'bg-white text-text-secondary border border-border'
          }`}
        >
          <ArrowLeftRight size={16} />
          Carte
        </button>
        <button
          onClick={() => setTab('users')}
          className={`flex-1 flex items-center justify-center gap-2 py-3 rounded-xl text-sm font-semibold transition-colors ${
            tab === 'users'
              ? 'bg-primary text-white shadow-sm'
              : 'bg-white text-text-secondary border border-border'
          }`}
        >
          <MapPin size={16} />
          Utenti vicini
        </button>
      </div>

      {/* Location bar */}
      <div className="bg-white rounded-2xl p-4 mb-4 border border-border/50">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Navigation size={16} className="text-primary" />
            <span className="text-sm font-medium">Geolocalizzazione</span>
          </div>
          {!position ? (
            <Button onClick={handleEnableLocation} size="sm" variant="outline" isLoading={geoLoading}>
              <MapPin size={14} />
              Attiva
            </Button>
          ) : (
            <span className="text-xs text-accent font-medium">Attiva</span>
          )}
        </div>

        {geoError && (
          <p className="text-xs text-danger mt-2">{geoError}</p>
        )}

        {position && (
          <div className="mt-3 flex items-center gap-3">
            <label className="text-xs text-text-secondary">Raggio:</label>
            <input
              type="range"
              min={5}
              max={200}
              step={5}
              value={radiusKm}
              onChange={(e) => setRadiusKm(Number(e.target.value))}
              className="flex-1 accent-primary"
            />
            <span className="text-xs font-bold text-primary w-12 text-right">{radiusKm} km</span>
          </div>
        )}
      </div>

      {/* Cards tab */}
      {tab === 'cards' && (
        <>
          <div className="relative mb-4">
            <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
            <input
              type="text"
              placeholder="Cerca tra le carte disponibili..."
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
              title="Nessuna carta disponibile"
              description={useLocation
                ? `Nessuna carta trovata entro ${radiusKm} km`
                : 'Non ci sono carte disponibili per lo scambio al momento'}
            />
          ) : (
            <div className="space-y-2">
              <p className="text-xs text-text-muted mb-2">
                {filteredCards.length} carte disponibili
                {useLocation && position && ` entro ${radiusKm} km`}
              </p>
              {filteredCards.map((card) => (
                <CardItem key={card.id} card={card} showUser />
              ))}
            </div>
          )}
        </>
      )}

      {/* Users tab */}
      {tab === 'users' && (
        <>
          {!position ? (
            <EmptyState
              icon={MapPin}
              title="Attiva la geolocalizzazione"
              description="Per trovare collezionisti vicino a te, attiva la posizione"
              action={
                <Button onClick={handleEnableLocation} size="sm" isLoading={geoLoading}>
                  <MapPin size={14} /> Attiva posizione
                </Button>
              }
            />
          ) : isLoading ? (
            <div className="flex justify-center py-12">
              <Loader2 size={28} className="animate-spin text-primary" />
            </div>
          ) : nearbyUsers.length === 0 ? (
            <EmptyState
              icon={MapPin}
              title="Nessun utente trovato"
              description={`Nessun collezionista trovato entro ${radiusKm} km. Prova ad aumentare il raggio.`}
            />
          ) : (
            <div className="space-y-2">
              <p className="text-xs text-text-muted mb-2">
                {nearbyUsers.length} utenti entro {radiusKm} km
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
                          {user.totalTradesCompleted} scambi
                        </span>
                        <span className="text-xs text-secondary font-medium">
                          {user.reputationScore.toFixed(1)} rep
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
