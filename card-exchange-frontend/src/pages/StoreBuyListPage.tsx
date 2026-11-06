import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, FileUp, Loader2, Lock, MessageCircle, Plus, ShoppingCart } from 'lucide-react';
import { buyList, catalog, collectionImport, scryfall } from '../api';
import type { BuyOrderInput, StoreBuyOrder } from '../types';
import { CARD_LANGUAGES, CONDITION_LABELS, FINISH_LABELS } from '../types';
import { useGame } from '../context/GameContext';
import { useFormat } from '../i18n/format';
import { apiErrorMessage } from '../utils/apiError';
import { readPrices } from '../utils/buyListCsv';
import GameCardSearch, { importGameCard } from '../components/cards/GameCardSearch';
import Button from '../components/ui/Button';

type GameCardPick = { externalId: string; name: string; imageUrl?: string; setName?: string };

const emptyForm = { minCondition: 5, finish: 0, language: '', cash: '', credit: '', quantity: 1 };

/** Lista acquisti del negozio: cosa compra, a che prezzo, proposte ricevute e consegne. */
export default function StoreBuyListPage() {
  const { t } = useTranslation('buylist');
  const { id } = useParams<{ id: string }>();
  const storeId = Number(id);
  const { selectedGame } = useGame();
  const { euro, dateTime } = useFormat();
  const [orders, setOrders] = useState<StoreBuyOrder[] | null>(null);
  const [needsPlan, setNeedsPlan] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [adding, setAdding] = useState<GameCardPick | null>(null);
  const [showSearch, setShowSearch] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [busy, setBusy] = useState(false);
  const [importMsg, setImportMsg] = useState<string | null>(null);

  const load = useCallback(() => {
    buyList.list(storeId).then(({ data }) => setOrders(data)).catch((err) => {
      if ((err as { response?: { status?: number } })?.response?.status === 402) setNeedsPlan(true);
      else setError(apiErrorMessage(err, t('store.empty')));
      setOrders([]);
    });
  }, [storeId, t]);
  useEffect(() => { load(); }, [load]);

  const toInput = (cardInfoId: number): BuyOrderInput => ({
    cardInfoId,
    minCondition: form.minCondition,
    finish: form.finish || null,
    language: form.language || null,
    priceCashEur: form.cash ? Number(form.cash) : null,
    priceCreditEur: form.credit ? Number(form.credit) : null,
    quantityWanted: form.quantity,
  });

  const save = async () => {
    if (!adding) return;
    setBusy(true);
    setError(null);
    try {
      const cardInfoId = await importGameCard(selectedGame?.name, adding.externalId, selectedGame?.catalogKey);
      await buyList.create(storeId, toInput(cardInfoId));
      setAdding(null);
      setShowSearch(false);
      setForm(emptyForm);
      load();
    } catch (err) {
      setError(apiErrorMessage(err, t('store.empty')));
    } finally {
      setBusy(false);
    }
  };

  const importCsv = async (file: File) => {
    setBusy(true);
    setError(null);
    setImportMsg(t('store.importing'));
    try {
      const text = await file.text();
      const prices = readPrices(text);
      const key = selectedGame?.catalogKey && selectedGame.catalogKey !== 'magic' ? selectedGame.catalogKey : undefined;
      const { data } = await collectionImport.preview(text, key);
      let created = 0;
      const failed: number[] = [];
      for (const row of data.rows) {
        const p = prices.get(row.lineNumber) ?? {};
        if (row.status !== 'ready' || !row.match || (p.cash == null && p.credit == null)) { failed.push(row.lineNumber); continue; }
        try {
          const m = row.match;
          const cardInfoId = m.catalogKey && m.catalogKey !== 'magic' && m.externalId
            ? (await catalog.importCard(m.catalogKey, m.externalId)).data.cardInfoId
            : (await scryfall.importCard(m.scryfallId!)).data.cardInfoId;
          await buyList.create(storeId, {
            cardInfoId, minCondition: row.condition, finish: null, language: row.language ?? null,
            priceCashEur: p.cash ?? null, priceCreditEur: p.credit ?? null, quantityWanted: Math.min(100, row.quantity),
          });
          created++;
        } catch {
          failed.push(row.lineNumber);
        }
      }
      setImportMsg([t('store.imported', { count: created }), failed.length ? t('store.importErrors', { lines: failed.join(', ') }) : ''].filter(Boolean).join(' · '));
      load();
    } catch (err) {
      setImportMsg(null);
      setError(apiErrorMessage(err, t('store.importCsv')));
    } finally {
      setBusy(false);
    }
  };

  const toggle = async (o: StoreBuyOrder) => {
    try {
      await buyList.update(storeId, o.id, {
        cardInfoId: o.cardInfoId, minCondition: o.minCondition, finish: o.finish ?? null, language: o.language ?? null,
        priceCashEur: o.priceCashEur ?? null, priceCreditEur: o.priceCreditEur ?? null, quantityWanted: o.quantityWanted,
        expiresAt: o.expiresAt ?? null, isActive: !o.isActive,
      });
      load();
    } catch (err) {
      setError(apiErrorMessage(err, t('store.pause')));
    }
  };

  const offerAction = async (fn: () => Promise<unknown>) => {
    setError(null);
    try { await fn(); load(); } catch (err) { setError(apiErrorMessage(err, t('store.offers'))); }
  };

  const label = 'block text-xs font-semibold text-text-secondary';
  const inputCls = 'mt-1 w-full px-3 py-2 rounded-xl border border-border text-sm font-normal text-text bg-white';
  const header = (
    <div className="flex items-center gap-2">
      <Link to={`/stores/${storeId}`} className="p-1.5 -ml-1.5 rounded-lg hover:bg-surface-hover" aria-label={t('common:actions.back')}><ArrowLeft size={20} /></Link>
      <h1 className="text-xl font-bold text-text flex items-center gap-2"><ShoppingCart size={20} className="text-primary" /> {t('store.title')}</h1>
    </div>
  );

  if (needsPlan) {
    return (
      <div className="max-w-2xl mx-auto space-y-4">
        {header}
        <section className="p-5 rounded-2xl bg-white border border-border text-center space-y-3">
          <Lock size={28} className="mx-auto text-primary" />
          <h2 className="text-lg font-bold text-text">{t('store.planTitle')}</h2>
          <p className="text-sm text-text-secondary">{t('store.planText')}</p>
          <Link to="/premium" className="inline-block px-4 py-2 rounded-xl bg-primary text-white text-sm font-semibold">{t('store.planCta')}</Link>
        </section>
      </div>
    );
  }

  const priceLine = (o: { priceCashEur?: number | null; priceCreditEur?: number | null }) =>
    o.priceCreditEur != null && o.priceCashEur != null ? t('sell.price', { credit: euro(o.priceCreditEur), cash: euro(o.priceCashEur) })
      : o.priceCreditEur != null ? t('sell.priceCredit', { credit: euro(o.priceCreditEur) })
      : o.priceCashEur != null ? t('sell.priceCash', { cash: euro(o.priceCashEur) }) : '';

  return (
    <div className="max-w-2xl mx-auto space-y-4 pb-24">
      {header}
      <p className="text-sm text-text-secondary -mt-2">{t('store.intro')}</p>
      {error && <p role="alert" className="text-sm p-3 rounded-xl bg-red-50 text-red-700">{error}</p>}

      <div className="flex flex-wrap gap-2">
        <Button onClick={() => { setShowSearch((v) => !v); setAdding(null); }}><Plus size={16} /> {t('store.add')}</Button>
        <label className={`inline-flex items-center gap-1.5 px-4 py-2 rounded-xl border border-border bg-white text-sm font-semibold text-text cursor-pointer ${busy ? 'opacity-60 pointer-events-none' : ''}`}>
          <FileUp size={16} /> {t('store.importCsv')}
          <input type="file" accept=".csv,text/csv" className="sr-only" onChange={(e) => { const f = e.target.files?.[0]; if (f) void importCsv(f); e.target.value = ''; }} />
        </label>
      </div>
      <p className="text-xs text-text-muted -mt-2">{t('store.importHint')}</p>
      {importMsg && <p role="status" className="text-sm p-3 rounded-xl bg-surface-dark text-text-secondary">{importMsg}</p>}

      {showSearch && !adding && (
        <section className="bg-white rounded-2xl border border-border p-3">
          <GameCardSearch onSelect={(c) => setAdding(c as GameCardPick)} placeholder={t('store.search')} />
        </section>
      )}

      {adding && (
        <section className="bg-white rounded-2xl border border-border p-3 space-y-2">
          <div className="flex gap-3 items-center">
            {adding.imageUrl && <img src={adding.imageUrl} alt="" className="w-12 h-[67px] object-cover rounded-md" />}
            <div>
              <p className="text-sm font-semibold text-text">{adding.name}</p>
              {adding.setName && <p className="text-xs text-text-muted">{adding.setName}</p>}
            </div>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <label className={label}>{t('store.minCondition')}
              <select value={form.minCondition} onChange={(e) => setForm({ ...form, minCondition: Number(e.target.value) })} className={inputCls}>
                {([1, 2, 3, 4, 5, 6, 7, 8] as const).map((c) => <option key={c} value={c}>{CONDITION_LABELS[c]}</option>)}
              </select>
            </label>
            <label className={label}>{t('store.quantity')}
              <input type="number" min={1} max={100} value={form.quantity} onChange={(e) => setForm({ ...form, quantity: Math.max(1, Math.min(100, Number(e.target.value) || 1)) })} className={inputCls} />
            </label>
            <label className={label}>{t('store.finish')}
              <select value={form.finish} onChange={(e) => setForm({ ...form, finish: Number(e.target.value) })} className={inputCls}>
                <option value={0}>{t('store.any')}</option>
                {([1, 2, 3, 4] as const).map((f) => <option key={f} value={f}>{FINISH_LABELS[f]}</option>)}
              </select>
            </label>
            <label className={label}>{t('store.language')}
              <select value={form.language} onChange={(e) => setForm({ ...form, language: e.target.value })} className={inputCls}>
                <option value="">{t('store.any')}</option>
                {CARD_LANGUAGES.map((l) => <option key={l.code} value={l.code}>{l.label}</option>)}
              </select>
            </label>
            <label className={label}>{t('store.credit')}
              <input type="number" min={0} step="0.5" inputMode="decimal" value={form.credit} onChange={(e) => setForm({ ...form, credit: e.target.value })} className={inputCls} />
            </label>
            <label className={label}>{t('store.cash')}
              <input type="number" min={0} step="0.5" inputMode="decimal" value={form.cash} onChange={(e) => setForm({ ...form, cash: e.target.value })} className={inputCls} />
            </label>
          </div>
          <div className="flex gap-2">
            <Button className="flex-1" isLoading={busy} disabled={!form.cash && !form.credit} onClick={save}>{t('store.save')}</Button>
            <Button variant="outline" onClick={() => setAdding(null)}>{t('store.cancel')}</Button>
          </div>
        </section>
      )}

      {orders === null ? (
        <div className="flex justify-center py-12"><Loader2 className="animate-spin text-primary" /></div>
      ) : orders.length === 0 ? (
        <p className="text-sm text-text-muted">{t('store.empty')}</p>
      ) : (
        <ul className="space-y-2">
          {orders.map((o) => (
            <li key={o.id} className={`bg-white rounded-2xl border border-border p-3 space-y-2 ${o.isActive ? '' : 'opacity-70'}`}>
              <div className="flex gap-3">
                {o.imageUrl
                  ? <img src={o.imageUrl} alt="" loading="lazy" className="w-12 h-[67px] object-cover rounded-md bg-surface-dark shrink-0" onError={(e) => { e.currentTarget.style.visibility = 'hidden'; }} />
                  : <div className="w-12 h-[67px] rounded-md bg-surface-dark shrink-0" />}
                <div className="flex-1 min-w-0">
                  <div className="flex items-start justify-between gap-2">
                    <p className="text-sm font-semibold text-text truncate">{o.cardName}</p>
                    {o.quantityFilled >= o.quantityWanted
                      ? <span className="shrink-0 text-[11px] px-2 py-0.5 rounded-full bg-green-50 text-green-700">{t('store.completed')}</span>
                      : !o.isActive && <span className="shrink-0 text-[11px] px-2 py-0.5 rounded-full bg-surface-dark text-text-muted">{t('store.paused')}</span>}
                  </div>
                  <p className="text-xs text-text-muted truncate">
                    {[o.setName, `≥ ${CONDITION_LABELS[o.minCondition]}`, o.finish ? FINISH_LABELS[o.finish] : null, o.language?.toUpperCase()].filter(Boolean).join(' · ')}
                  </p>
                  <p className="text-xs text-text font-medium">{priceLine(o)}</p>
                  <p className="text-xs text-text-secondary">
                    {t('store.filled', { filled: o.quantityFilled, wanted: o.quantityWanted })}
                    {o.quantityPending > 0 && <> · {t('store.pending', { count: o.quantityPending })}</>}
                  </p>
                </div>
              </div>
              <div className="h-1.5 rounded-full bg-surface-dark overflow-hidden" aria-hidden>
                <div className="h-full bg-primary" style={{ width: `${Math.min(100, (o.quantityFilled / o.quantityWanted) * 100)}%` }} />
              </div>
              {o.offers.length > 0 && (
                <ul className="divide-y divide-border/60 border-t border-border/60">
                  {o.offers.map((x) => (
                    <li key={x.id} className="py-2 flex flex-wrap items-center gap-2">
                      <span className="flex-1 min-w-0 text-sm text-text">
                        @{x.username} · ×{x.quantity} · {CONDITION_LABELS[x.condition]}{x.language ? ` · ${x.language.toUpperCase()}` : ''}
                        <span className="block text-xs text-text-secondary">
                          {t(`store.status.${x.status}`)}
                          {x.status === 'Accepted' && x.dropOffBy && <> · {t('store.dropOffBy', { when: dateTime(x.dropOffBy, { day: 'numeric', month: 'short' }) })}</>}
                        </span>
                      </span>
                      {x.status === 'Proposed' && (
                        <>
                          <button type="button" onClick={() => offerAction(() => buyList.accept(x.id))} className="px-3 py-1.5 rounded-xl bg-primary text-white text-xs font-semibold">{t('store.accept')}</button>
                          <button type="button" onClick={() => offerAction(() => buyList.reject(x.id))} className="px-3 py-1.5 rounded-xl border border-border text-xs font-semibold text-text-secondary">{t('store.reject')}</button>
                        </>
                      )}
                      {x.status === 'Accepted' && (
                        <button type="button" onClick={() => offerAction(() => buyList.complete(x.id))} className="px-3 py-1.5 rounded-xl bg-green-600 text-white text-xs font-semibold">{t('store.complete')}</button>
                      )}
                      {(x.status === 'Proposed' || x.status === 'Accepted') && (
                        <Link to={`/chat?userId=${x.userId}`} className="inline-flex items-center gap-1 text-xs font-semibold text-primary"><MessageCircle size={13} /> {t('store.write')}</Link>
                      )}
                    </li>
                  ))}
                </ul>
              )}
              {o.quantityFilled < o.quantityWanted && (
                <button type="button" onClick={() => toggle(o)} className="text-xs font-semibold text-primary">{o.isActive ? t('store.pause') : t('store.resume')}</button>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
