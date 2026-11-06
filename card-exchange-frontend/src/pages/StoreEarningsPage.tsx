import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, Loader2, PiggyBank, QrCode, Users } from 'lucide-react';
import { commissions } from '../api';
import { useFormat } from '../i18n/format';
import EmptyState from '../components/ui/EmptyState';

type Data = Awaited<ReturnType<typeof commissions.forStore>>['data'];

/** Guadagni del negozio: commissioni sul Premium dei clienti portati con il QR (solo importi e date). */
export default function StoreEarningsPage() {
  const { t } = useTranslation('stores');
  const { id } = useParams<{ id: string }>();
  const { euro, date } = useFormat();
  const [data, setData] = useState<Data | null | undefined>(undefined);

  useEffect(() => {
    commissions.forStore(Number(id)).then(({ data }) => setData(data)).catch(() => setData(null));
  }, [id]);

  if (data === undefined) return <div className="flex justify-center py-16"><Loader2 className="animate-spin text-primary" /></div>;
  if (data === null) return <EmptyState icon={Users} title={t('results.forbidden')} />;

  const tiles: [string, number][] = [['maturing', data.maturingEur], ['available', data.availableEur], ['credited', data.creditedEur]];
  return (
    <div className="max-w-2xl mx-auto space-y-4 pb-24">
      <div className="flex items-center gap-2">
        <Link to={`/stores/${id}`} className="p-1.5 -ml-1.5 rounded-lg hover:bg-surface-hover" aria-label={t('common:actions.back')}><ArrowLeft size={20} /></Link>
        <h1 className="text-xl font-bold text-text flex items-center gap-2"><PiggyBank size={20} className="text-primary" /> {t('earnings.title')}</h1>
      </div>
      <p className="text-sm text-text-secondary">{t('earnings.intro', { rate: Math.round(data.rate * 100) })}</p>

      <section className="p-4 rounded-2xl bg-primary text-white">
        <p className="text-sm">{t('earnings.thisMonth', { amount: euro(data.thisMonthEur), count: data.thisMonthCount })}</p>
      </section>
      <section className="grid grid-cols-3 gap-2">
        {tiles.map(([key, value]) => (
          <div key={key} className="p-3 rounded-xl bg-white border border-border">
            <p className="text-[11px] text-text-secondary">{t(`earnings.${key}`)}</p>
            <p className="text-base font-bold text-text tabular-nums">{euro(value)}</p>
          </div>
        ))}
      </section>

      <section className="bg-white rounded-2xl border border-border p-3">
        <h2 className="text-sm font-semibold text-text">{t('earnings.recent')}</h2>
        {data.recent.length === 0 ? (
          <div className="mt-1 space-y-2">
            <p className="text-sm text-text-muted">{t('earnings.none')}</p>
            <Link to={`/qr/store/${id}`} className="inline-flex items-center gap-1.5 text-sm font-semibold text-primary"><QrCode size={15} /> {t('earnings.qr')}</Link>
          </div>
        ) : (
          <ul className="divide-y divide-border/60">
            {data.recent.map((r, i) => (
              <li key={i} className="py-2 flex items-center justify-between text-sm">
                <span className="text-text-secondary">{date(r.paidAt, { day: 'numeric', month: 'short', year: 'numeric' })}</span>
                <span className="flex items-center gap-2">
                  <span className="text-xs text-text-muted">{t(`earnings.status.${r.status}`)}</span>
                  <strong className={`tabular-nums ${r.status === 'Reversed' ? 'line-through text-text-muted' : 'text-text'}`}>{euro(r.amountEur)}</strong>
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>

      {data.payouts.length > 0 && (
        <section className="bg-white rounded-2xl border border-border p-3">
          <h2 className="text-sm font-semibold text-text">{t('earnings.payouts')}</h2>
          <ul className="divide-y divide-border/60">
            {data.payouts.map((p) => (
              <li key={p.id} className="py-2 flex items-center justify-between text-sm">
                <span className="text-text-secondary">{p.month}</span>
                <span className="flex items-center gap-2">
                  <span className="text-xs text-text-muted">{p.status === 'Done' ? t('earnings.payoutDone') : t('earnings.payoutPending')}</span>
                  <strong className="tabular-nums text-text">{euro(p.amountEur)}</strong>
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}
      <p className="text-xs text-text-muted">{t('earnings.privacy')}</p>
    </div>
  );
}
