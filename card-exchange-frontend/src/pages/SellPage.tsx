import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, Loader2, MessageCircle, Store as StoreIcon, Tag } from 'lucide-react';
import { buyList } from '../api';
import type { BuyOrderForMe } from '../types';
import { CONDITION_LABELS } from '../types';
import { useFormat } from '../i18n/format';
import { apiErrorMessage } from '../utils/apiError';
import EmptyState from '../components/ui/EmptyState';
import EmptyStateLink from '../components/ui/EmptyStateLink';

/** "Vendi ai negozi": richieste di negozi vicini che corrispondono a carte della mia collezione. */
export default function SellPage() {
  const { t } = useTranslation('buylist');
  const navigate = useNavigate();
  const { euro, dateTime } = useFormat();
  const [list, setList] = useState<BuyOrderForMe[] | null>(null);
  const [choice, setChoice] = useState<Record<number, { cardId: number; quantity: number }>>({});
  const [busy, setBusy] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    buyList.forMe().then(({ data }) => setList(data)).catch(() => setList([]));
  }, []);
  useEffect(() => { load(); }, [load]);

  const act = async (orderId: number, fn: () => Promise<unknown>) => {
    setBusy(orderId);
    setError(null);
    try { await fn(); load(); } catch (err) { setError(apiErrorMessage(err, t('sell.propose'))); } finally { setBusy(null); }
  };

  const price = (o: BuyOrderForMe) =>
    o.priceCreditEur != null && o.priceCashEur != null ? t('sell.price', { credit: euro(o.priceCreditEur), cash: euro(o.priceCashEur) })
      : o.priceCreditEur != null ? t('sell.priceCredit', { credit: euro(o.priceCreditEur) })
      : o.priceCashEur != null ? t('sell.priceCash', { cash: euro(o.priceCashEur) }) : '';

  return (
    <div className="pb-24 space-y-4 max-w-2xl">
      <div className="flex items-center gap-3">
        <button onClick={() => navigate(-1)} aria-label={t('common:actions.back')} className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center shrink-0"><ArrowLeft size={18} /></button>
        <h1 className="text-xl font-bold text-text flex items-center gap-2"><Tag size={20} className="text-primary" /> {t('sell.title')}</h1>
      </div>
      <p className="text-sm text-text-secondary">{t('sell.intro')}</p>
      {error && <p role="alert" className="text-sm p-3 rounded-xl bg-red-50 text-red-700">{error}</p>}
      {list === null ? (
        <div className="flex justify-center py-12"><Loader2 className="animate-spin text-primary" /></div>
      ) : list.length === 0 ? (
        <EmptyState icon={StoreIcon} title={t('sell.title')} description={`${t('sell.empty')} ${t('sell.adultsOnly')}`}
          action={<EmptyStateLink to="/collection" label={t('sell.emptyAction')} />} />
      ) : (
        <ul className="space-y-2">
          {list.map((o) => {
            const sel = choice[o.orderId] ?? { cardId: o.myCards[0]?.id, quantity: 1 };
            const card = o.myCards.find((c) => c.id === sel.cardId) ?? o.myCards[0];
            const max = Math.max(1, Math.min(o.remaining, card?.available ?? 1));
            const open = o.myOffer && (o.myOffer.status === 'Proposed' || o.myOffer.status === 'Accepted');
            return (
              <li key={o.orderId} className="bg-white rounded-2xl border border-border p-3 space-y-2">
                <div className="flex gap-3">
                  {o.imageUrl
                    ? <img src={o.imageUrl} alt="" loading="lazy" className="w-12 h-[67px] object-cover rounded-md bg-surface-dark shrink-0" onError={(e) => { e.currentTarget.style.visibility = 'hidden'; }} />
                    : <div className="w-12 h-[67px] rounded-md bg-surface-dark shrink-0" />}
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-semibold text-text truncate">{o.cardName}</p>
                    <p className="text-xs text-text-muted truncate">{[o.setName, `≥ ${CONDITION_LABELS[o.minCondition]}`, o.language?.toUpperCase()].filter(Boolean).join(' · ')}</p>
                    <Link to={`/stores/${o.storeId}`} className="text-xs text-text-secondary hover:underline">{o.storeName} · {o.distanceKm} km</Link>
                    <p className="text-sm font-semibold text-text">{price(o)}</p>
                    <p className="text-xs text-text-muted">{t('sell.remaining', { count: o.remaining })}</p>
                  </div>
                </div>
                {open ? (
                  <div className="rounded-xl bg-surface-dark p-2.5 space-y-1">
                    <p className="text-sm font-medium text-text">{t(`store.status.${o.myOffer!.status}`)} · ×{o.myOffer!.quantity}</p>
                    {o.myOffer!.status === 'Accepted' && o.myOffer!.dropOffBy && (
                      <p className="text-xs text-text-secondary first-letter:uppercase">{t('sell.dropOff', { when: dateTime(o.myOffer!.dropOffBy, { weekday: 'long', day: 'numeric', month: 'long' }) })}</p>
                    )}
                    {o.myOffer!.storeNote && <p className="text-xs text-text-secondary">{t('sell.note', { note: o.myOffer!.storeNote })}</p>}
                    <div className="flex gap-3 pt-1">
                      {o.myOffer!.status === 'Accepted' && (
                        <Link to={`/chat?userId=${o.storeOwnerId}`} className="inline-flex items-center gap-1 text-xs font-semibold text-primary"><MessageCircle size={13} /> {t('sell.writeStore')}</Link>
                      )}
                      <button type="button" disabled={busy === o.orderId} onClick={() => act(o.orderId, () => buyList.withdraw(o.myOffer!.id))} className="text-xs font-semibold text-danger">{t('sell.withdraw')}</button>
                    </div>
                  </div>
                ) : (
                  <div className="flex flex-wrap items-end gap-2">
                    <label className="flex-1 min-w-[10rem] text-xs font-semibold text-text-secondary">{t('sell.yourCopy')}
                      <select value={card?.id} onChange={(e) => setChoice({ ...choice, [o.orderId]: { cardId: Number(e.target.value), quantity: 1 } })}
                        className="mt-1 w-full px-3 py-2 rounded-xl border border-border text-sm font-normal bg-white">
                        {o.myCards.map((c) => (
                          <option key={c.id} value={c.id}>{CONDITION_LABELS[c.condition]}{c.language ? ` · ${c.language.toUpperCase()}` : ''} · ×{c.available}</option>
                        ))}
                      </select>
                    </label>
                    {max > 1 && (
                      <input type="number" min={1} max={max} value={sel.quantity} aria-label={t('store.quantity')}
                        onChange={(e) => setChoice({ ...choice, [o.orderId]: { cardId: card.id, quantity: Math.max(1, Math.min(max, Number(e.target.value) || 1)) } })}
                        className="w-16 px-2 py-2 rounded-xl border border-border text-sm" />
                    )}
                    <button type="button" disabled={busy === o.orderId || !card}
                      onClick={() => act(o.orderId, () => buyList.propose(o.orderId, card.id, sel.quantity))}
                      className="px-4 py-2 rounded-xl bg-primary text-white text-sm font-semibold disabled:opacity-50">
                      {t('sell.propose')}
                    </button>
                  </div>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
