import { useState, useEffect, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ArrowLeft, Search, Library, MapPin } from 'lucide-react';
import { cards, users } from '../api';
import { useAuth } from '../context/AuthContext';
import type { Card, User } from '../types';
import CardItem from '../components/cards/CardItem';
import EmptyState from '../components/ui/EmptyState';
import CardSkeleton from '../components/ui/CardSkeleton';

export default function UserCollectionPage() {
  const { userId } = useParams<{ userId: string }>();
  const { user: currentUser } = useAuth();
  const navigate = useNavigate();

  const [owner, setOwner] = useState<User | null>(null);
  const [userCards, setUserCards] = useState<Card[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [filterText, setFilterText] = useState('');

  // If the user navigates to their own collection, redirect
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
        cards.getByUser(numericId),
        users.getProfile(numericId),
      ]);
      const allCards: Card[] = Array.isArray(cardsRes.data)
        ? cardsRes.data
        : (cardsRes.data as any).cards ?? [];
      // Only show cards available for trade
      setUserCards(allCards.filter((c) => c.isAvailableForTrade));
      setOwner(userRes.data);
    } catch (err) {
      console.error('Errore caricamento collezione utente:', err);
    } finally {
      setIsLoading(false);
    }
  }, [userId, numericId, isOwnCollection]);

  useEffect(() => { load(); }, [load]);

  const filtered = userCards.filter((c) => {
    if (!filterText) return true;
    return (c.cardName || c.cardInfo?.name || '')
      .toLowerCase()
      .includes(filterText.toLowerCase());
  });

  const totalValue = userCards.reduce(
    (sum, c) => sum + (c.estimatedValue || c.cardInfo?.priceEur || 0) * c.quantity, 0
  );

  if (isOwnCollection) return null;

  return (
    <div>
      {/* Header */}
      <div className="flex items-center gap-3 mb-4">
        <button
          onClick={() => navigate(-1)}
          className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center hover:bg-gray-200 transition"
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

      {/* Cards list */}
      {isLoading ? (
        <div className="space-y-3">
          {[...Array(5)].map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : filtered.length === 0 ? (
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
        <div className="space-y-2">
          {filtered.map((card) => (
            <CardItem key={card.id} card={card} />
          ))}
        </div>
      )}
    </div>
  );
}
