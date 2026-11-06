import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import type { ShowcaseCard } from '../../api';
import { useFormat } from '../../i18n/format';

/** Carta della vetrina: apre la collezione del venditore sulla carta. Prezzo solo come riferimento. */
export default function ShowcaseCardTile({ card, sellerId }: { card: ShowcaseCard; sellerId: number }) {
  const { t } = useTranslation('showcase');
  const { euro } = useFormat();
  return (
    <Link to={`/collection/${sellerId}`} state={{ preselectCardId: card.id, preselectCardInfoId: card.cardInfoId }}
      className="bg-white rounded-2xl border border-border/60 overflow-hidden flex flex-col hover:border-primary/40">
      <div className="aspect-[63/88] bg-surface-dark">
        {card.imageUrl ? <img src={card.imageUrl} alt="" loading="lazy" className="w-full h-full object-cover" />
          : <span className="h-full grid place-items-center p-2 text-xs text-text-muted text-center">{card.name}</span>}
      </div>
      <div className="p-2">
        <p className="text-xs font-bold text-text line-clamp-1">{card.name}</p>
        <p className="text-[10px] text-text-muted line-clamp-1">{[card.setName, card.number].filter(Boolean).join(' · ')}</p>
        <p className="text-xs font-semibold mt-0.5">
          {card.price != null ? euro(card.price) : '—'}
          {card.price != null && <span className="ml-1 text-[10px] font-normal text-text-muted">{card.priceKind === 'asking' ? t('asking') : t('market')}</span>}
        </p>
      </div>
    </Link>
  );
}
