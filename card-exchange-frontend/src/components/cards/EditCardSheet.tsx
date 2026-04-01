import { useState, useEffect } from 'react';
import { Trash2 } from 'lucide-react';
import type { Card, CardCondition } from '../../types';
import { CONDITION_LABELS } from '../../types';
import { cards } from '../../api';
import Button from '../ui/Button';
import BottomSheet from '../ui/BottomSheet';

interface EditCardSheetProps {
  card: Card | null;
  onClose: () => void;
  onUpdated: () => void;
  onDeleted?: (card: Card) => void;
}

const CONDITION_MAP: Record<string, number> = {
  Mint: 1, NearMint: 2, Excellent: 3, Good: 4,
  LightlyPlayed: 5, ModeratelyPlayed: 6, HeavilyPlayed: 7, Damaged: 8,
};

export default function EditCardSheet({ card, onClose, onUpdated, onDeleted }: EditCardSheetProps) {
  const [condition, setCondition] = useState<CardCondition>(2);
  const [quantity, setQuantity] = useState(1);
  const [isAvailableForTrade, setIsAvailableForTrade] = useState(true);
  const [notes, setNotes] = useState('');
  const [isSaving, setIsSaving] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Sync form with card prop
  useEffect(() => {
    if (!card) return;
    const condNum = typeof card.condition === 'number'
      ? card.condition
      : (CONDITION_MAP[card.condition] || 2);
    setCondition(condNum as CardCondition);
    setQuantity(card.quantity || 1);
    setIsAvailableForTrade(card.isAvailableForTrade ?? true);
    setNotes(card.notes || '');
    setError(null);
  }, [card]);

  const handleSave = async () => {
    if (!card) return;
    setIsSaving(true);
    setError(null);
    try {
      await cards.update(card.id, {
        condition,
        quantity,
        isAvailableForTrade,
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
    if (!card) return;
    if (!confirm(`Rimuovere "${card.cardName || card.cardInfo?.name}" dalla collezione?`)) return;
    setIsDeleting(true);
    try {
      await cards.delete(card.id);
      onDeleted?.(card);
      onClose();
    } catch (err: any) {
      setError(err?.response?.data?.message || 'Errore durante l\'eliminazione');
    } finally {
      setIsDeleting(false);
    }
  };

  if (!card) return null;

  const image = card.imageNormal || card.imageSmall || card.cardInfo?.imageNormal || card.cardInfo?.imageSmall || '';
  const cardName = card.cardName || card.cardInfo?.name || 'Carta';
  const cardSetName = card.cardSetName || card.cardInfo?.cardSet?.name || '';

  return (
    <BottomSheet open={!!card} onClose={onClose} title="Modifica Carta">
      <div className="space-y-4">
        {/* Card preview */}
        <div className="flex gap-4 p-3 bg-surface-dark rounded-xl">
          {image ? (
            <img src={image} alt={cardName} className="w-20 h-28 rounded-lg object-cover shadow-sm" />
          ) : (
            <div className="w-20 h-28 rounded-lg bg-gradient-to-br from-gray-200 to-gray-300 flex items-center justify-center">
              <span className="text-xs text-gray-500 font-semibold text-center px-1">{cardName}</span>
            </div>
          )}
          <div className="flex-1 min-w-0">
            <p className="font-semibold text-sm truncate">{cardName}</p>
            <p className="text-xs text-text-secondary truncate">{cardSetName}</p>
            {card.rarity && <p className="text-xs text-text-muted mt-0.5">{card.rarity}</p>}
            {card.estimatedValue != null && card.estimatedValue > 0 && (
              <p className="text-sm font-bold text-accent mt-1">€{card.estimatedValue.toFixed(2)}</p>
            )}
          </div>
        </div>

        {/* Condition */}
        <div>
          <label className="text-xs font-semibold text-text-secondary block mb-1.5">Condizione</label>
          <div className="grid grid-cols-2 gap-1.5">
            {([1, 2, 3, 4, 5, 6, 7, 8] as CardCondition[]).map((c) => (
              <button
                key={c}
                onClick={() => setCondition(c)}
                className={`px-3 py-2 rounded-xl text-xs font-medium border transition-colors ${
                  condition === c
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
              onClick={() => setQuantity(Math.max(1, quantity - 1))}
              className="w-10 h-10 rounded-xl border border-border flex items-center justify-center text-lg hover:bg-surface-dark"
            >
              -
            </button>
            <span className="text-lg font-bold w-8 text-center">{quantity}</span>
            <button
              onClick={() => setQuantity(quantity + 1)}
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
            onClick={() => setIsAvailableForTrade(!isAvailableForTrade)}
            className={`w-12 h-7 rounded-full transition-colors relative ${
              isAvailableForTrade ? 'bg-primary' : 'bg-gray-300'
            }`}
          >
            <div className={`w-5 h-5 bg-white rounded-full absolute top-1 transition-transform ${
              isAvailableForTrade ? 'translate-x-6' : 'translate-x-1'
            }`} />
          </button>
        </div>

        {/* Notes */}
        <div>
          <label className="text-xs font-semibold text-text-secondary block mb-1.5">Note (opzionale)</label>
          <textarea
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            placeholder="Es: Foil, italiano, prima edizione..."
            className="w-full px-4 py-2.5 rounded-xl border border-border text-sm resize-none h-20 focus:outline-none focus:ring-2 focus:ring-primary/30"
          />
        </div>

        {error && (
          <p className="text-xs text-danger bg-red-50 px-3 py-2 rounded-lg">{error}</p>
        )}

        {/* Actions */}
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
