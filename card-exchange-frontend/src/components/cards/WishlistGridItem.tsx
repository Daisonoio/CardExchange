import { Heart, X } from 'lucide-react';
import type { WishlistItem } from '../../types';

interface WishlistGridItemProps {
  item: WishlistItem;
  onClick?: (item: WishlistItem) => void;
  onDelete?: (item: WishlistItem) => void;
}

const PRIORITY_DOT: Record<number, string> = {
  1: 'bg-red-500',
  2: 'bg-amber-500',
  3: 'bg-blue-500',
};

export default function WishlistGridItem({ item, onClick, onDelete }: WishlistGridItemProps) {
  const cardName = item.cardName || item.cardInfo?.name || 'Carta';
  const image = item.imageSmall || item.imageNormal || item.cardInfo?.imageSmall || '';
  const matches = item.availableMatches ?? item.availableMatchesCount ?? 0;

  return (
    <button
      type="button"
      onClick={() => onClick?.(item)}
      className="relative w-full bg-white rounded-xl overflow-hidden shadow-sm border border-border/50 text-left transition-shadow hover:shadow-md"
    >
      {/* Image */}
      <div className="aspect-[5/7] bg-gray-100 relative">
        {image ? (
          <img src={image} alt={cardName} className="w-full h-full object-cover" loading="lazy" />
        ) : (
          <div className="w-full h-full bg-gradient-to-br from-primary/10 to-primary/5 flex items-center justify-center">
            <Heart size={24} className="text-primary/40" />
          </div>
        )}

        {/* Priority dot */}
        <span
          className={`absolute top-1 left-1 w-2.5 h-2.5 rounded-full ${PRIORITY_DOT[item.priority] || PRIORITY_DOT[2]} ring-1 ring-white`}
        />

        {/* Matches badge */}
        {matches > 0 && (
          <span className="absolute top-1 right-1 bg-green-600 text-white text-[9px] font-bold px-1.5 py-0.5 rounded-full shadow">
            {matches}
          </span>
        )}

        {/* Delete button */}
        {onDelete && (
          <button
            onClick={(e) => { e.stopPropagation(); onDelete(item); }}
            className="absolute bottom-1 right-1 bg-white/80 backdrop-blur p-1 rounded-full shadow hover:bg-red-50 text-text-muted hover:text-red-500"
          >
            <X size={12} />
          </button>
        )}
      </div>

      {/* Info */}
      <div className="p-1.5">
        <p className="text-[11px] font-semibold text-text truncate leading-tight">{cardName}</p>
        <p className="text-[10px] text-text-muted truncate">{item.cardSetName || item.cardInfo?.cardSet?.name || ''}</p>
      </div>
    </button>
  );
}
