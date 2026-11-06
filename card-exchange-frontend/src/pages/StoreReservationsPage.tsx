import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, Loader2, PackageCheck } from 'lucide-react';
import { reservations } from '../api';
import type { Reservation } from '../types';
import { apiErrorMessage } from '../utils/apiError';
import EmptyState from '../components/ui/EmptyState';
import ReservationCard from '../components/stores/ReservationCard';

/** Per il negozio: prenotazioni da preparare, ritiro con il codice del cliente, mancata presentazione. */
export default function StoreReservationsPage() {
  const { t } = useTranslation('reservations');
  const { id } = useParams<{ id: string }>();
  const storeId = Number(id);
  const [list, setList] = useState<Reservation[] | null>(null);
  const [forbidden, setForbidden] = useState(false);
  const [codes, setCodes] = useState<Record<number, string>>({});
  const [busy, setBusy] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    reservations.forStore(storeId).then(({ data }) => setList(data)).catch(() => { setForbidden(true); setList([]); });
  }, [storeId]);
  useEffect(() => { load(); }, [load]);

  const act = async (r: Reservation, fn: () => Promise<unknown>, question?: string) => {
    if (question && !confirm(question)) return;
    setBusy(r.id);
    setError(null);
    try {
      await fn();
      load();
    } catch (err) {
      setError(apiErrorMessage(err, t('store.empty')));
    } finally {
      setBusy(null);
    }
  };

  const active = list?.filter((r) => r.status === 'Pending' || r.status === 'Confirmed') ?? [];
  const past = list?.filter((r) => r.status !== 'Pending' && r.status !== 'Confirmed') ?? [];
  const btn = 'px-3 py-1.5 rounded-xl text-xs font-semibold disabled:opacity-50';

  return (
    <div className="pb-24 space-y-4 max-w-2xl">
      <div className="flex items-center gap-2">
        <Link to={`/stores/${storeId}`} className="p-1.5 -ml-1.5 rounded-lg hover:bg-surface-hover" aria-label={t('common:actions.back')}><ArrowLeft size={20} /></Link>
        <h1 className="text-xl font-bold text-text flex items-center gap-2"><PackageCheck size={20} className="text-primary" /> {t('storeTitle')}</h1>
      </div>
      {error && <p role="alert" className="text-sm p-3 rounded-xl bg-red-50 text-red-700">{error}</p>}
      {list === null ? (
        <div className="flex justify-center py-12"><Loader2 className="animate-spin text-primary" /></div>
      ) : forbidden ? (
        <EmptyState icon={PackageCheck} title={t('results.forbidden', { ns: 'stores' })} />
      ) : (
        <>
          {active.length === 0 ? (
            <p className="text-sm text-text-muted">{t('store.empty')}</p>
          ) : (
            <ul className="space-y-2">
              {active.map((r) => (
                <ReservationCard key={r.id} r={r} showStore={false}>
                  <div className="flex flex-wrap items-end gap-2">
                    {r.status === 'Pending' && (
                      <button type="button" disabled={busy === r.id} onClick={() => act(r, () => reservations.confirm(r.id))} className={`${btn} bg-primary text-white`}>
                        {t('store.confirm')}
                      </button>
                    )}
                    <label className="flex items-center gap-1.5">
                      <span className="sr-only">{t('store.pickupPrompt')}</span>
                      <input value={codes[r.id] ?? ''} maxLength={6} placeholder="ABC123" autoCapitalize="characters"
                        onChange={(e) => setCodes({ ...codes, [r.id]: e.target.value.toUpperCase() })}
                        className="w-24 px-2 py-1.5 rounded-xl border border-border text-sm font-mono tracking-widest" />
                      <button type="button" disabled={busy === r.id || (codes[r.id] ?? '').length < 6}
                        onClick={() => act(r, () => reservations.pickup(r.id, codes[r.id]))} className={`${btn} bg-green-600 text-white`}>
                        {t('store.pickup')}
                      </button>
                    </label>
                    <button type="button" disabled={busy === r.id} onClick={() => act(r, () => reservations.noShow(r.id), t('store.noShowConfirm'))} className={`${btn} border border-border text-text-secondary`}>
                      {t('store.noShow')}
                    </button>
                    <button type="button" disabled={busy === r.id} onClick={() => act(r, () => reservations.cancel(r.id), t('store.cancelConfirm'))} className={`${btn} text-danger`}>
                      {t('cancel')}
                    </button>
                  </div>
                </ReservationCard>
              ))}
            </ul>
          )}
          {past.length > 0 && (
            <section className="space-y-2">
              <h2 className="text-sm font-semibold text-text-secondary">{t('history')}</h2>
              <ul className="space-y-2">{past.map((r) => <ReservationCard key={r.id} r={r} showStore={false} />)}</ul>
            </section>
          )}
        </>
      )}
    </div>
  );
}
