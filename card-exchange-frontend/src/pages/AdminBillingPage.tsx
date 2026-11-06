import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowLeft, Loader2, Receipt, Search } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { adminBilling, billing } from '../api';
import { formatDate, formatEuro, formatNumber } from '../i18n/format';
import type { AdminSubscriptionRow, SubscriptionPlan } from '../types';
import { apiErrorMessage } from '../utils/apiError';
import Button from '../components/ui/Button';
import Input from '../components/ui/Input';
import EmptyState from '../components/ui/EmptyState';

type Tab = 'Pending' | 'Active' | 'all' | 'grant';

// Chiavi del namespace "admin"
const TAB_LABEL: Record<Tab, string> = { Pending: 'billing.tabs.pending', Active: 'billing.tabs.active', all: 'billing.tabs.all', grant: 'billing.tabs.grant' };
const STATUS_CLS: Record<AdminSubscriptionRow['status'], string> = {
  PendingPayment: 'bg-amber-100 text-amber-800',
  Active: 'bg-green-100 text-green-800',
  Expired: 'bg-gray-100 text-gray-700',
  Cancelled: 'bg-red-100 text-red-700',
};
const STATUS_LABEL: Record<AdminSubscriptionRow['status'], string> = {
  PendingPayment: 'billing.status.pendingPayment', Active: 'billing.status.active', Expired: 'billing.status.expired', Cancelled: 'billing.status.cancelled',
};
const METHOD_LABEL: Record<AdminSubscriptionRow['paymentMethod'], string> = {
  Stripe: 'billing.method.stripe', BankTransfer: 'billing.method.bankTransfer', Manual: 'billing.method.manual', Simulated: 'billing.method.simulated',
};

const euro = (v: number) => formatEuro(v);
const day = (iso: string) => formatDate(iso, {});

