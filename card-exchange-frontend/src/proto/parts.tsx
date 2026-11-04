import { Heart, MapPin, Moon, Sun, SunMoon } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link, useOutletContext } from 'react-router-dom';
import { useFormat } from '../i18n/format';
import type { ExploreCard } from './api';
import type { ProtoContext, ThemeChoice } from './ProtoShell';

/** Prezzo sempre indicativo: indicato dal proprietario o valore di mercato. */
export function PriceLine({ price, kind, size = 'sm' }: { price?: number; kind: 'asking' | 'market'; size?: 'sm' | 'lg' }) {
  const { t } = useTranslation('proto');
  const { euro } = useFormat();
  if (price == null) return <span className="text-xs cx-muted">{t('price.none')}</span>;
  return (
    <span className="flex items-baseline gap-1.5 flex-wrap">
      <span className={size === 'lg' ? 'text-3xl font-extrabold tracking-tight' : 'text-[15px] font-extrabold'}>{euro(price)}</span>
      <span className={`${size === 'lg' ? 'text-xs' : 'text-[10px]'} cx-muted font-medium`}>{kind === 'asking' ? t('price.asking') : t('price.market')}</span>
    </span>
  );
}

export function DistanceBadge({ km, className = '' }: { km?: number; className?: string }) {
  const { t } = useTranslation('proto');
  if (km == null) return null;
  return (
    <span className={`cx-badge inline-flex items-center gap-0.5 px-2 py-0.5 text-[11px] ${className}`}>
      <MapPin size={11} strokeWidth={2.6} />{t('distance', { km })}
    </span>
  );
}

export function CardTile({ card }: { card: ExploreCard }) {
  const { t } = useTranslation('proto');
  return (
    <Link to={`/preview/card/${card.id}`} className="cx-card overflow-hidden flex flex-col active:scale-[0.99] transition-transform">
      <div className="relative aspect-[63/88] m-1.5 mb-0 rounded-[14px] overflow-hidden" style={{ background: 'var(--cx-surface-2)' }}>
        {card.imageUrl
          ? <img src={card.imageUrl} alt="" loading="lazy" className="w-full h-full object-cover" />
          : <div className="w-full h-full flex items-center justify-center text-xs cx-muted p-2 text-center">{card.name}</div>}
        <DistanceBadge km={card.approxKm} className="absolute left-2 bottom-2" />
        {card.isWanted && (
          <span className="absolute right-2 top-2 w-7 h-7 rounded-full flex items-center justify-center" style={{ background: 'rgba(13,11,30,0.6)' }}
            title={t('wantedHint')} aria-label={t('wantedHint')}>
            <Heart size={14} fill="#ff6b7a" stroke="#ff6b7a" />
          </span>
        )}
      </div>
      <div className="px-3 pt-2 pb-3 flex-1 flex flex-col">
        <p className="text-[13px] font-bold leading-tight line-clamp-1">{card.name}</p>
        <p className="text-[11px] cx-muted line-clamp-1 mt-0.5">{[card.setName, card.number].filter(Boolean).join(' · ')}</p>
        <div className="mt-auto pt-1.5"><PriceLine price={card.price} kind={card.priceKind} /></div>
      </div>
    </Link>
  );
}

const NEXT: Record<ThemeChoice, ThemeChoice> = { auto: 'dark', dark: 'light', light: 'auto' };

export function ThemeToggle() {
  const { t } = useTranslation('proto');
  const { theme, setTheme } = useOutletContext<ProtoContext>();
  const Icon = theme === 'dark' ? Moon : theme === 'light' ? Sun : SunMoon;
  return (
    <button onClick={() => setTheme(NEXT[theme])} aria-label={t(`theme.${theme}`)} title={t(`theme.${theme}`)}
      className="w-10 h-10 rounded-full flex items-center justify-center" style={{ background: 'var(--cx-surface-2)' }}>
      <Icon size={18} />
    </button>
  );
}
