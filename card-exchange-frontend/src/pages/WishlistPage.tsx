import { useState, useEffect, useCallback } from 'react';
import { Plus, Heart, X, Star, ChevronDown } from 'lucide-react';
import { wishlist, scryfall } from '../api';
import type { WishlistItem, ScryfallCard, CardCondition } from '../types';
import { CONDITION_LABELS } from '../types';
import ScryfallSearch from '../components/cards/ScryfallSearch';
import Button from '../components/ui/Button';
import EmptyState from '../components/ui/EmptyState';

const PRIORITY_LABELS = { 1: 'Alta', 2: 'Media', 3: 'Bassa' } as const;
const PRIORITY_COLORS = {
  1: 'bg-red-50 text-red-600',
  2: 'bg-amber-50 text-amber-600',
  3: 'bg-blue-50 text-blue-600',
} as const;

export default function WishlistPage() {
  const [items, setItems] = useState<WishlistItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [showAddModal, setShowAddModal] = useState(false);
  const [selectedCard, setSelectedCard] = useState<ScryfallCard | null>(null);
  const [addForm, setAddForm] = useState({
    priority: 2 as 1 | 2 | 3,
    preferredCondition: undefined as CardCondition | undefined,
    maxPrice: undefined as number | undefined,
    notes: '',
  });
  const [isSaving, setIsSaving] = useState(false);

  const loadItems = useCallback(async () => {
    try {
      const { data } = await wishlist.getMine();
      setItems(data);
    } catch (err) {
      console.error('Errore caricamento wishlist:', err);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => { loadItems(); }, [loadItems]);

  const handleAdd = async () => {
    if (!selectedCard) return;
    setIsSaving(true);
    try {
      const { data: importResult } = await scryfall.importCard(selectedCard.id);
      const cardInfoId = importResult.cardInfoId || importResult.id;

      await wishlist.create({
        cardInfoId,
        priority: addForm.priority,
        preferredCondition: addForm.preferredCondition,
        maxPrice: addForm.maxPrice,
        notes: addForm.notes || undefined,
      });

      setShowAddModal(false);
      setSelectedCard(null);
      setAddForm({ priority: 2, preferredCondition: undefined, maxPrice: undefined, notes: '' });
      loadItems();
    } catch (err) {
      console.error('Errore aggiunta alla wishlist:', err);
    } finally {
      setIsSaving(false);
    }
  };

  const handleDelete = async (item: WishlistItem) => {
    try {
      await wishlist.delete(item.id);
      setItems((prev) => prev.filter((i) => i.id !== item.id));
    } catch (err) {
      console.error('Errore eliminazione:', err);
    }
  };

  const grouped = {
    1: items.filter((i) => i.priority === 1),
    2: items.filter((i) => i.priority === 2),
    3: items.filter((i) => i.priority === 3),
  };

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold text-text">Le mie Ricerche</h1>
          <p className="text-sm text-text-secondary">{items.length} carte cercate</p>
        </div>
        <Button onClick={() => setShowAddModal(true)} size="sm">
          <Plus size={16} />
          Aggiungi
        </Button>
      </div>

      {isLoading ? (
        <div className="space-y-3">
          {[...Array(3)].map((_, i) => (
            <div key={i} className="bg-white rounded-2xl p-4 animate-pulse h-20" />
          ))}
        </div>
      ) : items.length === 0 ? (
        <EmptyState
          icon={Heart}
          title="Nessuna carta cercata"
          description="Aggiungi le carte che stai cercando per trovare scambi"
          action={
            <Button onClick={() => setShowAddModal(true)} size="sm">
              <Plus size={16} /> Cerca carta
            </Button>
          }
        />
      ) : (
        <div className="space-y-6">
          {([1, 2, 3] as const).map((priority) =>
            grouped[priority].length > 0 ? (
              <div key={priority}>
                <div className="flex items-center gap-2 mb-2">
                  <span className={`px-2.5 py-0.5 rounded-full text-xs font-semibold ${PRIORITY_COLORS[priority]}`}>
                    {PRIORITY_LABELS[priority]}
                  </span>
                  <span className="text-xs text-text-muted">{grouped[priority].length} carte</span>
                </div>
                <div className="space-y-2">
                  {grouped[priority].map((item) => (
                    <div key={item.id} className="bg-white rounded-2xl p-3 shadow-sm border border-border/50">
                      <div className="flex gap-3">
                        <div className="w-14 shrink-0">
                          {item.cardInfo?.imageSmall ? (
                            <img
                              src={item.cardInfo.imageSmall}
                              alt={item.cardInfo.name}
                              className="w-14 h-20 rounded-lg object-cover"
                              loading="lazy"
                            />
                          ) : (
                            <div className="w-14 h-20 rounded-lg bg-primary/10 flex items-center justify-center">
                              <Heart size={20} className="text-primary" />
                            </div>
                          )}
                        </div>
                        <div className="flex-1 min-w-0">
                          <div className="flex items-start justify-between">
                            <div className="min-w-0">
                              <h3 className="text-sm font-semibold truncate">{item.cardInfo?.name}</h3>
                              <p className="text-xs text-text-secondary truncate">
                                {item.cardInfo?.cardSet?.name}
                              </p>
                            </div>
                            <button
                              onClick={() => handleDelete(item)}
                              className="p-1 rounded-lg hover:bg-red-50 text-text-muted hover:text-danger"
                            >
                              <X size={16} />
                            </button>
                          </div>
                          <div className="flex items-center gap-2 mt-1.5 flex-wrap">
                            {item.preferredCondition && (
                              <span className="text-xs text-text-muted">
                                Min: {CONDITION_LABELS[item.preferredCondition]}
                              </span>
                            )}
                            {item.maxPrice && (
                              <span className="text-xs font-medium text-accent">
                                Max {item.maxPrice.toFixed(2)}
                              </span>
                            )}
                            {item.availableMatchesCount != null && item.availableMatchesCount > 0 && (
                              <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-green-50 text-green-600">
                                {item.availableMatchesCount} disponibili
                              </span>
                            )}
                          </div>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            ) : null
          )}
        </div>
      )}

      {/* Add Modal */}
      {showAddModal && (
        <div className="fixed inset-0 z-50 flex items-end md:items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="bg-white w-full max-w-lg rounded-t-3xl md:rounded-2xl max-h-[90vh] overflow-y-auto p-6">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-lg font-bold">Aggiungi alla Ricerca</h2>
              <button onClick={() => { setShowAddModal(false); setSelectedCard(null); }}>
                <X size={22} className="text-text-muted" />
              </button>
            </div>

            {!selectedCard ? (
              <div>
                <p className="text-sm text-text-secondary mb-3">
                  Quale carta stai cercando?
                </p>
                <ScryfallSearch onSelect={setSelectedCard} placeholder="Cerca la carta desiderata..." />
              </div>
            ) : (
              <div className="space-y-4">
                <div className="flex gap-4 p-3 bg-surface-dark rounded-xl">
                  {selectedCard.image_uris?.small && (
                    <img
                      src={selectedCard.image_uris.small}
                      alt={selectedCard.name}
                      className="w-14 h-20 rounded-lg object-cover"
                    />
                  )}
                  <div>
                    <p className="font-semibold text-sm">{selectedCard.name}</p>
                    <p className="text-xs text-text-secondary">{selectedCard.set_name}</p>
                    {selectedCard.prices?.eur && (
                      <p className="text-sm font-bold text-accent mt-1">{selectedCard.prices.eur}</p>
                    )}
                  </div>
                </div>

                <button onClick={() => setSelectedCard(null)} className="text-sm text-primary hover:underline">
                  Cambia carta
                </button>

                {/* Priority */}
                <div>
                  <label className="text-sm font-medium text-text-secondary block mb-1">Priorita</label>
                  <div className="flex gap-2">
                    {([1, 2, 3] as const).map((p) => (
                      <button
                        key={p}
                        onClick={() => setAddForm({ ...addForm, priority: p })}
                        className={`flex-1 px-3 py-2 rounded-xl text-xs font-medium border transition-colors ${
                          addForm.priority === p
                            ? 'border-primary bg-primary/10 text-primary'
                            : 'border-border text-text-secondary hover:bg-surface-dark'
                        }`}
                      >
                        {PRIORITY_LABELS[p]}
                      </button>
                    ))}
                  </div>
                </div>

                {/* Max price */}
                <div>
                  <label className="text-sm font-medium text-text-secondary block mb-1">Prezzo massimo (opzionale)</label>
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    value={addForm.maxPrice ?? ''}
                    onChange={(e) => setAddForm({ ...addForm, maxPrice: e.target.value ? Number(e.target.value) : undefined })}
                    placeholder="0.00"
                    className="w-full px-4 py-2.5 rounded-xl border border-border text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                  />
                </div>

                <Button onClick={handleAdd} isLoading={isSaving} className="w-full" size="lg">
                  Aggiungi alla Ricerca
                </Button>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
