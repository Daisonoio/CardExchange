import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowLeft, Loader2, Receipt, Search } from 'lucide-react';
import { adminBilling, billing } from '../api';
import type { AdminSubscriptionRow, SubscriptionPlan } from '../types';
import { apiErrorMessage } from '../utils/apiError';
import Button from '../components/ui/Button';
import Input from '../components/ui/Input';
import EmptyState from '../components/ui/EmptyState';

type Tab = 'Pending' | 'Active' | 'all' | 'grant';

const TAB_LABEL: Record<Tab, string> = { Pending: 'Bonifici', Active: 'Attivi', all: 'Tutti', grant: 'Attiva' };
const STATUS_CLS: Record<AdminSubscriptionRow['status'], string> = {
  PendingPayment: 'bg-amber-100 text-amber-800',
  Active: 'bg-green-100 text-green-800',
  Expired: 'bg-gray-100 text-gray-700',
  Cancelled: 'bg-red-100 text-red-700',
};
const STATUS_LABEL: Record<AdminSubscriptionRow['status'], string> = {
  PendingPayment: 'in attesa', Active: 'attivo', Expired: 'scaduto', Cancelled: 'annullato',
};
const METHOD_LABEL: Record<AdminSubscriptionRow['paymentMethod'], string> = {
  Stripe: 'Carta', BankTransfer: 'Bonifico', Manual: 'Manuale', Simulated: 'Prova',
};

const euro = (v: number) => v.toLocaleString('it-IT', { style: 'currency', currency: 'EUR' });
const day = (iso: string) => new Date(iso).toLocaleDateString('it-IT');