/** Abbonamenti alla piattaforma: registrazione dei bonifici, attivazioni manuali, revoche (Admin/SuperAdmin). */
export default function AdminBillingPage() {
  const { t } = useTranslation('admin');
  const navigate = useNavigate();
  const [tab, setTab] = useState<Tab>('Pending');
  const [q, setQ] = useState('');
  const [items, setItems] = useState<AdminSubscriptionRow[] | null>(null);
  const [total, setTotal] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<number | null>(null);
  const [amounts, setAmounts] = useState<Record<number, string>>({});
  const [notes, setNotes] = useState<Record<number, string>>({});
  const [plans, setPlans] = useState<SubscriptionPlan[]>([]);
  const [grant, setGrant] = useState({ user: '', planId: 0, amountPaid: '', endDate: '', paymentReference: '', note: '' });
  const [granting, setGranting] = useState(false);

  const load = useCallback(async () => {
    if (tab === 'grant') return;
    setItems(null);
    setError(null);
    try {
      const { data } = await adminBilling.list({ status: tab === 'all' ? undefined : tab, q: q.trim() || undefined });
      setItems(data.items);
      setTotal(data.total);
    } catch (err) {
      setError(apiErrorMessage(err, t('billing.errors.noAccess')));
      setItems([]);
    }
  }, [tab, q, t]);

  useEffect(() => { load(); }, [tab]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => {
    billing.plans().then(({ data }) => {
      setPlans(data);
      setGrant((g) => ({ ...g, planId: g.planId || data.find((p) => p.tier === 'Store' || p.tier === 'StorePro')?.id || data[0]?.id || 0 }));
    }).catch(() => undefined);
  }, []);

  const parseAmount = (s: string | undefined) => {
    if (!s?.trim()) return undefined;
    const v = Number(s.replace(',', '.'));
    return Number.isFinite(v) && v >= 0 ? v : NaN;
  };

  const confirmPayment = async (row: AdminSubscriptionRow) => {
    const amount = parseAmount(amounts[row.id]);
    if (Number.isNaN(amount)) { setError(t('billing.errors.invalidAmount')); return; }
    if (amount !== undefined && amount < row.price &&
        !confirm(t('billing.confirmUnderpaid', { amount: euro(amount), price: euro(row.price) }))) return;
    setBusyId(row.id);
    setError(null);
    try {
      const { data } = await adminBilling.confirm(row.id, { amountPaid: amount, note: notes[row.id]?.trim() || undefined });
      setNotice(t('billing.confirmedFor', { message: data.message, username: row.username }));
      setItems((prev) => prev?.filter((r) => r.id !== row.id) ?? null);
    } catch (err) {
      setError(apiErrorMessage(err, t('billing.errors.operationFailed')));
    } finally {
      setBusyId(null);
    }
  };

  const revoke = async (row: AdminSubscriptionRow) => {
    const pending = row.status === 'PendingPayment';
    const question = pending
      ? t('billing.confirmReject', { username: row.username })
      : `${t('billing.confirmRevoke', { plan: row.planName, username: row.username })}${row.paymentMethod === 'Stripe' ? ` ${t('billing.confirmRevokeStripe')}` : ''}`;
    if (!confirm(question)) return;
    setBusyId(row.id);
    try {
      await adminBilling.revoke(row.id, notes[row.id]?.trim() || undefined);
      await load();
    } catch (err) {
      setError(apiErrorMessage(err, t('billing.errors.operationFailed')));
    } finally {
      setBusyId(null);
    }
  };

  const submitGrant = async (e: React.FormEvent) => {
    e.preventDefault();
    const amount = parseAmount(grant.amountPaid) ?? 0;
    if (Number.isNaN(amount)) { setError(t('billing.errors.invalidAmount')); return; }
    setGranting(true);
    setError(null);
    try {
      const { data } = await adminBilling.grant({
        user: grant.user.trim(),
        planId: grant.planId,
        amountPaid: amount,
        endDate: grant.endDate ? new Date(`${grant.endDate}T23:59:00`).toISOString() : undefined,
        paymentReference: grant.paymentReference.trim() || undefined,
        note: grant.note.trim() || undefined,
      });
      setNotice(data.message);
      setGrant((g) => ({ ...g, user: '', amountPaid: '', endDate: '', paymentReference: '', note: '' }));
    } catch (err) {
      setError(apiErrorMessage(err, t('billing.errors.grantFailed')));
    } finally {
      setGranting(false);
    }
  };

  return (
    <div className="pb-20 space-y-4">
      <div className="flex items-center gap-3">
        <button onClick={() => navigate(-1)} aria-label={t('common:actions.back')} className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center">
          <ArrowLeft size={18} />
        </button>
        <h1 className="text-xl font-bold text-text flex-1">{t('billing.title')}</h1>
      </div>

      <div className="flex rounded-xl border border-border overflow-hidden bg-white">
        {(Object.keys(TAB_LABEL) as Tab[]).map((tb) => (
          <button
            key={tb}
            onClick={() => { setTab(tb); setNotice(null); }}
            className={`flex-1 py-2 text-sm font-medium ${tab === tb ? 'bg-primary text-white' : 'text-text-secondary'}`}
          >
            {t(TAB_LABEL[tb])}
          </button>
        ))}
      </div>

      {error && <div className="p-3 rounded-xl bg-red-50 text-danger text-sm">{error}</div>}
      {notice && <div className="p-3 rounded-xl bg-green-50 text-green-800 text-sm">{notice}</div>}

      {tab === 'grant' ? (
        <form onSubmit={submitGrant} className="p-4 rounded-2xl bg-white border border-border space-y-3">
          <p className="text-sm text-text-secondary">
            {t('billing.grant.intro')}
          </p>
          <Input label={t('billing.grant.user')} value={grant.user} onChange={(e) => setGrant({ ...grant, user: e.target.value })} required />
          <label className="block">
            <span className="block text-sm font-medium text-text mb-1">{t('billing.grant.plan')}</span>
            <select
              value={grant.planId}
              onChange={(e) => setGrant({ ...grant, planId: Number(e.target.value) })}
              className="w-full px-3 py-2.5 rounded-xl border border-border bg-white text-sm"
            >
              {plans.map((p) => <option key={p.id} value={p.id}>{p.name} · {euro(p.price)}</option>)}
            </select>
          </label>
          <div className="grid grid-cols-2 gap-3">
            <Input label={t('billing.grant.amount')} inputMode="decimal" placeholder="0" value={grant.amountPaid} onChange={(e) => setGrant({ ...grant, amountPaid: e.target.value })} />
            <Input label={t('billing.grant.endDate')} type="date" value={grant.endDate} onChange={(e) => setGrant({ ...grant, endDate: e.target.value })} />
          </div>
          <Input label={t('billing.grant.reference')} value={grant.paymentReference} onChange={(e) => setGrant({ ...grant, paymentReference: e.target.value })} />
          <Input label={t('billing.grant.note')} value={grant.note} onChange={(e) => setGrant({ ...grant, note: e.target.value })} />
          <Button type="submit" className="w-full" isLoading={granting} disabled={!grant.user.trim() || !grant.planId}>{t('billing.grant.submit')}</Button>
        </form>
      ) : (
        <>
          <form onSubmit={(e) => { e.preventDefault(); load(); }} className="flex gap-2">
            <div className="relative flex-1">
              <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
              <input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder={t('billing.searchPlaceholder')}
                className="w-full pl-9 pr-3 py-2.5 rounded-xl border border-border bg-white text-sm"
              />
            </div>
            <Button type="submit" variant="outline">{t('common:actions.search')}</Button>
          </form>

          {items === null ? (
            <div className="flex justify-center py-10"><Loader2 className="animate-spin text-primary" /></div>
          ) : items.length === 0 ? (
            <EmptyState icon={Receipt} title={t('billing.empty.title')} description={tab === 'Pending' ? t('billing.empty.pending') : t('billing.empty.other')} />
          ) : (
            <>
              <p className="text-xs text-text-muted">{t('billing.results', { count: total })}</p>
              <ul className="space-y-3">
                {items.map((r) => (
                  <li key={r.id} className="p-3 rounded-2xl border border-border bg-white space-y-2">
                    <div className="flex items-start justify-between gap-2">
                      <div className="min-w-0">
                        <p className="text-sm font-semibold truncate">@{r.username} · {r.planName}</p>
                        <p className="text-[11px] text-text-muted truncate">{r.email}</p>
                      </div>
                      <span className={`text-[10px] px-2 py-0.5 rounded-full shrink-0 ${STATUS_CLS[r.status]}`}>{t(STATUS_LABEL[r.status])}</span>
                    </div>
                    <p className="text-xs text-text-secondary">
                      {t(METHOD_LABEL[r.paymentMethod])}
                      {r.paymentReference && <> · <span className="font-mono select-all">{r.paymentReference}</span></>}
                      {r.status === 'PendingPayment'
                        ? <> · {t('billing.row.pending', { price: euro(r.price), date: day(r.createdAt) })}</>
                        : <> · {euro(r.amountPaid)} · {day(r.startDate)} → {day(r.endDate)}{r.autoRenew ? ` · ${t('billing.row.autoRenew')}` : ''}</>}
                    </p>
                    {r.adminNote && <p className="text-[11px] text-text-muted">{t('billing.row.note', { note: r.adminNote })}</p>}
                    {(r.status === 'PendingPayment' || r.status === 'Active') && (
                      <input
                        value={notes[r.id] ?? ''}
                        onChange={(e) => setNotes({ ...notes, [r.id]: e.target.value })}
                        placeholder={t('billing.row.notePlaceholder')}
                        className="w-full px-3 py-2 rounded-xl border border-border text-sm"
                      />
                    )}
                    {r.status === 'PendingPayment' && (
                      <div className="flex gap-2 items-center">
                        <input
                          value={amounts[r.id] ?? ''}
                          onChange={(e) => setAmounts({ ...amounts, [r.id]: e.target.value })}
                          placeholder={`€ ${formatNumber(r.price, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`}
                          aria-label={t('billing.row.amountReceived')}
                          inputMode="decimal"
                          className="w-24 px-3 py-2 rounded-xl border border-border text-sm"
                        />
                        <Button size="sm" onClick={() => confirmPayment(r)} isLoading={busyId === r.id}>{t('billing.row.confirm')}</Button>
                        <Button size="sm" variant="ghost" onClick={() => revoke(r)} disabled={busyId === r.id}>{t('billing.row.reject')}</Button>
                      </div>
                    )}
                    {r.status === 'Active' && (
                      <Button size="sm" variant="ghost" onClick={() => revoke(r)} isLoading={busyId === r.id}>{t('billing.row.revoke')}</Button>
                    )}
                  </li>
                ))}
              </ul>
            </>
          )}
        </>
      )}
    </div>
  );
}
