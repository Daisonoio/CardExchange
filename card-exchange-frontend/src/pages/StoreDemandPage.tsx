import { useEffect, useState } from 'react';
import { Link, Navigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, BellRing, Download, Loader2, Lock, Target, Users } from 'lucide-react';
import { stores } from '../api';
import type { DemandCard, StoreDemand } from '../types';
import { useFormat } from '../i18n/format';
import { apiErrorMessage } from '../utils/apiError';
import EmptyState from '../components/ui/EmptyState';
import Button from '../components/ui/Button';
import { useEntitlements } from '../hooks/useEntitlements';

type State = { kind: 'loading' } | { kind: 'ok'; data: StoreDemand } | { kind: 'plan' } | { kind: 'error'; message: string };

function downloadCsv(rows: DemandCard[], filename: string) {
  const esc = (v: unknown) => `"${String(v ?? '').replace(/"/g, '""')}"`;
  const lines = [['Carta', 'Espansione', 'Persone', 'Prezzo massimo medio (EUR)', 'Valore di mercato (EUR)'].map(esc).join(';')]
    .concat(rows.map((r) => [r.name, r.setName, r.users, r.avgMaxPriceEur ?? '', r.marketPriceEur ?? ''].map(esc).join(';')));
  const blob = new Blob(['﻿' + lines.join('\n')], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

/** Domanda locale per il negozio: carte cercate in zona tra quelle che ha e quelle che non ha. Solo numeri, mai nomi. */
export default function StoreDemandPage() {
  const { t } = useTranslation('stores');
  const { paidPlans } = useEntitlements();
  const { id } = useParams<{ id: string }>();
  const { euro, dateTime } = useFormat();
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [sending, setSending] = useState<number | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    stores.demand(Number(id))
      .then(({ data }) => setState({ kind: 'ok', data }))
      .catch((err) => {
        const status = (err as { response?: { status?: number } })?.response?.status;
        setState(status === 402 ? { kind: 'plan' } : { kind: 'error', message: apiErrorMessage(err, t('results.forbidden')) });
      });
  }, [id, t]);

  const notify = async (card: DemandCard) => {
    if (!confirm(t('demand.notifyConfirm', { name: card.name }))) return;
    setSending(card.cardInfoId);
    setNotice(null);
    try {
      const { data } = await stores.notifyInterested(Number(id), card.cardInfoId);
      setNotice(data.sent > 0 ? t('demand.notifySent', { count: data.sent }) : t('demand.notifyNone'));
    } catch (err) {
      setNotice(apiErrorMessage(err, t('demand.notifyNone')));
    } finally {
      setSending(null);
    }
  };

  const header = (
    <div className="flex items-center gap-2">
      <Link to={`/stores/${id}`} className="p-1.5 -ml-1.5 rounded-lg hover:bg-surface-hover" aria-label={t('common:actions.back')}><ArrowLeft size={20} /></Link>
      <h1 className="text-xl font-bold text-text flex items-center gap-2"><Target size={20} className="text-primary" /> {t('demand.title')}</h1>
    </div>
  );

  if (state.kind === 'loading') return <div className="flex justify-center py-16"><Loader2 className="animate-spin text-primary" /></div>;
  if (state.kind === 'error') return <EmptyState icon={Users} title={state.message} />;
  if (state.kind === 'plan') {
    if (!paidPlans) return <Navigate to="/" replace />;
    return (
      <div className="max-w-2xl mx-auto space-y-4">
        {header}
        <section className="p-5 rounded-2xl bg-white border border-border text-center space-y-3">
          <Lock size={28} className="mx-auto text-primary" />
          <h2 className="text-lg font-bold text-text">{t('demand.planTitle')}</h2>
          <p className="text-sm text-text-secondary">{t('demand.planText')}</p>
          <Link to="/premium" className="inline-block px-4 py-2 rounded-xl bg-primary text-white text-sm font-semibold">{t('demand.planCta')}</Link>
        </section>
      </div>
    );
  }

  const d = state.data;
  const row = (c: DemandCard, inStock: boolean) => (
    <li key={c.cardInfoId} className="flex gap-3 py-3">
      {c.imageUrl
        ? <img src={c.imageUrl} alt="" loading="lazy" className="w-12 h-[67px] object-cover rounded-md bg-surface-dark shrink-0" onError={(e) => { e.currentTarget.style.visibility = 'hidden'; }} />
        : <div className="w-12 h-[67px] rounded-md bg-surface-dark shrink-0" />}
      <div className="flex-1 min-w-0">
        <p className="text-sm font-semibold text-text truncate">{c.name}</p>
        <p className="text-xs text-text-muted truncate">{c.setName}</p>
        <p className="text-xs text-text-secondary mt-0.5">
          <strong className="text-text">{t('demand.people', { count: c.users })}</strong>
          {c.avgMaxPriceEur != null && <> · {t('demand.avgMax', { price: euro(c.avgMaxPriceEur) })}</>}
          {c.marketPriceEur != null && <> · {t('demand.market', { price: euro(c.marketPriceEur) })}</>}
          {inStock && <> · {t('demand.copies', { count: c.copiesInStock })}</>}
        </p>
        {inStock && (
          <button type="button" disabled={sending !== null} onClick={() => notify(c)}
            className="mt-1.5 inline-flex items-center gap-1.5 text-xs font-semibold text-primary disabled:opacity-50">
            {sending === c.cardInfoId ? <Loader2 size={13} className="animate-spin" /> : <BellRing size={13} />} {t('demand.notify')}
          </button>
        )}
      </div>
    </li>
  );

  return (
    <div className="max-w-2xl mx-auto space-y-4 pb-24">
      {header}
      <p className="text-sm text-text-secondary -mt-2">{t('demand.subtitle', { km: d.radiusKm })}</p>

      <section className="p-4 rounded-2xl bg-primary text-white">
        <p className="text-3xl font-bold tabular-nums">{d.interestedUsers || '–'}</p>
        <p className="text-sm">{d.interestedUsers ? t('demand.interested', { count: d.interestedUsers }) : t('demand.interestedNone')}</p>
        <p className="mt-2 text-xs text-white/80">{t('demand.active', { count: d.activeUsersInRadius })}</p>
      </section>

      {notice && <p role="status" className="text-sm p-3 rounded-xl bg-green-50 text-green-800">{notice}</p>}

      <section className="bg-white rounded-2xl border border-border p-4">
        <h2 className="text-sm font-semibold text-text">{t('demand.inStock')}</h2>
        {d.inStock.length === 0 ? <p className="mt-1 text-sm text-text-muted">{t('demand.empty')}</p> : <ul className="divide-y divide-border/60">{d.inStock.map((c) => row(c, true))}</ul>}
      </section>

      <section className="bg-white rounded-2xl border border-border p-4">
        <div className="flex items-start justify-between gap-2">
          <div>
            <h2 className="text-sm font-semibold text-text">{t('demand.missing')}</h2>
            <p className="text-xs text-text-muted">{t('demand.missingHint')}</p>
          </div>
          {d.missing.length > 0 && (
            <Button variant="outline" onClick={() => downloadCsv(d.missing, 'carte-cercate-in-zona.csv')} className="shrink-0 !px-3 !py-1.5 text-xs">
              <Download size={14} /> {t('demand.csv')}
            </Button>
          )}
        </div>
        {d.missing.length === 0 ? <p className="mt-1 text-sm text-text-muted">{t('demand.empty')}</p> : <ul className="divide-y divide-border/60">{d.missing.map((c) => row(c, false))}</ul>}
      </section>

      <p className="text-xs text-text-muted">{t('demand.privacy')}</p>
      <p className="text-[11px] text-text-muted">{t('demand.updated', { when: dateTime(d.computedAt, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }) })}</p>
    </div>
  );
}
