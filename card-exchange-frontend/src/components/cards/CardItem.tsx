import { useTranslation } from 'react-i18next';
import { MoreHorizontal, ArrowLeftRight } from 'lucide-react';
import type { Card, CardCondition } from '../../types';
import { CONDITION_LABELS } from '../../types';
import CardVariantBadges from './CardVariantBadges';
import PriceTag from './PriceTag';

interface CardItemProps {
  card: Card;
  onEdit?: (card: Card) => void;
  onDelete?: (card: Card) => void;
  onClick?: (card: Card) => void;
  showUser?: boolean;
}

// Map string condition to numeric for color
const CONDITION_MAP: Record<string, number> = {
  Mint: 1, NearMint: 2, Excellent: 3, Good: 4,
  LightlyPlayed: 5, ModeratelyPlayed: 6, HeavilyPlayed: 7, Damaged: 8,
};

export default function CardItem({ card, onEdit, onDelete, onClick, showUser }: CardItemProps) {
  const info = card.cardInfo;
  const { t } = useTranslation('cards');
  const cardName = card.cardName || info?.name || t('unknownCard');
  const cardSetName = card.cardSetName || info?.cardSet?.name || '';
  const cardRarity = card.rarity || info?.rarity || '';
  const image = card.imageSmall || card.imageNormal || info?.imageSmall || info?.imageUrl || '';
  const condNum = typeof card.condition === 'number' ? card.condition : (CONDITION_MAP[card.condition] || 0);
  const conditionLabel = typeof card.condition === 'string'
    ? card.condition
    : (CONDITION_LABELS[card.condition as CardCondition] || 'N/A');

  const conditionColor = condNum <= 2
    ? 'text-green-600 bg-green-50'
    : condNum <= 4
    ? 'text-amber-600 bg-amber-50'
    : 'text-red-600 bg-red-50';

  return (
    <div
      className={`bg-white rounded-2xl p-3 shadow-sm border border-border/50 hover:shadow-md transition-shadow ${onClick ? 'cursor-pointer' : ''}`}
      onClick={onClick ? () => onClick(card) : undefined}
    >
      <div className="flex gap-3">
        {/* Card image */}
        <div className="w-16 shrink-0">
          {image ? (
            <img
              src={image}
              alt={info?.name || 'Card'}
              className="w-16 h-22 rounded-lg object-cover shadow-sm"
              loading="lazy"
            />
          ) : (
            <div className="w-16 h-22 rounded-lg bg-gradient-to-br from-primary/20 to-primary/5 flex items-center justify-center">
              <span className="text-2xl">🃏</span>
            </div>
          )}
        </div>

        {/* Card info */}
        <div className="flex-1 min-w-0">
          <div className="flex items-start justify-between gap-2">
            <div className="min-w-0">
              <h3 className="text-sm font-semibold text-text truncate">
                {cardName}
              </h3>
              <p className="text-xs text-text-secondary truncate">
                {cardSetName}
                {cardRarity && <span> &middot; {cardRarity}</span>}
              </p>
            </div>

            {(onEdit || onDelete) && (
              <div className="relative group">
                <button className="p-1 rounded-lg hover:bg-surface-dark transition-colors">
                  <MoreHorizontal size={16} className="text-text-muted" />
                </button>
                <div className="absolute right-0 top-full mt-1 bg-white rounded-lg shadow-lg border border-border py-1 hidden group-focus-within:block z-10 min-w-[120px]">
                  {onEdit && (
                    <button
                      onClick={() => onEdit(card)}
                      className="w-full text-left px-3 py-2 text-sm hover:bg-surface-dark"
                    >
                      {t('item.edit')}
                    </button>
                  )}
                  {onDelete && (
                    <button
                      onClick={() => onDelete(card)}
                      className="w-full text-left px-3 py-2 text-sm hover:bg-red-50 text-danger"
                    >
                      {t('item.remove')}
                    </button>
                  )}
                </div>
              </div>
            )}
          </div>

          {/* Badges row */}
          <div className="flex items-center gap-2 mt-2 flex-wrap">
            <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${conditionColor}`}>
              {conditionLabel}
            </span>
            <CardVariantBadges language={card.language} finish={card.finish} />
            {card.quantity > 1 && (
              <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-blue-50 text-blue-600">
                x{card.quantity}
              </span>
            )}
            {card.isAvailableForTrade && (
              <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-primary/10 text-primary flex items-center gap-1">
                <ArrowLeftRight size={10} />
                {t('item.trade')}
              </span>
            )}
          </div>

          {/* Price */}
          <div className="flex items-center justify-between mt-2">
            <div className="flex items-center gap-3">
              <PriceTag askingPrice={card.askingPrice} marketValue={card.marketValue ?? info?.priceEur ?? card.estimatedValue} />
            </div>
            {showUser && (card.user || card.userUsername) && (
              <span className="text-xs text-text-secondary">@{card.user?.username || card.userUsername}</span>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
