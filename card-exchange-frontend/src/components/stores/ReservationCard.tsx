import { useTranslation } from 'react-i18next';
import type { Reservation } from '../../types';
import { conditionLabel } from '../../types';
import { useFormat } from '../../i18n/format';

const STATUS_CLS: Record<Reservation['status'], string> = {
  Pending: 'bg-amber-50 text-amber-700',
  Confirmed: 'bg-green-50 text-green-700',
  PickedUp: 'bg-surface-dark text-text-secondary',
  Cancelled: 'bg-surface-dark text-text-muted',
  NoShow: 'bg-red-50 text-red-700',
  Expired: 'bg-surface-dark text-text-muted',
};

/** Riga di una prenotazione: carta, stato, scadenza; le azioni arrivano dal chiamante. */
export default function ReservationCard({ r, children, showStore = true }: { r: Reservation; children?: React.ReactNode; showStore?: boolean }) {
  const { t } = useTranslation('reservations');
  const { euro, dateTime } = useFormat();
  const active = r.status === 'Pending' || r.status === 'Confirmed';
  return (
    <li className="bg-white rounded-2xl border border-border p-3 space-y-2">
      <div className="flex gap-3">
        {r.imageUrl
          ? <img src={r.imageUrl} alt="" loading="lazy" className="w-12 h-[67px] object-cover rounded-md bg-surface-dark shrink-0" onError={(e) => { e.currentTarget.style.visibility = 'hidden'; }} />
          : <div className="w-12 h-[67px] rounded-md bg-surface-dark shrink-0" />}
        <div className="flex-1 min-w-0">
          <div className="flex items-start justify-between gap-2">
            <p className="text-sm font-semibold text-text truncate">{r.cardName}{r.quantity > 1 ? ` ×${r.quantity}` : ''}</p>
            <span className={`shrink-0 text-[11px] font-medium px-2 py-0.5 rounded-full ${STATUS_CLS[r.status]}`}>{t(`status.${r.status}`)}</span>
          </div>
          <p className="text-xs text-text-muted truncate">{[r.setName, conditionLabel(r.condition), r.language?.toUpperCase()].filter(Boolean).join(' · ')}</p>
          {showStore && <p className="text-xs text-text-secondary truncate">{r.storeName} · {r.storeAddress}, {r.storeCity}</p>}
          {r.username && <p className="text-xs text-text-secondary">{t('store.by', { username: r.username })}</p>}
          <p className="text-xs text-text-secondary">
            {r.priceEur != null && <><strong className="text-text">{euro(r.priceEur)}</strong> · {t('payInStore')}</>}
          </p>
          {active && <p className="text-xs text-text-secondary first-letter:uppercase">{t('until', { when: dateTime(r.expiresAt, { weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }) })}</p>}
        </div>
      </div>
      {active && r.pickupCode && (
        <div className="flex items-center justify-between rounded-xl bg-surface-dark px-3 py-2">
          <span className="text-xs text-text-secondary">{t('code')}</span>
          <span className="font-mono text-lg font-bold tracking-[0.25em] text-text">{r.pickupCode}</span>
        </div>
      )}
      {children}
    </li>
  );
}
