import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, Gauge, Loader2, RefreshCw } from 'lucide-react';
import { planMetrics, type PlanMetricsResult } from '../api';
import { useFormat } from '../i18n/format';

type Status = 'ok' | 'low' | 'alarm' | 'none';

/** Obiettivi del piano di lancio ("Come misuriamo"): giorno 30, giorno 90, soglia di allarme. */
const TARGETS = {
  match: { d30: 0.4, d90: 0.6, alarm: 0.25, percent: true },
  wau: { d30: 150, d90: 400, alarm: 100, percent: false },
  trades: { d30: 20, d90: 100, alarm: 30, percent: false },
  retention: { d30: 0.25, d90: 0.35, alarm: 0.15, percent: true },
  stores: { d30: 8, d90: 12, alarm: 6, percent: false },
  broadcasts: { d30: 10, d90: 40, alarm: null, percent: false },
} as const;
type MetricKey = keyof typeof TARGETS;

const STATUS_CLS: Record<Status, string> = {
  ok: 'bg-green-50 text-green-700',
  low: 'bg-amber-50 text-amber-700',
  alarm: 'bg-red-50 text-red-700',
  none: 'bg-surface-dark text-text-muted',
};

function statusOf(key: MetricKey, value: number | null): Status {
  if (value === null) return 'none';
  const tg = TARGETS[key];
  if (tg.alarm !== null && value < tg.alarm) return 'alarm';
  return value >= tg.d30 ? 'ok' : 'low';
}

/** Metriche del piano di lancio per gli amministratori, per città. */
export default function AdminMetricsPage() {
  const { t } = useTranslation('admin');
  const { date } = useFormat();
  const navigate = useNavigate();
  const [city, setCity] = useState('');
  const [data, setData] = useState<PlanMetricsResult | null>(null);
  const [error, setError] = useState(false);

  const load = useCallback(() => {
    setError(false);
    planMetrics.get(city).then(({ data }) => setData(data)).catch(() => setError(true));
  }, [city]);
  useEffect(() => { load(); }, [load]);

  const fmt = (key: MetricKey, v: number) => (TARGETS[key].percent ? `${Math.round(v * 100)}%` : String(v));

  const cards: { key: MetricKey; value: number | null; detail?: string }[] = data ? [
    { key: 'match', value: data.matchWithin48h.rate ?? null, detail: t('metrics.of', { count: data.matchWithin48h.count, total: data.matchWithin48h.eligible }) },
    { key: 'wau', value: data.weeklyActiveUsers },
    { key: 'trades', value: data.tradesCompletedLastWeek },
    { key: 'retention', value: data.retentionAfter4Weeks.rate ?? null, detail: t('metrics.of', { count: data.retentionAfter4Weeks.count, total: data.retentionAfter4Weeks.eligible }) },
    { key: 'stores', value: data.activeStores },
    { key: 'broadcasts', value: data.broadcastsLastWeek },
  ] : [];

  return (
    <div className="pb-24 space-y-4 max-w-3xl">
      <div className="flex items-center gap-3">
        <button onClick={() => navigate(-1)} aria-label={t('common:actions.back')} className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center shrink-0"><ArrowLeft size={18} /></button>
        <h1 className="text-xl font-bold text-text flex items-center gap-2 flex-1"><Gauge size={20} className="text-primary" /> {t('metrics.title')}</h1>
        <button onClick={load} aria-label={t('monitoring.refresh')} className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center"><RefreshCw size={16} /></button>
      </div>

      <label className="block">
        <span className="sr-only">{t('metrics.allCities')}</span>
        <select value={city} onChange={(e) => setCity(e.target.value)}
          className="w-full sm:w-auto px-3 py-2 rounded-xl border border-border bg-white text-sm">
          <option value="">{t('metrics.allCities')}</option>
          {data?.cities.map((c) => <option key={c.city} value={c.city}>{c.city} · {t('metrics.users', { count: c.users })}</option>)}
          {city && data && !data.cities.some((c) => c.city === city) && <option value={city}>{city}</option>}
        </select>
      </label>

      <p className="text-xs text-text-secondary">{t('metrics.targetsNote')}</p>

      {error && <p role="alert" className="text-sm p-3 rounded-xl bg-red-50 text-red-700">{t('metrics.loadError')}</p>}
      {!data && !error && <div className="flex justify-center py-12"><Loader2 className="animate-spin text-primary" /></div>}

      {data && (
        <>
          <section className="grid sm:grid-cols-2 gap-2">
            {cards.map(({ key, value, detail }) => {
              const tg = TARGETS[key];
              const status = statusOf(key, value);
              return (
                <div key={key} className="bg-white rounded-2xl border border-border/60 p-4">
                  <div className="flex items-start justify-between gap-2">
                    <h2 className="text-sm font-semibold text-text">{t(`metrics.m.${key}.label`)}</h2>
                    <span className={`shrink-0 text-[11px] font-medium px-2 py-0.5 rounded-full ${STATUS_CLS[status]}`}>{t(`metrics.status.${status}`)}</span>
                  </div>
                  <p className="mt-1 text-3xl font-bold text-text tabular-nums">{value === null ? '–' : fmt(key, value)}</p>
                  {value === null ? (
                    <p className="text-xs text-text-muted">{t('metrics.notEnough')}</p>
                  ) : detail && <p className="text-xs text-text-secondary tabular-nums">{detail}</p>}
                  <p className="mt-2 text-xs text-text-secondary">
                    {t('metrics.target', { d30: fmt(key, tg.d30), d90: fmt(key, tg.d90) })}
                    {tg.alarm !== null && <> · {t('metrics.alarm', { value: fmt(key, tg.alarm) })}</>}
                  </p>
                  <p className="mt-1 text-[11px] text-text-muted">{t(`metrics.m.${key}.hint`)}</p>
                </div>
              );
            })}
          </section>

          <section className="bg-white rounded-2xl border border-border/60 p-4">
            <h2 className="font-semibold text-text">{t('metrics.weeks')}</h2>
            <div className="mt-2 overflow-x-auto">
              <table className="w-full text-sm tabular-nums">
                <thead>
                  <tr className="text-xs text-text-secondary text-right">
                    <th className="text-left font-medium py-1.5">{t('metrics.week')}</th>
                    <th className="font-medium">{t('metrics.activeUsers')}</th>
                    <th className="font-medium">{t('metrics.newUsers')}</th>
                    <th className="font-medium">{t('metrics.tradesCompleted')}</th>
                    <th className="font-medium">{t('metrics.broadcasts')}</th>
                  </tr>
                </thead>
                <tbody>
                  {[...data.weeks].reverse().map((w) => (
                    <tr key={w.from} className="border-t border-border/60 text-right">
                      <td className="text-left py-1.5 text-text-secondary">{date(w.from, { day: 'numeric', month: 'short' })}</td>
                      <td className="text-text">{w.activeUsers}</td>
                      <td className="text-text">{w.newUsers}</td>
                      <td className="text-text">{w.tradesCompleted}</td>
                      <td className="text-text">{w.broadcasts}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        </>
      )}
    </div>
  );
}
