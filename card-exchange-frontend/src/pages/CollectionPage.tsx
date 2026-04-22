import { useState, useEffect, useCallback } from 'react';
import { Plus, Library, Search, TrendingUp } from 'lucide-react';
import { cards, priceTracking } from '../api';
import { useAuth } from '../context/AuthContext';
import { useGame } from '../context/GameContext';
import type { Card, GameCard, CardCondition, PriceSpike } from '../types';
import { CONDITION_LABELS } from '../types';
import CardGridItem from '../components/cards/CardGridItem';
import EditCardSheet from '../components/cards/EditCardSheet';
import GameCardSearch, { importGameCard } from '../components/cards/GameCardSearch';
import Button from '../components/ui/Button';
import EmptyState from '../components/ui/EmptyState';
import BottomSheet from '../components/ui/BottomSheet';

export default function CollectionPage() {
  const { user } = useAuth();
  const { selectedGameId, selectedGame } = useGame();
  const [myCards, setMyCards] = useState<Card[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [showAddModal, setShowAddModal] = useState(false);
  const [editCard, setEditCard] = useState<Card | null>(null);
  const [selectedCard, setSelectedCard] = useState<GameCard | null>(null);
  const [addForm, setAddForm] = useState({
    condition: 2 as CardCondition,
    quantity: 1,
    isAvailableForTrade: true,
    notes: '',
  });
  const [isSaving, setIsSaving] = useState(false);
  const [filterText, setFilterText] = useState('');
  const [filterTrade, setFilterTrade] = useState<'all' | 'trade' | 'keep'>('all');
  const [spikeCards, setSpikeCards] = useState<PriceSpike[]>([]);
  const [spikeCardIds, setSpikeCardIds] = useState<Set<number>>(new Set());
  const [showSpikesOnly, setShowSpikesOnly] = useState(false);

  const loadCards = useCallback(async () => {
    if (!user) return;
    setIsLoading(true);
    try {
      const { data } = await cards.getByUser(user.id, selectedGameId);
      setMyCards(Array.isArray(data) ? data : (data as any).cards ?? []);
    } catch (err) {
      console.error('Errore caricamento carte:', err);
    } finally {
      setIsLoading(false);
    }
  }, [user, selectedGameId]);

  const loadSpikes = useCallback(async () => {
    try {
      const { data } = await priceTracking.getSpikes(selectedGameId);
      const spikes = Array.isArray(data) ? data : [];
      setSpikeCards(spikes);
      setSpikeCardIds(new Set(spikes.map((s) => s.cardId)));
    } catch {
      // silently fail - spikes are non-essential
    }
  }, [selectedGameId]);

  useEffect(() => { loadCards(); loadSpikes(); }, [loadCards, loadSpikes]);

  const handleSelectCard = (card: GameCard) => {
    setSelectedCard(card);
  };

  const handleAddCard = async () => {
    if (!selectedCard) return;
    setIsSaving(true);
    try {
      const cardInfoId = await importGameCard(selectedGame?.name, selectedCard.externalId);

      await cards.create(user!.id, {
        cardInfoId,
        condition: addForm.condition,
        quantity: addForm.quantity,
        isAvailableForTrade: addForm.isAvailableForTrade,
        notes: addForm.notes || undefined,
      });

      setShowAddModal(false);
      setSelectedCard(null);
      setAddForm({ condition: 2, quantity: 1, isAvailableForTrade: true, notes: '' });
      loadCards();
    } catch (err) {
      console.error('Errore aggiunta carta:', err);
    } finally {
      setIsSaving(false);
    }
  };

  const handleCardDeleted = (card: Card) => {
    setMyCards((prev) => prev.filter((c) => c.id !== card.id));
  };

  const filtered = myCards.filter((c) => {
    const matchText = !filterText ||
      (c.cardName || c.cardInfo?.name || '').toLowerCase().includes(filterText.toLowerCase());
    const matchTrade = filterTrade === 'all' ||
      (filterTrade === 'trade' && c.isAvailableForTrade) ||
      (filterTrade === 'keep' && !c.isAvailableForTrade);
    const matchSpike = !showSpikesOnly || spikeCardIds.has(c.id);
    return matchText && matchTrade && matchSpike;
  });

  const totalValue = myCards.reduce(
    (sum, c) => sum + (c.estimatedValue || c.cardInfo?.priceEur || 0) * c.quantity, 0
  );

  return (
    <div>
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold text-text">La mia Collezione</h1>
          <p className="text-sm text-text-secondary">
            {myCards.length} carte &middot; Valore: €{totalValue.toFixed(2)}
          </p>
        </div>
        <Button onClick={() => setShowAddModal(true)} size="sm">
          <Plus size={16} />
          Aggiungi
        </Button>
      </div>

      {/* Spike alert banner */}
      {spikeCards.length > 0 && (
        <button
          onClick={() => setShowSpikesOnly(!showSpikesOnly)}
          className={`w-full mb-3 flex items-center gap-2 px-4 py-2.5 rounded-xl border-2 transition-all ${
            showSpikesOnly
              ? 'border-amber-400 bg-amber-50 text-amber-800'
              : 'border-amber-300 bg-amber-50/60 text-amber-700 hover:bg-amber-50'
          }`}
        >
          <TrendingUp size={16} className="text-amber-500" />
          <span className="text-sm font-medium flex-1 text-left">
            {spikeCards.length} {spikeCards.length === 1 ? 'carta sta' : 'carte stanno'} salendo di prezzo!
          </span>
          <span className={`text-xs px-2 py-0.5 rounded-full font-semibold ${
            showSpikesOnly ? 'bg-amber-200 text-amber-800' : 'bg-amber-100 text-amber-600'
          }`}>
            {showSpikesOnly ? 'Mostra tutte' : 'Mostra'}
          </span>
        </button>
      )}

      {/* Search & filters */}
      <div className="flex gap-2 mb-4">
        <div className="flex-1 relative">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
          <input
            type="text"
            placeholder="Filtra le tue carte..."
            value={filterText}
            onChange={(e) => setFilterText(e.target.value)}
            className="w-full pl-9 pr-4 py-2.5 rounded-xl border border-border bg-white text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
          />
        </div>
        <div className="flex rounded-xl border border-border overflow-hidden bg-white">
          {(['all', 'trade', 'keep'] as const).map((f) => (
            <button
              key={f}
              onClick={() => setFilterTrade(f)}
              className={`px-3 py-2 text-xs font-medium transition-colors ${
                filterTrade === f ? 'bg-primary text-white' : 'text-text-secondary hover:bg-surface-dark'
              }`}
            >
              {f === 'all' ? 'Tutte' : f === 'trade' ? 'Scambio' : 'Tengo'}
            </button>
          ))}
        </div>
      </div>

      {/* Cards grid */}
      {isLoading ? (
        <div className="grid grid-cols-3 ">
          {[...Array(9)].map((_, i) => (
            <div key={i} className="aspect-[6/7] bg-white rounded-xl animate-pulse" />
          ))}
        </div>
      ) : filtered.length === 0 ? (
        <EmptyState
          icon={Library}
          title={myCards.length === 0 ? 'Nessuna carta' : 'Nessun risultato'}
          description={myCards.length === 0
            ? 'Aggiungi la tua prima carta alla collezione'
            : 'Prova a modificare i filtri'}
          action={myCards.length === 0 ? (
            <Button onClick={() => setShowAddModal(true)} size="sm">
              <Plus size={16} /> Aggiungi carta
            </Button>
          ) : undefined}
        />
      ) : (
        <div className="grid grid-cols-3 sm:grid-cols-4 gap-2">
          {filtered.map((card) => (
            <CardGridItem
              key={card.id}
              card={card}
              onClick={setEditCard}
              hasPriceSpike={spikeCardIds.has(card.id)}
            />
          ))}
        </div>
      )}

      {/* Edit Card Sheet */}
      <EditCardSheet
        card={editCard}
        onClose={() => setEditCard(null)}
        onUpdated={loadCards}
        onDeleted={handleCardDeleted}
        spikeInfo={editCard ? spikeCards.find((s) => s.cardId === editCard.id) ?? null : null}
      />

      {/* Add Card Bottom Sheet */}
      <BottomSheet
        open={showAddModal}
        onClose={() => { setShowAddModal(false); setSelectedCard(null); }}
        title="Aggiungi Carta"
      >
        {!selectedCard ? (
          <div>
            <p className="text-sm text-text-secondary mb-3">
              Cerca la carta da aggiungere alla tua collezione:
            </p>
            <GameCardSearch onSelect={handleSelectCard} />
          </div>
        ) : (
          <div className="space-y-4">
            {/* Selected card preview */}
            <div className="flex gap-4 p-3 bg-surface-dark rounded-xl">
              {selectedCard.imageSmall && (
                <img
                  src={selectedCard.imageSmall}
                  alt={selectedCard.name}
                  className="w-16 h-22 rounded-lg object-cover"
                />
              )}
              <div>
                <p className="font-semibold text-sm">{selectedCard.name}</p>
                <p className="text-xs text-text-secondary">{selectedCard.setName}</p>
                {selectedCard.subtitle && <p className="text-xs text-text-muted">{selectedCard.subtitle}</p>}
                {selectedCard.priceEur && (
                  <p className="text-sm font-bold text-accent mt-1">{selectedCard.priceEur} EUR</p>
                )}
              </div>
            </div>

            <button
              onClick={() => setSelectedCard(null)}
              className="text-sm text-primary hover:underline"
            >
              Cambia carta
            </button>

            {/* Condition select */}
            <div>
              <label className="text-xs font-semibold text-text-secondary block mb-1.5">Condizione</label>
              <div className="grid grid-cols-2 gap-1.5">
                {([1,2,3,4,5,6,7,8] as CardCondition[]).map((c) => (
                  <button
                    key={c}
                    onClick={() => setAddForm({ ...addForm, condition: c })}
                    className={`px-3 py-2 rounded-xl text-xs font-medium border transition-colors ${
                      addForm.condition === c
                        ? 'border-primary bg-primary/10 text-primary'
                        : 'border-border text-text-secondary hover:bg-surface-dark'
                    }`}
                  >
                    {CONDITION_LABELS[c]}
                  </button>
                ))}
              </div>
            </div>

            {/* Quantity */}
            <div>
              <label className="text-xs font-semibold text-text-secondary block mb-1.5">Quantità</label>
              <div className="flex items-center gap-3">
                <button
                  onClick={() => setAddForm({ ...addForm, quantity: Math.max(1, addForm.quantity - 1) })}
                  className="w-10 h-10 rounded-xl border border-border flex items-center justify-center text-lg hover:bg-surface-dark"
                >
                  -
                </button>
                <span className="text-lg font-bold w-8 text-center">{addForm.quantity}</span>
                <button
                  onClick={() => setAddForm({ ...addForm, quantity: addForm.quantity + 1 })}
                  className="w-10 h-10 rounded-xl border border-border flex items-center justify-center text-lg hover:bg-surface-dark"
                >
                  +
                </button>
              </div>
            </div>

            {/* Trade toggle */}
            <div className="flex items-center justify-between p-3 bg-surface-dark rounded-xl">
              <div>
                <p className="text-sm font-medium">Disponibile per lo scambio</p>
                <p className="text-xs text-text-muted">Altri utenti potranno trovarla</p>
              </div>
              <button
                onClick={() => setAddForm({ ...addForm, isAvailableForTrade: !addForm.isAvailableForTrade })}
                className={`w-12 h-7 rounded-full transition-colors relative ${
                  addForm.isAvailableForTrade ? 'bg-primary' : 'bg-gray-300'
                }`}
              >
                <div className={`w-5 h-5 bg-white rounded-full absolute top-1 transition-transform ${
                  addForm.isAvailableForTrade ? 'translate-x-6' : 'translate-x-1'
                }`} />
              </button>
            </div>

            {/* Notes */}
            <div>
              <label className="text-xs font-semibold text-text-secondary block mb-1.5">Note (opzionale)</label>
              <textarea
                value={addForm.notes}
                onChange={(e) => setAddForm({ ...addForm, notes: e.target.value })}
                placeholder="Es: Foil, italiano, prima edizione..."
                className="w-full px-4 py-2.5 rounded-xl border border-border text-sm resize-none h-20 focus:outline-none focus:ring-2 focus:ring-primary/30"
              />
            </div>

            <Button onClick={handleAddCard} isLoading={isSaving} className="w-full" size="lg">
              Aggiungi alla Collezione
            </Button>
          </div>
        )}
      </BottomSheet>
    </div>
  );
}
