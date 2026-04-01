import { ArrowLeftRight } from 'lucide-react';
import type { Card } from '../../types';
import { CONDITION_LABELS } from '../../types';

interface CardGridItemProps {
  card: Card;
  onClick?: (card: Card) => void;
  selected?: boolean;
}

const CONDITION_MAP: Record<string, number> = {
  Mint: 1, NearMint: 2, Excellent: 3, Good: 4,
  LightlyPlayed: 5, ModeratelyPlayed: 6, HeavilyPlayed: 7, Damaged: 8,
};

export default function CardGridItem({ card, onClick, selected }: CardGridItemProps) {
  const cardName = card.cardName || card.cardInfo?.name || 'Carta';
  const image = card.imageSmall || card.imageNormal || card.cardInfo?.imageSmall || card.cardInfo?.imageUrl || '';
  const condNum = typeof card.condition === 'number' ? card.condition : (CONDITION_MAP[card.condition] || 0);
  const conditionLabel = typeof card.condition === 'string'
    ? card.condition.replace(/([A-Z])/g, ' $1').trim()
    : (CONDITION_LABELS[card.condition as import('../../types').CardCondition] || '');

  const condColor = condNum <= 2
    ? 'bg-green-500'
    : condNum <= 4
    ? 'bg-amber-500'
    : 'bg-red-500';

  return (
    <button
      onClick={() => onClick?.(card)}
      className={`relative bg-white rounded-xl overflow-hidden shadow-sm border transition-all text-left ${
        selected ? 'ring-2 ring-primary border-primary' : 'border-border/50 hover:shadow-md'
      }`}
    >
      {/* Image */}
      <div className="aspect-[5/7] bg-gray-100 relative">
        {image ? (
          <img
            src={image}
            alt={cardName}
            className="w-full h-full object-cover"
            loading="lazy"
          />
        ) : (
          <div className="w-full h-full bg-gradient-to-br from-gray-200 to-gray-300 flex items-center justify-center">
            <span className="text-xs text-gray-500 font-semibold text-center px-1 leading-tight">
              {cardName}
            </span>
          </div>
        )}

        {/* Quantity badge */}
        {card.quantity > 1 && (
          <span className="absolute top-1 right-1 bg-blue-600 text-white text-[10px] font-bold px-1.5 py-0.5 rounded-full shadow">
            x{card.quantity}
          </span>
        )}

        {/* Trade badge */}
        {card.isAvailableForTrade && (
          <span className="absolute top-1 left-1 bg-primary text-white p-1 rounded-full shadow">
            <ArrowLeftRight size={10} />
          </span>
        )}

        {/* Condition dot */}
        <span className={`absolute bottom-1 right-1 w-2.5 h-2.5 rounded-full ${condColor} ring-1 ring-white`}
              title={conditionLabel} />
      </div>

      {/* Info */}
      <div className="p-1.5">
        <p className="text-[11px] font-semibold text-text truncate leading-tight">{cardName}</p>
        <p className="text-[10px] text-text-muted truncate">{card.cardSetName || card.cardInfo?.cardSet?.name || ''}</p>
        {(card.estimatedValue != null && card.estimatedValue > 0) && (
          <p className="text-[10px] font-bold text-accent mt-0.5">€{card.estimatedValue.toFixed(2)}</p>
        )}
      </div>

      {/* Selected overlay */}
      {selected && (
        <div className="absolute inset-0 bg-primary/10 pointer-events-none" />
      )}
    </button>
  );
}
