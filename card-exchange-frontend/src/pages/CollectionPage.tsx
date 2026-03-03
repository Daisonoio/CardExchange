import { useState, useEffect, useCallback } from 'react';
import { Plus, Library, Search, X } from 'lucide-react';
import { cards, scryfall } from '../api';
import { useAuth } from '../context/AuthContext';
import type { Card, ScryfallCard, CardCondition } from '../types';
import { CONDITION_LABELS } from '../types';
import CardItem from '../components/cards/CardItem';
import ScryfallSearch from '../components/cards/ScryfallSearch';
import Button from '../components/ui/Button';
import EmptyState from '../components/ui/EmptyState';
import CardSkeleton from '../components/ui/CardSkeleton';

export default function CollectionPage() {
  const { user } = useAuth();
  const [myCards, setMyCards] = useState<Card[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [showAddModal, setShowAddModal] = useState(false);
  const [selectedCard, setSelectedCard] = useState<ScryfallCard | null>(null);
  const [addForm, setAddForm] = useState({
    condition: 2 as CardCondition,
    quantity: 1,
    isAvailableForTrade: true,
    notes: '',
  });
  const [isSaving, setIsSaving] = useState(false);
  const [filterText, setFilterText] = useState('');
  const [filterTrade, setFilterTrade] = useState<'all' | 'trade' | 'keep'>('all');

  const loadCards = useCallback(async () => {
    if (!user) return;
    try {
      const { data } = await cards.getByUser(user.id);
      setMyCards(data);
    } catch (err) {
      console.error('Errore caricamento carte:', err);
    } finally {
      setIsLoading(false);
    }
  }, [user]);

  useEffect(() => { loadCards(); }, [loadCards]);

  const handleSelectScryfall = (card: ScryfallCard) => {
    setSelectedCard(card);
  };

  const handleAddCard = async () => {
    if (!selectedCard) return;
    setIsSaving(true);
    try {
      // Import Scryfall card to get a CardInfo ID
      const { data: importResult } = await scryfall.importCard(selectedCard.id);
      const cardInfoId = importResult.cardInfoId || importResult.id;

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

  const handleDelete = async (card: Card) => {
    if (!confirm(`Rimuovere "${card.cardInfo?.name}" dalla collezione?`)) return;
    try {
      await cards.delete(card.id);
      setMyCards((prev) => prev.filter((c) => c.id !== card.id));
    } catch (err) {
      console.error('Errore eliminazione:', err);
    }
  };

  const filtered = myCards.filter((c) => {
    const matchText = !filterText ||
      c.cardInfo?.name?.toLowerCase().includes(filterText.toLowerCase());
    const matchTrade = filterTrade === 'all' ||
      (filterTrade === 'trade' && c.isAvailableForTrade) ||
      (filterTrade === 'keep' && !c.isAvailableForTrade);
    return matchText && matchTrade;
  });

  const totalValue = myCards.reduce(
    (sum, c) => sum + (c.cardInfo?.priceEur || 0) * c.quantity, 0
  );

  return (
    <div>
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold text-text">La mia Collezione</h1>
          <p className="text-sm text-text-secondary">
            {myCards.length} carte &middot; Valore: {totalValue.toFixed(2)}
          </p>
        </div>
        <Button onClick={() => setShowAddModal(true)} size="sm">
          <Plus size={16} />
          Aggiungi
        </Button>
      </div>

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

      {/* Cards list */}
      {isLoading ? (
        <div className="space-y-3">
          {[...Array(5)].map((_, i) => <CardSkeleton key={i} />)}
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
        <div className="space-y-2">
          {filtered.map((card) => (
            <CardItem key={card.id} card={card} onDelete={handleDelete} />
          ))}
        </div>
      )}

      {/* Add Card Modal */}
      {showAddModal && (
        <div className="fixed inset-0 z-50 flex items-end md:items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="bg-white w-full max-w-lg rounded-t-3xl md:rounded-2xl max-h-[90vh] overflow-y-auto p-6">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-lg font-bold">Aggiungi Carta</h2>
              <button onClick={() => { setShowAddModal(false); setSelectedCard(null); }}>
                <X size={22} className="text-text-muted" />
              </button>
            </div>

            {!selectedCard ? (
              <div>
                <p className="text-sm text-text-secondary mb-3">
                  Cerca la carta da aggiungere alla tua collezione:
                </p>
                <ScryfallSearch onSelect={handleSelectScryfall} />
              </div>
            ) : (
              <div className="space-y-4">
                {/* Selected card preview */}
                <div className="flex gap-4 p-3 bg-surface-dark rounded-xl">
                  {(selectedCard.image_uris?.small || selectedCard.card_faces?.[0]?.image_uris?.small) && (
                    <img
                      src={selectedCard.image_uris?.small || selectedCard.card_faces?.[0]?.image_uris?.small}
                      alt={selectedCard.name}
                      className="w-16 h-22 rounded-lg object-cover"
                    />
                  )}
                  <div>
                    <p className="font-semibold text-sm">{selectedCard.name}</p>
                    <p className="text-xs text-text-secondary">{selectedCard.set_name}</p>
                    <p className="text-xs text-text-muted">{selectedCard.type_line}</p>
                    {selectedCard.prices?.eur && (
                      <p className="text-sm font-bold text-accent mt-1">{selectedCard.prices.eur}</p>
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
                  <label className="text-sm font-medium text-text-secondary block mb-1">Condizione</label>
                  <div className="grid grid-cols-2 gap-2">
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
                  <label className="text-sm font-medium text-text-secondary block mb-1">Quantita</label>
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
                  <label className="text-sm font-medium text-text-secondary block mb-1">Note (opzionale)</label>
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
          </div>
        </div>
      )}
    </div>
  );
}
