import { useState, useEffect, useCallback } from 'react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import { ArrowLeft, Search, Library, MapPin, ArrowLeftRight } from 'lucide-react';
import { cards, users, favorites } from '../api';
import { useAuth } from '../context/AuthContext';
import { useGame } from '../context/GameContext';
import type { Card, User } from '../types';
import CardGridItem from '../components/cards/CardGridItem';
import EditCardSheet from '../components/cards/EditCardSheet';
import EmptyState from '../components/ui/EmptyState';
import Button from '../components/ui/Button';

export default function UserCollectionPage() {
  const { userId } = useParams<{ userId: string }>();
  const { user: currentUser } = useAuth();
  const { selectedGameId } = useGame();
  const navigate = useNavigate();
  const location = useLocation();

  const [owner, setOwner] = useState<User | null>(null);
  const [userCards, setUserCards] = useState<Card[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [filterText, setFilterText] = useState('');
  const [selectedCards, setSelectedCards] = useState<Card[]>([]);
  const [selectionMode, setSelectionMode] = useState(true);

  // Photo viewer
  const [viewingCard, setViewingCard] = useState<Card | null>(null);

  // Favorites
  const [favoriteIds, setFavoriteIds] = useState<Set<number>>(new Set());

  const preselectCardId = (location.state as any)?.preselectCardId as number | undefined;
  const preselectCardInfoId = (location.state as any)?.preselectCardInfoId as number | undefined;

  const numericId = Number(userId);
  const isOwnCollection = currentUser?.id === numericId;

  useEffect(() => {
    if (isOwnCollection) {
      navigate('/collection', { replace: true });
      return;
    }
  }, [isOwnCollection, navigate]);

  const load = useCallback(async () => {
    if (!userId || isOwnCollection) return;
    setIsLoading(true);
    try {
      const [cardsRes, userRes] = await Promise.all([
        cards.getByUser(numericId, selectedGameId),
        users.getProfile(numericId),
      ]);
      const allCards: Card[] = Array.isArray(cardsRes.data)
        ? cardsRes.data
        : (cardsRes.data as any).cards ?? [];
      setUserCards(allCards.filter((c) => c.isAvailableForTrade));
      setOwner(userRes.data);

      // Load favorite IDs
      try {
        const { data: favData } = await favorites.getCardIds();
        const ids = favData?.cardIds ?? (favData as any)?.CardIds ?? [];
        setFavoriteIds(new Set(ids));
      } catch {
        // favorites may not be available
      }
    } catch (err) {
      console.error('Errore caricamento collezione utente:', err);
    } finally {
      setIsLoading(false);
    }
  }, [userId, numericId, isOwnCollection, selectedGameId]);

  useEffect(() => { load(); }, [load]);

  useEffect(() => {
    if (!selectionMode || userCards.length === 0) return;
    if (!preselectCardId && !preselectCardInfoId) return;
    const preselected = userCards.find((c) =>
      (preselectCardId != null && c.id === preselectCardId) ||
      (preselectCardInfoId != null && c.cardInfoId === preselectCardInfoId)
    );
    if (preselected) {
      setSelectedCards((prev) => (prev.some((c) => c.id === preselected.id) ? prev : [preselected]));
    }
  }, [selectionMode, userCards, preselectCardId, preselectCardInfoId]);

  const toggleCardSelection = (card: Card) => {
    setSelectedCards((prev) =>
      prev.find((c) => c.id === card.id)
        ? prev.filter((c) => c.id !== card.id)
        : [...prev, card]
    );
  };

  const handleGoToTradeRequest = () => {
    navigate(`/trades/new/${numericId}`, {
      state: { selectedCards },
    });
  };

  const handleFavoriteToggle = async (card: Card) => {
    const isFav = favoriteIds.has(card.id);
    try {
      if (isFav) {
        await favorites.remove(card.id);
        setFavoriteIds((prev) => { const s = new Set(prev); s.delete(card.id); return s; });
      } else {
        await favorites.add(card.id);
        setFavoriteIds((prev) => new Set(prev).add(card.id));
      }
    } catch (err) {
      console.error('Errore preferiti:', err);
    }
  };

  const handlePhotoClick = (card: Card) => {
    setViewingCard(card);
  };

  const filtered = userCards.filter((c) => {
    if (!filterText) return true;
    return (c.cardName || c.cardInfo?.name || '')
      .toLowerCase()
      .includes(filterText.toLowerCase());
  });

  // Sort: favorites first
  const sortedFiltered = [...filtered].sort((a, b) => {
    const aFav = favoriteIds.has(a.id) ? 0 : 1;
    const bFav = favoriteIds.has(b.id) ? 0 : 1;
    return aFav - bFav;
  });

  const totalValue = userCards.reduce(
    (sum, c) => sum + (c.estimatedValue || c.cardInfo?.priceEur || 0) * c.quantity, 0
  );

  if (isOwnCollection) return null;

  return (
    <div className="pb-20">
      {/* Header */}
      <div className="flex items-center gap-3 mb-4">
        <button
          onClick={() => navigate(-1)}
          className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center hover:bg-gray-200 transition shrink-0"
        >
          <ArrowLeft size={18} />
        </button>
        <div className="flex-1 min-w-0">
          <h1 className="text-xl font-bold text-text truncate">
            {owner ? `Collezione di ${owner.username}` : 'Collezione utente'}
          </h1>
          <p className="text-sm text-text-secondary">
            {userCards.length} carte disponibili per lo scambio
            {totalValue > 0 && <> &middot; Valore: €{totalValue.toFixed(2)}</>}
          </p>
        </div>
        {userCards.length > 0 && (
          <button
            onClick={() => {  if (selectionMode) setSelectedCards([]); }}
            className={`px-3 py-1.5 rounded-xl text-xs font-semibold transition-colors shrink-0 ${
              selectionMode
                ? 'bg-primary text-white'
                : 'bg-surface-dark text-text-secondary hover:bg-gray-200'
            }`}
          >

            {selectedCards.length!=0 ? 'Annulla ' : 'Seleziona'}
          </button>
        )}
      </div>

      {/* Owner info */}
      {owner?.location && (
        <div className="flex items-center gap-1.5 text-xs text-text-muted mb-4">
          <MapPin size={12} />
          <span>
            {[owner.location.city, owner.location.province, owner.location.country]
              .filter(Boolean)
              .join(', ')}
          </span>
        </div>
      )}

      {/* Search */}
      {userCards.length > 0 && (
        <div className="relative mb-4">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
          <input
            type="text"
            placeholder="Cerca nelle carte..."
            value={filterText}
            onChange={(e) => setFilterText(e.target.value)}
            className="w-full pl-9 pr-4 py-2.5 rounded-xl border border-border bg-white text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
          />
        </div>
      )}

      {/* Cards grid */}
      {isLoading ? (
        <div className="grid grid-cols-3 sm:grid-cols-4 md:grid-cols-6 lg:grid-cols-8 xl:grid-cols-10 gap-2">
          {[...Array(9)].map((_, i) => (
            <div key={i} className="aspect-[5/7] bg-white rounded-xl animate-pulse" />
          ))}
        </div>
      ) : sortedFiltered.length === 0 ? (
        <EmptyState
          icon={Library}
          title={userCards.length === 0 ? 'Nessuna carta disponibile' : 'Nessun risultato'}
          description={
            userCards.length === 0
              ? 'Questo utente non ha carte disponibili per lo scambio'
              : 'Prova a modificare la ricerca'
          }
        />
      ) : (
        <div className="grid grid-cols-3 sm:grid-cols-4 md:grid-cols-6 lg:grid-cols-8 xl:grid-cols-10 gap-2">
          {sortedFiltered.map((card) => {
            const isSelected = !!selectedCards.find((c) => c.id === card.id);
            return (
              <CardGridItem
                key={card.id}
                card={card}
                onClick={selectionMode ? () => toggleCardSelection(card) : () => handlePhotoClick(card)}
                selected={selectionMode && isSelected}
                onPhotoClick={handlePhotoClick}
                showFavorite
                isFavorite={favoriteIds.has(card.id)}
                onFavoriteToggle={handleFavoriteToggle}
              />
            );
          })}
        </div>
      )}

      {/* Photo / detail viewer (read-only) */}
      <EditCardSheet
        card={viewingCard}
        onClose={() => setViewingCard(null)}
        onUpdated={() => {}}
        readOnly
      />

      {/* Floating action bar */}
      {selectionMode && selectedCards.length > 0 && (
        <div className="fixed bottom-16 md:bottom-0 left-0 right-0 bg-white border-t border-border p-4 z-40 shadow-lg">
          <div className="flex items-center gap-3 max-w-lg mx-auto">
            <div className="flex-1 min-w-0">
              <p className="text-sm font-semibold">{selectedCards.length} carte selezionate</p>
              <p className="text-xs text-text-muted">
                {selectedCards.reduce((s, c) => s + (c.estimatedValue || 0), 0).toFixed(2)} EUR
              </p>
            </div>
            <Button onClick={handleGoToTradeRequest} size="lg">
              <ArrowLeftRight size={16} />
              Richiedi
            </Button>
          </div>
        </div>
      )}

      {/* Quick trade button when not in selection mode */}
      {!selectionMode && userCards.length > 0 && (
        <div className="fixed bottom-16 md:bottom-0 left-0 right-0 bg-white/90 backdrop-blur border-t border-border p-4 z-40">
          <Button
            onClick={() => setSelectionMode(true)}
            className="w-full max-w-lg mx-auto"
            size="lg"
          >
            <ArrowLeftRight size={16} />
            Seleziona carte per scambio
          </Button>
        </div>
      )}
    </div>
  );
}
