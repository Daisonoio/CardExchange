import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Activity, ArrowLeft, CheckCircle2, ChevronDown, ChevronUp, Loader2, RefreshCw, XCircle } from 'lucide-react';
import { monitoring, type MonitoringOverview } from '../api';
import { useFormat } from '../i18n/format';

const CATALOG_NAMES: Record<string, string> = { magic: 'Magic', pokemon: 'Pokémon', yugioh: 'Yu-Gi-Oh!', onepiece: 'One Piece', lorcana: 'Disney Lorcana' };

/** Monitoraggio per gli amministratori: errori raggruppati e stato dei cataloghi esterni. */
export default function AdminMonitoringPage() {
  const { t } = useTranslation('admin');
  const { dateTime, number } = useFormat();
  const navigate = useNavigate();
  const [data, setData] = useState<MonitoringOverview | null>(null);
  const [includeResolved, setIncludeResolved] = useState(false);
  const [open, setOpen] = useState<number | null>(null);
  const [error, setError] = useState(false);

  const load = useCallback(() => {
    setError(false);
    monitoring.overview(includeResolved).then(({ data }) => setData(data)).catch(() => setError(true));
  }, [includeResolved]);
  useEffect(() => { load(); }, [load]);

  const resolve = async (id: number) => {
    await monitoring.resolve(id);
    load();
  };

  const when = (iso?: string | null) => (iso ? dateTime(iso, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }) : '–');

  return (
    <div className="pb-24 space-y-4 max-w-3xl">
      <div className="flex items-center gap-3">
        <button onClick={() => navigate(-1)} aria-label={t('common:actions.back')} className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center shrink-0"><ArrowLeft size={18} /></button>
        <h1 className="text-xl font-bold text-text flex items-center gap-2 flex-1"><Activity size={20} className="text-primary" /> {t('monitoring.title')}</h1>
        <button onClick={load} aria-label={t('monitoring.refresh')} className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center"><RefreshCw size={16} /></button>
      </div>

      {error && <p role="alert" className="text-sm p-3 rounded-xl bg-red-50 text-red-700">{t('monitoring.loadError')}</p>}
      {!data && !error && <div className="flex justify-center py-12"><Loader2 className="animate-spin text-primary" /></div>}

      {data && (
        <>
          <section className="bg-white rounded-2xl border border-border/60 p-4 space-y-2">
            <h2 className="font-semibold text-text">{t('monitoring.catalogs')}</h2>
            {data.catalogs.length === 0 ? (
              <p className="text-sm text-text-muted">{t('monitoring.noChecksYet')}</p>
            ) : (
              <ul className="grid sm:grid-cols-2 gap-2">
                {data.catalogs.map((c) => (
                  <li key={c.key} className={`flex items-start gap-2 p-2.5 rounded-xl ${c.isUp ? 'bg-green-50' : 'bg-red-50'}`}>
                    {c.isUp ? <CheckCircle2 size={18} className="text-green-600 shrink-0 mt-0.5" /> : <XCircle size={18} className="text-red-600 shrink-0 mt-0.5" />}
                    <span className="min-w-0">
                      <span className="block text-sm font-semibold text-text">{CATALOG_NAMES[c.key] ?? c.key}</span>
                      <span className="block text-xs text-text-secondary">
                        {c.isUp ? t('monitoring.up', { when: when(c.lastOkAt) }) : t('monitoring.down', { count: c.consecutiveFailures, when: when(c.lastOkAt) })}
                      </span>
                      {!c.isUp && c.lastError && <span className="block text-xs text-red-700 break-words">{c.lastError}</span>}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className="space-y-2">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h2 className="font-semibold text-text">{t('monitoring.errors', { open: data.openGroups, recent: data.seenLast24h })}</h2>
              <label className="flex items-center gap-2 text-sm text-text-secondary">
                <input type="checkbox" checked={includeResolved} onChange={(e) => setIncludeResolved(e.target.checked)} className="accent-primary" />
                {t('monitoring.showResolved')}
              </label>
            </div>
            {data.errors.length === 0 ? (
              <p className="text-sm p-4 rounded-2xl bg-white border border-border/60 text-text-muted">{t('monitoring.noErrors')}</p>
            ) : (
              <ul className="space-y-2">
                {data.errors.map((e) => (
                  <li key={e.id} className="bg-white rounded-2xl border border-border/60 overflow-hidden">
                    <button type="button" onClick={() => setOpen(open === e.id ? null : e.id)} aria-expanded={open === e.id} className="w-full text-left p-3 flex gap-3 items-start">
                      <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full shrink-0 mt-0.5 ${e.source === 'Client' ? 'bg-purple-50 text-purple-700' : 'bg-blue-50 text-blue-700'}`}>
                        {e.source === 'Client' ? t('monitoring.client') : t('monitoring.server')}
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block text-sm font-medium text-text break-words">{e.message}</span>
                        <span className="block text-xs text-text-muted">
                          {t('monitoring.times', { count: e.count, n: number(e.count) })} · {t('monitoring.lastSeen', { when: when(e.lastSeenAt) })}{e.path ? ` · ${e.path}` : ''}
                        </span>
                      </span>
                      {open === e.id ? <ChevronUp size={16} className="text-text-muted shrink-0" /> : <ChevronDown size={16} className="text-text-muted shrink-0" />}
                    </button>
                    {open === e.id && (
                      <div className="px-3 pb-3 space-y-2">
                        <p className="text-xs text-text-muted">{e.category} · {t('monitoring.firstSeen', { when: when(e.firstSeenAt) })}</p>
                        {e.detail && <pre className="text-[11px] leading-snug bg-surface-dark rounded-xl p-2 overflow-x-auto max-h-64">{e.detail}</pre>}
                        {!e.isResolved && (
                          <button type="button" onClick={() => resolve(e.id)} className="text-sm font-semibold text-primary hover:underline">{t('monitoring.resolve')}</button>
                        )}
                      </div>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      )}
    </div>
  );
}
