import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, Loader2, ShoppingBag } from 'lucide-react';
import { reservations } from '../api';
import type { Reservation } from '../types';
import { apiErrorMessage } from '../utils/apiError';
import EmptyState from '../components/ui/EmptyState';
import EmptyStateLink from '../components/ui/EmptyStateLink';
import ReservationCard from '../components/stores/ReservationCard';

/** Le prenotazioni dell'utente: codice di ritiro, scadenza, annullamento. */
export default function ReservationsPage() {
  const { t } = useTranslation('reservations');
  const navigate = useNavigate();
  const [list, setList] = useState<Reservation[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    reservations.mine().then(({ data }) => setList(data)).catch(() => setList([]));
  }, []);
  useEffect(() => { load(); }, [load]);

  const cancel = async (r: Reservation) => {
    if (!confirm(t('cancelConfirm'))) return;
    setError(null);
    try {
      await reservations.cancel(r.id);
      load();
    } catch (err) {
      setError(apiErrorMessage(err, t('cancel')));
    }
  };

  const active = list?.filter((r) => r.status === 'Pending' || r.status === 'Confirmed') ?? [];
  const past = list?.filter((r) => r.status !== 'Pending' && r.status !== 'Confirmed') ?? [];

  return (
    <div className="pb-24 space-y-4 max-w-2xl">
      <div className="flex items-center gap-3">
        <button onClick={() => navigate(-1)} aria-label={t('common:actions.back')} className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center shrink-0"><ArrowLeft size={18} /></button>
        <h1 className="text-xl font-bold text-text flex items-center gap-2"><ShoppingBag size={20} className="text-primary" /> {t('title')}</h1>
      </div>
      {error && <p role="alert" className="text-sm p-3 rounded-xl bg-red-50 text-red-700">{error}</p>}
      {list === null ? (
        <div className="flex justify-center py-12"><Loader2 className="animate-spin text-primary" /></div>
      ) : list.length === 0 ? (
        <EmptyState icon={ShoppingBag} title={t('title')} description={t('empty')} action={<EmptyStateLink to="/local" label={t('emptyAction')} />} />
      ) : (
        <>
          {active.length > 0 && (
            <section className="space-y-2">
              <h2 className="text-sm font-semibold text-text">{t('active')}</h2>
              <ul className="space-y-2">
                {active.map((r) => (
                  <ReservationCard key={r.id} r={r}>
                    <button type="button" onClick={() => cancel(r)} className="text-xs font-semibold text-danger">{t('cancel')}</button>
                  </ReservationCard>
                ))}
              </ul>
            </section>
          )}
          {past.length > 0 && (
            <section className="space-y-2">
              <h2 className="text-sm font-semibold text-text-secondary">{t('history')}</h2>
              <ul className="space-y-2">{past.map((r) => <ReservationCard key={r.id} r={r} />)}</ul>
            </section>
          )}
        </>
      )}
    </div>
  );
}
