import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, Loader2, TrendingDown, TrendingUp, Users } from 'lucide-react';
import { Bar, BarChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { stores } from '../api';
import type { StoreReports, StoreStats } from '../types';
import { useFormat } from '../i18n/format';
import EmptyState from '../components/ui/EmptyState';

const METRICS: (keyof StoreStats)[] = [
  'tradesCompletedAtStore', 'eventCheckIns', 'eventRegistrations', 'meetupsScheduled',
  'newFollowers', 'qrSignups', 'qrScans', 'profileViews',
];

function Delta({ now, before }: { now: number; before?: number }) {
  if (before === undefined || now === before) return null;
  const up = now > before;
  const Icon = up ? TrendingUp : TrendingDown;
  return (
    <span className={`inline-flex items-center gap-0.5 text-[11px] font-medium ${up ? 'text-green-700' : 'text-red-600'}`}>
      <Icon size={12} /> {up ? '+' : ''}{now - before}
    </span>
  );
}

/** Risultati del negozio per il proprietario: persone portate in negozio e altri valori, mese per mese. */
export default function StoreResultsPage() {
  const { t } = useTranslation('stores');
  const { id } = useParams<{ id: string }>();
  const { locale } = useFormat();
  const [data, setData] = useState<StoreReports | null | undefined>(undefined);

  useEffect(() => {
    stores.reports(Number(id)).then(({ data }) => setData(data)).catch(() => setData(null));
  }, [id]);

  if (data === undefined) return <div className="flex justify-center py-16"><Loader2 className="animate-spin text-primary" /></div>;
  if (data === null) return <EmptyState icon={Users} title={t('results.forbidden')} />;

  const monthLabel = (ym: string, long = false) =>
    new Date(`${ym}-01T12:00:00`).toLocaleDateString(locale, long ? { month: 'long', year: 'numeric' } : { month: 'short' });
  const last = data.months[0];
  const chart = [...data.months].reverse().slice(-5)
    .map((m) => ({ month: monthLabel(m.month), people: m.stats?.peopleBroughtIn ?? 0 }))
    .concat({ month: monthLabel(data.current.month), people: data.current.stats.peopleBroughtIn });

  return (
    <div className="max-w-2xl mx-auto space-y-4">
      <div className="flex items-center gap-2">
        <Link to={`/stores/${data.store.id}`} className="p-1.5 -ml-1.5 rounded-lg hover:bg-surface-hover" aria-label={data.store.name}>
          <ArrowLeft size={20} />
        </Link>
        <div className="min-w-0">
          <h1 className="text-xl font-bold text-text">{t('results.title')}</h1>
          <p className="text-sm text-text-secondary truncate">{data.store.name}</p>
        </div>
      </div>

      <section className="p-4 rounded-2xl bg-primary text-white">
        <p className="text-xs uppercase tracking-wide text-white/70">{t('results.thisMonth')}</p>
        <p className="mt-1 flex items-baseline gap-2">
          <span className="text-4xl font-bold tabular-nums">{data.current.stats.peopleBroughtIn}</span>
          <span className="text-sm">{t('results.people', { count: data.current.stats.peopleBroughtIn })}</span>
        </p>
        <p className="mt-2 text-xs text-white/80">{t('results.peopleHint')}</p>
      </section>

      <section className="grid grid-cols-2 gap-2">
        {METRICS.map((key) => (
          <div key={key} className="p-3 rounded-xl bg-white border border-border">
            <p className="text-xs text-text-secondary">{t(`results.metrics.${key}`)}</p>
            <p className="mt-0.5 text-lg font-semibold text-text tabular-nums">{data.current.stats[key]}</p>
          </div>
        ))}
      </section>

      <section className="p-4 rounded-2xl bg-white border border-border">
        <h2 className="text-sm font-semibold text-text">{t('results.history')}</h2>
        {data.months.length === 0 ? (
          <p className="mt-1 text-sm text-text-secondary">{t('results.noHistory')}</p>
        ) : (
          <>
            <div className="h-40 mt-3" aria-hidden>
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={chart} margin={{ top: 4, right: 4, left: -24, bottom: 0 }}>
                  <XAxis dataKey="month" tick={{ fontSize: 11 }} tickLine={false} axisLine={false} />
                  <YAxis allowDecimals={false} tick={{ fontSize: 11 }} tickLine={false} axisLine={false} />
                  <Tooltip cursor={{ fill: 'rgba(0,0,0,0.04)' }} formatter={(v) => [v, t('results.people', { count: Number(v) })]} />
                  <Bar dataKey="people" fill="var(--color-primary)" radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
            {last?.stats && (
              <div className="mt-3 border-t border-border pt-3">
                <p className="text-sm font-medium text-text capitalize">{monthLabel(last.month, true)}</p>
                <ul className="mt-1 space-y-1">
                  {(['peopleBroughtIn', ...METRICS] as (keyof StoreStats)[]).map((key) => (
                    <li key={key} className="flex items-center justify-between gap-2 text-sm">
                      <span className="text-text-secondary">
                        {key === 'peopleBroughtIn' ? t('results.peopleLabel') : t(`results.metrics.${key}`)}
                      </span>
                      <span className="flex items-center gap-2 tabular-nums">
                        <Delta now={last.stats![key]} before={last.previous?.[key]} />
                        <strong className="text-text">{last.stats![key]}</strong>
                      </span>
                    </li>
                  ))}
                </ul>
                <p className="mt-1 text-[11px] text-text-muted">{t('results.vsPrevious')}</p>
              </div>
            )}
          </>
        )}
      </section>

      <p className="text-xs text-text-muted">{t('results.privacy')}</p>
    </div>
  );
}
