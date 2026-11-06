import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { CheckCircle2, Loader2, Search, ShoppingBag } from 'lucide-react';
import { reservations } from '../../api';
import type { Reservation, StoreCardItem } from '../../types';
import { conditionLabel } from '../../types';
import { useFormat } from '../../i18n/format';
import { apiErrorMessage } from '../../utils/apiError';
import BottomSheet from '../ui/BottomSheet';
import Button from '../ui/Button';

/**
 * Carte disponibili nel negozio, con "Prenota e ritira" se il negozio accetta prenotazioni.
 * Solo le carte del negozio: nessun confronto di prezzo con altri venditori.
 */
export default function StoreCardsSection({ storeId, isOwner }: { storeId: number; isOwner: boolean }) {
  const { t } = useTranslation('reservations');
  const { euro, dateTime } = useFormat();
  const [query, setQuery] = useState('');
  const [items, setItems] = useState<StoreCardItem[] | null>(null);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [accepts, setAccepts] = useState(false);
  const [selected, setSelected] = useState<StoreCardItem | null>(null);
  const [quantity, setQuantity] = useState(1);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState<Reservation | null>(null);

  const load = useCallback((q: string, p: number) => {
    reservations.storeCards(storeId, { q: q || undefined, page: p })
      .then(({ data }) => {
        setAccepts(data.acceptsReservations);
        setTotal(data.total);
        setItems((prev) => (p === 1 ? data.items : [...(prev ?? []), ...data.items]));
      })
      .catch(() => setItems([]));
  }, [storeId]);

  useEffect(() => {
    const h = setTimeout(() => { setPage(1); load(query, 1); }, 300);
    return () => clearTimeout(h);
  }, [query, load]);

  const open = (card: StoreCardItem) => {
    setSelected(card);
    setQuantity(1);
    setError(null);
    setDone(null);
  };

  const reserve = async () => {
    if (!selected) return;
    setBusy(true);
    setError(null);
    try {
      const { data } = await reservations.create(storeId, selected.id, quantity);
      setDone(data);
      load(query, 1);
    } catch (err) {
      setError(apiErrorMessage(err, t('cards.empty')));
    } finally {
      setBusy(false);
    }
  };

  if (items !== null && items.length === 0 && !query) return null;

  return (
    <section className="bg-white rounded-2xl border border-border p-3 space-y-3">
      <div className="flex items-center justify-between gap-2">
        <h2 className="text-sm font-semibold text-text flex items-center gap-1.5"><ShoppingBag size={16} className="text-primary" /> {t('cards.title')}</h2>
        {total > 0 && <span className="text-xs text-text-muted tabular-nums">{total}</span>}
      </div>
      <label className="relative block">
        <span className="sr-only">{t('cards.search')}</span>
        <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
        <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder={t('cards.search')}
          className="w-full pl-8 pr-3 py-2 rounded-xl border border-border text-sm" />
      </label>
      {items === null ? (
        <div className="flex justify-center py-4"><Loader2 className="animate-spin text-primary" size={18} /></div>
      ) : items.length === 0 ? (
        <p className="text-sm text-text-muted">{t('cards.empty')}</p>
      ) : (
        <ul className="divide-y divide-border/60">
          {items.map((c) => (
            <li key={c.id} className="flex gap-3 py-2.5">
              {c.imageUrl
                ? <img src={c.imageUrl} alt="" loading="lazy" className="w-11 h-[62px] object-cover rounded-md bg-surface-dark shrink-0" onError={(e) => { e.currentTarget.style.visibility = 'hidden'; }} />
                : <div className="w-11 h-[62px] rounded-md bg-surface-dark shrink-0" />}
              <div className="flex-1 min-w-0">
                <p className="text-sm font-semibold text-text truncate">{c.name}</p>
                <p className="text-xs text-text-muted truncate">{[c.setName, conditionLabel(c.condition), c.language?.toUpperCase(), c.finish !== 'Normal' ? c.finish : null].filter(Boolean).join(' · ')}</p>
                <p className="text-xs text-text-secondary mt-0.5">
                  {c.priceEur != null && <strong className="text-text">{euro(c.priceEur)}</strong>}
                  {c.priceEur != null && ' · '}{t('cards.available', { count: c.available })}
                </p>
              </div>
              {accepts && !isOwner && (
                <button type="button" onClick={() => open(c)} className="self-center shrink-0 px-3 py-1.5 rounded-xl bg-primary text-white text-xs font-semibold">
                  {t('cards.reserve')}
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
      {items !== null && items.length < total && (
        <button type="button" onClick={() => { const p = page + 1; setPage(p); load(query, p); }} className="w-full text-sm font-semibold text-primary">
          {t('cards.more')}
        </button>
      )}

      <BottomSheet open={selected !== null} onClose={() => setSelected(null)} title={done ? t('sheet.doneTitle') : t('sheet.title')}>
        {selected && (done ? (
          <div className="space-y-3 text-center">
            <CheckCircle2 size={32} className="mx-auto text-green-600" />
            <p className="text-sm text-text-secondary">{t('sheet.doneText')}</p>
            <p className="text-3xl font-mono font-bold tracking-[0.3em] text-text">{done.pickupCode}</p>
            <p className="text-xs text-text-secondary first-letter:uppercase">{t('until', { when: dateTime(done.expiresAt, { weekday: 'long', day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' }) })}</p>
            <Link to="/reservations" className="inline-block px-4 py-2 rounded-xl bg-primary text-white text-sm font-semibold">{t('sheet.goToMine')}</Link>
          </div>
        ) : (
          <div className="space-y-3">
            <p className="text-sm font-semibold text-text">{selected.name}</p>
            <p className="text-sm text-text-secondary">
              {selected.priceEur != null ? <>{t('sheet.price')}: <strong className="text-text">{euro(selected.priceEur)}</strong></> : t('sheet.priceNone')}
            </p>
            {selected.available > 1 && (
              <label className="block text-xs font-semibold text-text-secondary">{t('sheet.quantity')}
                <input type="number" min={1} max={selected.available} value={quantity}
                  onChange={(e) => setQuantity(Math.min(selected.available, Math.max(1, Number(e.target.value) || 1)))}
                  className="mt-1 w-24 px-3 py-2 rounded-xl border border-border text-sm" />
              </label>
            )}
            <p className="text-xs text-text-muted">{t('sheet.howItWorks')}</p>
            {error && <p role="alert" className="text-sm text-danger">{error}</p>}
            <Button className="w-full" isLoading={busy} onClick={reserve}>{t('sheet.confirm')}</Button>
          </div>
        ))}
      </BottomSheet>
    </section>
  );
}