/** Abbonamenti alla piattaforma: registrazione dei bonifici, attivazioni manuali, revoche (Admin/SuperAdmin). */
export default function AdminBillingPage() {
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
      setError(apiErrorMessage(err, 'Non hai accesso agli abbonamenti.'));
      setItems([]);
    }
  }, [tab, q]);

  useEffect(() => { load(); }, [tab]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => {
    billing.plans().then(({ data }) => {
      setPlans(data);
      setGrant((g) => ({ ...g, planId: g.planId || data.find((p) => p.tier === 'Store')?.id || data[0]?.id || 0 }));
    }).catch(() => undefined);
  }, []);

  const parseAmount = (s: string | undefined) => {
    if (!s?.trim()) return undefined;
    const v = Number(s.replace(',', '.'));
    return Number.isFinite(v) && v >= 0 ? v : NaN;
  };

  const confirmPayment = async (row: AdminSubscriptionRow) => {
    const amount = parseAmount(amounts[row.id]);
    if (Number.isNaN(amount)) { setError('Importo non valido'); return; }
    if (amount !== undefined && amount < row.price &&
        !confirm(`L'importo ricevuto (${euro(amount)}) è inferiore al prezzo del piano (${euro(row.price)}). Attivare comunque?`)) return;
    setBusyId(row.id);
    setError(null);
    try {
      const { data } = await adminBilling.confirm(row.id, { amountPaid: amount, note: notes[row.id]?.trim() || undefined });
      setNotice(`${data.message} per @${row.username}`);
      setItems((prev) => prev?.filter((r) => r.id !== row.id) ?? null);
    } catch (err) {
      setError(apiErrorMessage(err, 'Operazione non riuscita.'));
    } finally {
      setBusyId(null);
    }
  };

  const revoke = async (row: AdminSubscriptionRow) => {
    const pending = row.status === 'PendingPayment';
    const question = pending
      ? `Rifiutare la richiesta di @${row.username}?`
      : `Revocare subito ${row.planName} a @${row.username}?${row.paymentMethod === 'Stripe' ? ' Il rinnovo su Stripe va annullato anche dalla dashboard Stripe.' : ''}`;
    if (!confirm(question)) return;
    setBusyId(row.id);
    try {
      await adminBilling.revoke(row.id, notes[row.id]?.trim() || undefined);
      await load();
    } catch (err) {
      setError(apiErrorMessage(err, 'Operazione non riuscita.'));
    } finally {
      setBusyId(null);
    }
  };

  const submitGrant = async (e: React.FormEvent) => {
    e.preventDefault();
    const amount = parseAmount(grant.amountPaid) ?? 0;
    if (Number.isNaN(amount)) { setError('Importo non valido'); return; }
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
      setError(apiErrorMessage(err, 'Attivazione non riuscita.'));
    } finally {
      setGranting(false);
    }
  };

  return (
    <div className="pb-20 space-y-4">
      <div className="flex items-center gap-3">
        <button onClick={() => navigate(-1)} aria-label="Indietro" className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center">
          <ArrowLeft size={18} />
        </button>
        <h1 className="text-xl font-bold text-text flex-1">Abbonamenti</h1>
      </div>

      <div className="flex rounded-xl border border-border overflow-hidden bg-white">
        {(Object.keys(TAB_LABEL) as Tab[]).map((t) => (
          <button
            key={t}
            onClick={() => { setTab(t); setNotice(null); }}
            className={`flex-1 py-2 text-sm font-medium ${tab === t ? 'bg-primary text-white' : 'text-text-secondary'}`}
          >
            {TAB_LABEL[t]}
          </button>
        ))}
      </div>

      {error && <div className="p-3 rounded-xl bg-red-50 text-danger text-sm">{error}</div>}
      {notice && <div className="p-3 rounded-xl bg-green-50 text-green-800 text-sm">{notice}</div>}

      {tab === 'grant' ? (
        <form onSubmit={submitGrant} className="p-4 rounded-2xl bg-white border border-border space-y-3">
          <p className="text-sm text-text-secondary">
            Per pagamenti ricevuti fuori dall'app (fattura, contanti in fiera) o per un periodo di prova. Senza scadenza vale la durata del piano,
            in coda a un eventuale periodo già attivo dello stesso tipo.
          </p>
          <Input label="Username o email" value={grant.user} onChange={(e) => setGrant({ ...grant, user: e.target.value })} required />
          <label className="block">
            <span className="block text-sm font-medium text-text mb-1">Piano</span>
            <select
              value={grant.planId}
              onChange={(e) => setGrant({ ...grant, planId: Number(e.target.value) })}
              className="w-full px-3 py-2.5 rounded-xl border border-border bg-white text-sm"
            >
              {plans.map((p) => <option key={p.id} value={p.id}>{p.name} · {euro(p.price)}</option>)}
            </select>
          </label>
          <div className="grid grid-cols-2 gap-3">
            <Input label="Importo ricevuto €" inputMode="decimal" placeholder="0" value={grant.amountPaid} onChange={(e) => setGrant({ ...grant, amountPaid: e.target.value })} />
            <Input label="Scadenza (facoltativa)" type="date" value={grant.endDate} onChange={(e) => setGrant({ ...grant, endDate: e.target.value })} />
          </div>
          <Input label="Riferimento (n. fattura…)" value={grant.paymentReference} onChange={(e) => setGrant({ ...grant, paymentReference: e.target.value })} />
          <Input label="Nota interna" value={grant.note} onChange={(e) => setGrant({ ...grant, note: e.target.value })} />
          <Button type="submit" className="w-full" isLoading={granting} disabled={!grant.user.trim() || !grant.planId}>Attiva piano</Button>
        </form>
      ) : (
        <>
          <form onSubmit={(e) => { e.preventDefault(); load(); }} className="flex gap-2">
            <div className="relative flex-1">
              <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
              <input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder="Utente, email o causale"
                className="w-full pl-9 pr-3 py-2.5 rounded-xl border border-border bg-white text-sm"
              />
            </div>
            <Button type="submit" variant="outline">Cerca</Button>
          </form>

          {items === null ? (
            <div className="flex justify-center py-10"><Loader2 className="animate-spin text-primary" /></div>
          ) : items.length === 0 ? (
            <EmptyState icon={Receipt} title="Niente da mostrare" description={tab === 'Pending' ? 'Nessun bonifico in attesa.' : 'Nessun abbonamento trovato.'} />
          ) : (
            <>
              <p className="text-xs text-text-muted">{total} risultati</p>
              <ul className="space-y-3">
                {items.map((r) => (
                  <li key={r.id} className="p-3 rounded-2xl border border-border bg-white space-y-2">
                    <div className="flex items-start justify-between gap-2">
                      <div className="min-w-0">
                        <p className="text-sm font-semibold truncate">@{r.username} · {r.planName}</p>
                        <p className="text-[11px] text-text-muted truncate">{r.email}</p>
                      </div>
                      <span className={`text-[10px] px-2 py-0.5 rounded-full shrink-0 ${STATUS_CLS[r.status]}`}>{STATUS_LABEL[r.status]}</span>
                    </div>
                    <p className="text-xs text-text-secondary">
                      {METHOD_LABEL[r.paymentMethod]}
                      {r.paymentReference && <> · <span className="font-mono select-all">{r.paymentReference}</span></>}
                      {r.status === 'PendingPayment'
                        ? <> · da pagare {euro(r.price)} · richiesto il {day(r.createdAt)}</>
                        : <> · {euro(r.amountPaid)} · {day(r.startDate)} → {day(r.endDate)}{r.autoRenew ? ' · rinnovo automatico' : ''}</>}
                    </p>
                    {r.adminNote && <p className="text-[11px] text-text-muted">Nota: {r.adminNote}</p>}
                    {(r.status === 'PendingPayment' || r.status === 'Active') && (
                      <input
                        value={notes[r.id] ?? ''}
                        onChange={(e) => setNotes({ ...notes, [r.id]: e.target.value })}
                        placeholder="Nota (facoltativa)"
                        className="w-full px-3 py-2 rounded-xl border border-border text-sm"
                      />
                    )}
                    {r.status === 'PendingPayment' && (
                      <div className="flex gap-2 items-center">
                        <input
                          value={amounts[r.id] ?? ''}
                          onChange={(e) => setAmounts({ ...amounts, [r.id]: e.target.value })}
                          placeholder={`€ ${r.price.toFixed(2).replace('.', ',')}`}
                          aria-label="Importo ricevuto"
                          inputMode="decimal"
                          className="w-24 px-3 py-2 rounded-xl border border-border text-sm"
                        />
                        <Button size="sm" onClick={() => confirmPayment(r)} isLoading={busyId === r.id}>Bonifico ricevuto</Button>
                        <Button size="sm" variant="ghost" onClick={() => revoke(r)} disabled={busyId === r.id}>Rifiuta</Button>
                      </div>
                    )}
                    {r.status === 'Active' && (
                      <Button size="sm" variant="ghost" onClick={() => revoke(r)} isLoading={busyId === r.id}>Revoca</Button>
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
