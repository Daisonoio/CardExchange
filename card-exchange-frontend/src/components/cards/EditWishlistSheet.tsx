import { useEffect, useState } from 'react';
import { Trash2 } from 'lucide-react';
import { wishlist } from '../../api';
import type { CardCondition, WishlistItem } from '../../types';
import { CONDITION_LABELS } from '../../types';
import Button from '../ui/Button';
import BottomSheet from '../ui/BottomSheet';

interface EditWishlistSheetProps {
  item: WishlistItem | null;
  onClose: () => void;
  onUpdated: () => void;
  onDeleted?: (item: WishlistItem) => void;
}

const PRIORITY_LABELS = { 1: 'Alta', 2: 'Media', 3: 'Bassa' } as const;
const CONDITION_MAP: Record<string, number> = {
  Mint: 1, NearMint: 2, Excellent: 3, Good: 4,
  LightlyPlayed: 5, ModeratelyPlayed: 6, HeavilyPlayed: 7, Damaged: 8,
};

export default function EditWishlistSheet({ item, onClose, onUpdated, onDeleted }: EditWishlistSheetProps) {
  const [priority, setPriority] = useState<1 | 2 | 3>(2);
  const [preferredCondition, setPreferredCondition] = useState<CardCondition | undefined>(undefined);
  const [maxPrice, setMaxPrice] = useState<number | undefined>(undefined);
  const [notes, setNotes] = useState('');
  const [isSaving, setIsSaving] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!item) return;
    const p = item.priority === 1 || item.priority === 2 || item.priority === 3 ? item.priority : 2;
    const condNum = item.preferredCondition == null
      ? undefined
      : (typeof item.preferredCondition === 'number'
        ? item.preferredCondition
        : (CONDITION_MAP[item.preferredCondition] || undefined));

    setPriority(p);
    setPreferredCondition(condNum as CardCondition | undefined);
    setMaxPrice(item.maxPrice);
    setNotes(item.notes || '');
    setError(null);
  }, [item]);

  const handleSave = async () => {
    if (!item) return;
    setIsSaving(true);
    setError(null);
    try {
      await wishlist.update(item.id, {
        priority,
        preferredCondition,
        maxPrice,
        notes: notes || undefined,
      });
      onUpdated();
      onClose();
    } catch (err: any) {
      setError(err?.response?.data?.message || 'Errore durante il salvataggio');
    } finally {
      setIsSaving(false);
    }
  };

  const handleDelete = async () => {
    if (!item) return;
    if (!confirm(`Rimuovere \"${item.cardName || item.cardInfo?.name || 'questa carta'}\" dalla ricerca?`)) return;
    setIsDeleting(true);
    try {
      await wishlist.delete(item.id);
      onDeleted?.(item);
      onClose();
    } catch (err: any) {
      setError(err?.response?.data?.message || 'Errore durante l\'eliminazione');
    } finally {
      setIsDeleting(false);
    }
  };

  if (!item) return null;

  const cardName = item.cardName || item.cardInfo?.name || 'Carta';
  const cardSetName = item.cardSetName || item.cardInfo?.cardSet?.name || '';
  const image = item.imageNormal || item.imageSmall || item.cardInfo?.imageNormal || item.cardInfo?.imageSmall || '';

  return (
    <BottomSheet open={!!item} onClose={onClose} title="Modifica Ricerca">
      <div className="space-y-4">
        <div className="flex gap-4 p-3 bg-surface-dark rounded-xl">
          {image ? (
            <img src={image} alt={cardName} className="w-20 h-28 rounded-lg object-cover shadow-sm" />
          ) : (
            <div className="w-20 h-28 rounded-lg bg-gradient-to-br from-primary/10 to-primary/5" />
          )}
          <div className="flex-1 min-w-0">
            <p className="font-semibold text-sm truncate">{cardName}</p>
            <p className="text-xs text-text-secondary truncate">{cardSetName}</p>
            {item.rarity && <p className="text-xs text-text-muted mt-0.5">{item.rarity}</p>}
          </div>
        </div>

        <div>
          <label className="text-xs font-semibold text-text-secondary block mb-1.5">Priorita</label>
          <div className="flex gap-2">
            {([1, 2, 3] as const).map((p) => (
              <button
                key={p}
                onClick={() => setPriority(p)}
                className={`flex-1 px-3 py-2 rounded-xl text-xs font-medium border transition-colors ${
                  priority === p
                    ? 'border-primary bg-primary/10 text-primary'
                    : 'border-border text-text-secondary hover:bg-surface-dark'
                }`}
              >
                {PRIORITY_LABELS[p]}
              </button>
            ))}
          </div>
        </div>

        <div>
          <label className="text-xs font-semibold text-text-secondary block mb-1.5">Condizione preferita</label>
          <div className="grid grid-cols-2 gap-1.5">
            <button
              onClick={() => setPreferredCondition(undefined)}
              className={`px-3 py-2 rounded-xl text-xs font-medium border transition-colors ${
                preferredCondition == null
                  ? 'border-primary bg-primary/10 text-primary'
                  : 'border-border text-text-secondary hover:bg-surface-dark'
              }`}
            >
              Qualsiasi
            </button>
            {([1, 2, 3, 4, 5, 6, 7, 8] as CardCondition[]).map((c) => (
              <button
                key={c}
                onClick={() => setPreferredCondition(c)}
                className={`px-3 py-2 rounded-xl text-xs font-medium border transition-colors ${
                  preferredCondition === c
                    ? 'border-primary bg-primary/10 text-primary'
                    : 'border-border text-text-secondary hover:bg-surface-dark'
                }`}
              >
                {CONDITION_LABELS[c]}
              </button>
            ))}
          </div>
        </div>

        <div>
          <label className="text-xs font-semibold text-text-secondary block mb-1.5">Prezzo massimo (opzionale)</label>
          <input
            type="number"
            step="0.01"
            min="0"
            value={maxPrice ?? ''}
            onChange={(e) => setMaxPrice(e.target.value ? Number(e.target.value) : undefined)}
            placeholder="0.00 EUR"
            className="w-full px-4 py-2.5 rounded-xl border border-border text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
          />
        </div>

        <div>
          <label className="text-xs font-semibold text-text-secondary block mb-1.5">Note (opzionale)</label>
          <textarea
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            placeholder="es. Cerco versione foil"
            className="w-full px-4 py-2.5 rounded-xl border border-border text-sm resize-none h-20 focus:outline-none focus:ring-2 focus:ring-primary/30"
          />
        </div>

        {error && (
          <p className="text-xs text-danger bg-red-50 px-3 py-2 rounded-lg">{error}</p>
        )}

        <div className="flex gap-2">
          {onDeleted && (
            <button
              onClick={handleDelete}
              disabled={isDeleting}
              className="p-3 rounded-xl border border-red-200 text-red-500 hover:bg-red-50 transition-colors"
            >
              <Trash2 size={18} />
            </button>
          )}
          <Button onClick={handleSave} isLoading={isSaving} className="flex-1" size="lg">
            Salva modifiche
          </Button>
        </div>
      </div>
    </BottomSheet>
  );
}
