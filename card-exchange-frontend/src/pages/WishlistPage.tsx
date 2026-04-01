import { useState, useEffect, useCallback } from 'react';
import { Plus, Heart, X, ChevronDown, Loader2 } from 'lucide-react';
import { wishlist, scryfall } from '../api';
import { useAuth } from '../context/AuthContext';
import type { WishlistItem, ScryfallCard, CardCondition } from '../types';
import { CONDITION_LABELS } from '../types';
import ScryfallSearch from '../components/cards/ScryfallSearch';
import Button from '../components/ui/Button';
import EmptyState from '../components/ui/EmptyState';
import BottomSheet from '../components/ui/BottomSheet';

const PRIORITY_LABELS = { 1: 'Alta', 2: 'Media', 3: 'Bassa' } as const;
const PRIORITY_COLORS = {
  1: 'bg-red-50 text-red-600',
  2: 'bg-amber-50 text-amber-600',
  3: 'bg-blue-50 text-blue-600',
} as const;

export default function WishlistPage() {
  const { user } = useAuth();
  const [items, setItems] = useState<WishlistItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [showAddModal, setShowAddModal] = useState(false);
  const [selectedCard, setSelectedCard] = useState<ScryfallCard | null>(null);
  const [printings, setPrintings] = useState<ScryfallCard[]>([]);
  const [loadingPrintings, setLoadingPrintings] = useState(false);
  const [showPrintings, setShowPrintings] = useState(false);
  const [addForm, setAddForm] = useState({
    priority: 2 as 1 | 2 | 3,
    preferredCondition: undefined as CardCondition | undefined,
    maxPrice: undefined as number | undefined,
    notes: '',
  });
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  const loadItems = useCallback(async () => {
    if (!user) return;
    try {
      const { data } = await wishlist.getByUser(user.id);
      const list = Array.isArray(data)
        ? data
        : (data as any)?.items ?? (data as any)?.wishlistItems ?? [];
      setItems(Array.isArray(list) ? list : []);
    } catch (err) {
      console.error('Errore caricamento wishlist:', err);
    } finally {
      setIsLoading(false);
    }
  }, [user]);

  useEffect(() => { loadItems(); }, [loadItems]);

  // Load all printings when card is selected
  const loadPrintings = useCallback(async (cardName: string) => {
    setLoadingPrintings(true);
    try {
      const { data } = await scryfall.search(`!"${cardName}" unique:prints`);
      const cards = data.data ?? data.cards ?? [];
      setPrintings(Array.isArray(cards) ? cards : []);
    } catch {
      setPrintings([]);
    } finally {
      setLoadingPrintings(false);
    }
  }, []);

  const handleSelectCard = (card: ScryfallCard) => {
    setSelectedCard(card);
    setSaveError(null);
    loadPrintings(card.name);
  };

  const handleSelectPrinting = (card: ScryfallCard) => {
    setSelectedCard(card);
    setShowPrintings(false);
  };

  const getCardId = (card: ScryfallCard) => card.scryfallId || card.id;
  const getCardImage = (card: ScryfallCard) =>
    card.image_uris?.normal || card.images?.normal ||
    card.image_uris?.large || card.images?.large ||
    card.card_faces?.[0]?.image_uris?.normal || '';
  const getCardImageSmall = (card: ScryfallCard) =>
    card.image_uris?.small || card.images?.small ||
    card.card_faces?.[0]?.image_uris?.small || '';
  const getSetName = (card: ScryfallCard) => card.set_name || card.setName || '';
  const getSetCode = (card: ScryfallCard) => card.setCode || card.set || '';

  const handleAdd = async () => {
    if (!selectedCard) return;
    setIsSaving(true);
    setSaveError(null);
    try {
      const scryfallId = getCardId(selectedCard);
      const { data: importResult } = await scryfall.importCard(scryfallId);
      const cardInfoId = importResult.cardInfoId || importResult.id;

      await wishlist.create(user!.id, {
        cardInfoId,
        priority: addForm.priority,
        preferredCondition: addForm.preferredCondition,
        maxPrice: addForm.maxPrice,
        notes: addForm.notes || undefined,
      });

      setShowAddModal(false);
      setSelectedCard(null);
      setPrintings([]);
      setAddForm({ priority: 2, preferredCondition: undefined, maxPrice: undefined, notes: '' });
      loadItems();
    } catch (err: any) {
      const msg = err?.response?.data?.message;
      setSaveError(msg || 'Errore durante il salvataggio');
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

  const closeModal = () => {
    setShowAddModal(false);
    setSelectedCard(null);
    setPrintings([]);
    setShowPrintings(false);
    setSaveError(null);
  };

  const getPriority = (p: number | undefined): 1 | 2 | 3 =>
    p === 1 || p === 2 || p === 3 ? p : 2;
  const grouped = {
    1: items.filter((i) => getPriority(i.priority) === 1),
    2: items.filter((i) => getPriority(i.priority) === 2),
    3: items.filter((i) => getPriority(i.priority) === 3),
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
                          {(item.imageSmall || item.imageNormal || item.cardInfo?.imageSmall) ? (
                            <img
                              src={item.imageSmall || item.imageNormal || item.cardInfo?.imageSmall || ''}
                              alt={item.cardInfo?.name || item.cardName || 'Card'}
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
                              <h3 className="text-sm font-semibold truncate">{item.cardName || item.cardInfo?.name}</h3>
                              <p className="text-xs text-text-secondary truncate">
                                {item.cardSetName || item.cardInfo?.cardSet?.name}
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
                            {((item.availableMatches ?? item.availableMatchesCount ?? 0) > 0) && (
                              <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-green-50 text-green-600">
                                {item.availableMatches ?? item.availableMatchesCount} disponibili
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

      {/* Add Bottom Sheet */}
      <BottomSheet open={showAddModal} onClose={closeModal} title="Aggiungi alla Ricerca">
        {!selectedCard ? (
          <div>
            <p className="text-sm text-text-secondary mb-3">
              Quale carta stai cercando?
            </p>
            <ScryfallSearch onSelect={handleSelectCard} placeholder="Cerca la carta desiderata..." />
          </div>
        ) : (
          <div>
            {/* Card image - hero section */}
            <div className="relative bg-gradient-to-b from-gray-900 to-gray-800 flex justify-center py-5 rounded-xl -mx-1">
              {getCardImage(selectedCard) ? (
                <img
                  src={getCardImage(selectedCard)}
                  alt={selectedCard.name}
                  className="h-64 rounded-xl shadow-2xl object-contain"
                />
              ) : (
                <div className="h-64 w-44 rounded-xl bg-gray-700 flex items-center justify-center">
                  <span className="text-white text-center text-sm font-bold px-3">{selectedCard.name}</span>
                </div>
              )}
            </div>

            {/* Card name + set selector */}
            <div className="pt-4">
              <h3 className="text-base font-bold">{selectedCard.name}</h3>

              {/* Set selector */}
              <button
                onClick={() => setShowPrintings(!showPrintings)}
                className="mt-1.5 flex items-center gap-1.5 text-sm text-primary hover:underline"
              >
                <span className="uppercase font-semibold text-xs bg-primary/10 px-1.5 py-0.5 rounded">
                  {getSetCode(selectedCard)}
                </span>
                <span>{getSetName(selectedCard)}</span>
                <ChevronDown size={14} className={`transition-transform ${showPrintings ? 'rotate-180' : ''}`} />
              </button>

              {/* Price */}
              {selectedCard.prices?.eur && (
                <p className="text-sm font-bold text-accent mt-1">{selectedCard.prices.eur} EUR</p>
              )}

              {/* Printings dropdown */}
              {showPrintings && (
                <div className="mt-2 max-h-48 overflow-y-auto border border-border rounded-xl bg-surface-dark">
                  {loadingPrintings ? (
                    <div className="flex justify-center py-4">
                      <Loader2 size={20} className="animate-spin text-primary" />
                    </div>
                  ) : (
                    <>
                      <div className="px-3 py-2 text-xs text-text-muted border-b border-border/50">
                        {printings.length} espansioni disponibili
                      </div>
                      {printings.map((p) => {
                        const isSelected = getCardId(p) === getCardId(selectedCard);
                        return (
                          <button
                            key={getCardId(p)}
                            onClick={() => handleSelectPrinting(p)}
                            className={`w-full flex items-center gap-3 px-3 py-2.5 text-left hover:bg-white transition-colors border-b border-border/30 last:border-0 ${
                              isSelected ? 'bg-primary/5' : ''
                            }`}
                          >
                            {getCardImageSmall(p) && (
                              <img src={getCardImageSmall(p)} alt="" className="w-8 h-11 rounded object-cover shrink-0" />
                            )}
                            <div className="min-w-0 flex-1">
                              <p className="text-xs font-semibold truncate">
                                <span className="uppercase text-text-muted">{getSetCode(p)}</span>
                                {' '}{getSetName(p)}
                              </p>
                              <p className="text-xs text-text-muted">
                                {p.rarity}{p.prices?.eur ? ` · ${p.prices.eur} EUR` : ''}
                              </p>
                            </div>
                            {isSelected && (
                              <span className="text-xs font-semibold text-primary shrink-0">Selezionata</span>
                            )}
                          </button>
                        );
                      })}
                    </>
                  )}
                </div>
              )}
            </div>

            {/* Form options */}
            <div className="pt-4 space-y-4">
              {/* Priority */}
              <div>
                <label className="text-xs font-semibold text-text-secondary block mb-1.5">Priorità</label>
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
                <label className="text-xs font-semibold text-text-secondary block mb-1.5">Prezzo massimo (opzionale)</label>
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  value={addForm.maxPrice ?? ''}
                  onChange={(e) => setAddForm({ ...addForm, maxPrice: e.target.value ? Number(e.target.value) : undefined })}
                  placeholder="0.00 EUR"
                  className="w-full px-4 py-2.5 rounded-xl border border-border text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                />
              </div>

              {/* Notes */}
              <div>
                <label className="text-xs font-semibold text-text-secondary block mb-1.5">Note (opzionale)</label>
                <input
                  type="text"
                  value={addForm.notes}
                  onChange={(e) => setAddForm({ ...addForm, notes: e.target.value })}
                  placeholder="es. Cerco versione foil"
                  className="w-full px-4 py-2.5 rounded-xl border border-border text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                />
              </div>

              {saveError && (
                <p className="text-xs text-danger bg-red-50 px-3 py-2 rounded-lg">{saveError}</p>
              )}

              <div className="flex gap-2">
                <button
                  onClick={() => { setSelectedCard(null); setPrintings([]); setShowPrintings(false); }}
                  className="px-4 py-2.5 rounded-xl text-sm font-medium text-text-secondary border border-border hover:bg-surface-dark transition-colors"
                >
                  Cambia carta
                </button>
                <Button onClick={handleAdd} isLoading={isSaving} className="flex-1" size="lg">
                  Aggiungi alla Ricerca
                </Button>
              </div>
            </div>
          </div>
        )}
      </BottomSheet>
    </div>
  );
}
