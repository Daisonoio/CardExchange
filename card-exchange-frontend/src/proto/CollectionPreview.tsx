import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeftRight, Heart, Library, Loader2 } from 'lucide-react';
import { cards as cardsApi, wishlist as wishlistApi } from '../api';
import { useAuth } from '../context/AuthContext';
import { useFormat } from '../i18n/format';
import type { Card, WishlistItem } from '../types';
import { ThemeToggle } from './parts';

type Tab = 'have' | 'want';

/** Anteprima: collezione e wishlist nella nuova grafica, con il valore totale in evidenza. */
export default function CollectionPreview() {
  const { t } = useTranslation('proto');
  const { euro } = useFormat();
  const { user } = useAuth();
  const [tab, setTab] = useState<Tab>('have');
  const [cards, setCards] = useState<Card[] | null>(null);
  const [wishes, setWishes] = useState<WishlistItem[] | null>(null);

  useEffect(() => {
    if (!user) return;
    // Le API restituiscono { cards: [...] } e { items: [...] }: si accettano anche liste semplici
    cardsApi.getByUser(user.id)
      .then(({ data }) => setCards(Array.isArray(data) ? data : (data as unknown as { cards?: Card[] }).cards ?? []))
      .catch(() => setCards([]));
    wishlistApi.getByUser(user.id)
      .then(({ data }) => setWishes(Array.isArray(data) ? data : (data as unknown as { items?: WishlistItem[] }).items ?? []))
      .catch(() => setWishes([]));
  }, [user]);

  const stats = useMemo(() => {
    const list = cards ?? [];
    return {
      copies: list.reduce((s, c) => s + c.quantity, 0),
      value: list.reduce((s, c) => s + (c.marketValue ?? 0) * c.quantity, 0),
      trading: list.filter((c) => c.isAvailableForTrade).reduce((s, c) => s + c.quantity, 0),
    };
  }, [cards]);

  const loading = tab === 'have' ? cards === null : wishes === null;

  return (
    <div className="px-4 pt-4">
      <div className="flex items-center gap-2 mb-4">
        <h1 className="text-2xl font-extrabold flex-1">{t('collection.title')}</h1>
        <ThemeToggle />
      </div>

      {/* Riepilogo */}
      <div className="rounded-[24px] p-5 mb-4" style={{ background: 'linear-gradient(135deg, #2a1f63, #141029)', color: '#f5f3ff' }}>
        <p className="text-xs font-semibold opacity-70">{t('collection.value')}</p>
        <p className="text-3xl font-extrabold tracking-tight mt-0.5">{euro(stats.value)}</p>
        <div className="flex gap-5 mt-3 text-sm">
          <span><strong className="text-lg">{stats.copies}</strong> <span className="opacity-70">{t('collection.copies', { count: stats.copies })}</span></span>
          <span><strong className="text-lg" style={{ color: '#c8f53c' }}>{stats.trading}</strong> <span className="opacity-70">{t('collection.inTrade')}</span></span>
        </div>
      </div>

      <div className="flex p-1 rounded-full mb-4" style={{ background: 'var(--cx-surface-2)' }}>
        {(['have', 'want'] as Tab[]).map((k) => (
          <button key={k} onClick={() => setTab(k)} aria-pressed={tab === k}
            className="flex-1 py-2 rounded-full text-sm font-bold flex items-center justify-center gap-1.5"
            style={tab === k ? { background: 'var(--cx-accent)', color: 'var(--cx-on-accent)' } : { color: 'var(--cx-text-2)' }}>
            {k === 'have' ? <Library size={15} /> : <Heart size={15} />}
            {k === 'have' ? t('collection.have', { count: cards?.length ?? 0 }) : t('collection.want', { count: wishes?.length ?? 0 })}
          </button>
        ))}
      </div>

      {loading ? (
        <div className="flex justify-center py-12"><Loader2 className="animate-spin cx-accent-text" /></div>
      ) : tab === 'have' ? (
        cards!.length === 0 ? <Empty text={t('collection.emptyHave')} /> : (
          <div className="grid grid-cols-3 gap-2.5">
            {cards!.map((c) => (
              <div key={c.id} className="relative">
                <div className="aspect-[63/88] rounded-xl overflow-hidden" style={{ background: 'var(--cx-surface-2)' }}>
                  {(c.imageNormal ?? c.imageSmall)
                    ? <img src={c.imageNormal ?? c.imageSmall} alt="" loading="lazy" className="w-full h-full object-cover" />
                    : <div className="w-full h-full flex items-center justify-center text-[10px] cx-muted p-1 text-center">{c.cardName}</div>}
                </div>
                {c.isAvailableForTrade && (
                  <span className="cx-badge absolute left-1.5 bottom-1.5 w-6 h-6 flex items-center justify-center" title={t('collection.tradeable')} aria-label={t('collection.tradeable')}>
                    <ArrowLeftRight size={12} strokeWidth={2.6} />
                  </span>
                )}
                {c.quantity > 1 && <span className="absolute right-1.5 top-1.5 text-[10px] font-bold px-1.5 py-0.5 rounded-full" style={{ background: 'rgba(13,11,30,0.7)', color: '#fff' }}>×{c.quantity}</span>}
                <p className="text-[11px] font-bold mt-1 line-clamp-1">{c.cardName}</p>
                {c.marketValue != null && <p className="text-[10px] cx-muted">{euro(c.marketValue)}</p>}
              </div>
            ))}
          </div>
        )
      ) : (
        wishes!.length === 0 ? <Empty text={t('collection.emptyWant')} /> : (
          <ul className="space-y-2.5">
            {wishes!.map((w) => (
              <li key={w.id} className="cx-card flex items-center gap-3 p-2.5">
                <div className="w-12 aspect-[63/88] rounded-lg overflow-hidden shrink-0" style={{ background: 'var(--cx-surface-2)' }}>
                  {(w.imageSmall ?? w.imageNormal) && <img src={w.imageSmall ?? w.imageNormal} alt="" loading="lazy" className="w-full h-full object-cover" />}
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-bold line-clamp-1">{w.cardName}</p>
                  <p className="text-[11px] cx-muted line-clamp-1">{w.cardSetName}</p>
                </div>
                <Link to={`/preview?q=${encodeURIComponent(w.cardName ?? '')}`} className="cx-chip px-3 py-1.5 text-xs font-bold whitespace-nowrap">{t('collection.findNearby')}</Link>
              </li>
            ))}
          </ul>
        )
      )}
    </div>
  );
}

function Empty({ text }: { text: string }) {
  return <div className="cx-card p-6 text-center text-sm cx-muted">{text}</div>;
}
