import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { ArrowLeft, BadgeCheck, Check, Copy, CreditCard, Crown, Info, Landmark, Loader2, Store as StoreIcon } from 'lucide-react';
import { billing } from '../api';
import type { BankTransferInstructions, BillingOptions, SubscriptionPlan, SubscriptionState, UserSubscription } from '../types';
import { apiErrorMessage } from '../utils/apiError';
import Button from '../components/ui/Button';
import BottomSheet from '../components/ui/BottomSheet';

const euro = (v: number) => v.toLocaleString('it-IT', { style: 'currency', currency: 'EUR' });
const day = (iso: string) => new Date(iso).toLocaleDateString('it-IT', { day: 'numeric', month: 'long', year: 'numeric' });

const TIER_LABEL = { Free: 'Gratuito', Premium: 'Premium', Store: 'Negozio' } as const;
const METHOD_LABEL: Record<UserSubscription['paymentMethod'], string> = {
  Stripe: 'carta',
  BankTransfer: 'bonifico',
  Manual: 'attivazione manuale',
  Simulated: 'prova',
};

const FREE_FEATURES = [
  'Collezione e wishlist con lingua, finitura e prezzi',
  'Match con chi ha le tue carte vicino a te',
  'Negozi, eventi e serate di scambio',
  'Wishlist fino a 20 carte, 5 proposte attive, 10 messaggi al giorno',
];

function planFeatures(p: SubscriptionPlan): string[] {
  const f: string[] = [];
  if (p.tier === 'Store') f.push('Scheda negozio in evidenza nella sezione Locale');
  f.push('Wishlist, proposte e messaggi senza limiti');
  if (p.hasWishlistAlerts) f.push('Avvisi quando una carta della wishlist è disponibile');
  if (p.canSearchAdvanced || p.canSearchGeographic) f.push('Ricerca avanzata e per distanza');
  if (p.canViewStatistics) f.push('Statistiche e andamento del valore della collezione');
  if (p.canExportCollection) f.push('Esportazione della collezione');
  if (p.hasPriorityListing) f.push('Più visibilità nei match');
  if (p.hasVerifiedBadge) f.push('Badge verificato sul profilo');
  return f;
}

function CopyRow({ label, value }: { label: string; value?: string }) {
  const [copied, setCopied] = useState(false);
  if (!value) return null;
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch {
      /* copia non disponibile: il valore resta selezionabile */
    }
  };
  return (
    <div className="flex items-center gap-2 py-2 border-b border-border/60 last:border-0">
      <div className="flex-1 min-w-0">
        <p className="text-[11px] text-text-muted">{label}</p>
        <p className="text-sm font-semibold text-text break-all select-all">{value}</p>
      </div>
      <button onClick={copy} aria-label={`Copia ${label}`} className="w-8 h-8 rounded-lg hover:bg-surface-dark flex items-center justify-center text-primary shrink-0">
        {copied ? <Check size={16} /> : <Copy size={16} />}
      </button>
    </div>
  );
}

export default function PremiumPage() {
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const [plans, setPlans] = useState<SubscriptionPlan[] | null>(null);
  const [options, setOptions] = useState<BillingOptions | null>(null);
  const [state, setState] = useState<SubscriptionState | null>(null);
  const [audience, setAudience] = useState<'private' | 'store'>(params.get('for') === 'store' ? 'store' : 'private');
  const [cycle, setCycle] = useState<'Monthly' | 'Annual'>('Annual');
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [instructions, setInstructions] = useState<BankTransferInstructions | null>(null);
  const checkout = params.get('checkout');

  const loadState = useCallback(async () => {
    const { data } = await billing.current();
    setState(data);
    return data;
  }, []);

  // Chi ha già il piano Negozio vede subito la sezione negozi
  const storeTier = state?.tier === 'Store';
  useEffect(() => {
    if (storeTier && !params.get('for')) setAudience('store');
  }, [storeTier]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    Promise.all([billing.plans(), billing.options(), loadState()])
      .then(([p, o]) => {
        setPlans(p.data);
        setOptions(o.data);
      })
      .catch((err) => setError(apiErrorMessage(err, 'Impossibile caricare i piani.')));
  }, [loadState]);

  // Ritorno da Stripe: l'attivazione arriva dal webhook, quindi si attende qualche secondo
  useEffect(() => {
    if (checkout !== 'success') return;
    let tries = 0;
    let cancelled = false;
    const poll = async () => {
      const data = await loadState().catch(() => null);
      if (cancelled) return;
      if (data?.active?.paymentMethod === 'Stripe' || tries++ >= 10) return;
      setTimeout(poll, 2000);
    };
    poll();
    return () => { cancelled = true; };
  }, [checkout, loadState]);

  const premium = useMemo(() => (plans ?? []).filter((p) => p.tier === 'Premium'), [plans]);
  const storePlans = useMemo(() => (plans ?? []).filter((p) => p.tier === 'Store'), [plans]);
  const monthly = premium.find((p) => p.cycle === 'Monthly');
  const shownPremium = premium.find((p) => p.cycle === cycle) ?? premium[0];
  const annualSaving = monthly && premium.find((p) => p.cycle === 'Annual')
    ? Math.round((1 - premium.find((p) => p.cycle === 'Annual')!.price / (monthly.price * 12)) * 100)
    : 0;

  const cardEnabled = !!options?.cardEnabled;
  const bankEnabled = !!options?.bankTransfer?.enabled;
  const stripeAutoRenew = state?.active?.paymentMethod === 'Stripe' && state.active.autoRenew;

  const payByCard = async (plan: SubscriptionPlan) => {
    setBusy(`card-${plan.id}`);
    setError(null);
    try {
      const { data } = await billing.checkout(plan.id);
      window.location.assign(data.url);
    } catch (err) {
      setError(apiErrorMessage(err, 'Pagamento con carta non disponibile.'));
      setBusy(null);
    }
  };

  const payByBank = async (plan: SubscriptionPlan) => {
    setBusy(`bank-${plan.id}`);
    setError(null);
    try {
      const { data } = await billing.bankTransfer(plan.id);
      setInstructions(data);
      await loadState();
    } catch (err) {
      setError(apiErrorMessage(err, 'Richiesta non riuscita.'));
    } finally {
      setBusy(null);
    }
  };

  const showInstructions = (s: UserSubscription) => {
    const plan = plans?.find((p) => p.name === s.planName);
    if (plan) payByBank(plan);
  };

  const cancelRequest = async (s: UserSubscription) => {
    if (!confirm('Annullare la richiesta di bonifico? Se hai già pagato, non annullarla: contattaci.')) return;
    setBusy(`del-${s.id}`);
    try {
      await billing.cancelBankTransfer(s.id);
      await loadState();
    } catch (err) {
      setError(apiErrorMessage(err, 'Operazione non riuscita.'));
    } finally {
      setBusy(null);
    }
  };

  const cancelRenewal = async () => {
    if (!confirm('Disdire il rinnovo automatico? Il piano resta attivo fino alla scadenza già pagata.')) return;
    setBusy('cancel');
    try {
      const { data } = await billing.cancelRenewal();
      setNotice(data.message);
      await loadState();
    } catch (err) {
      setError(apiErrorMessage(err, 'Disdetta non riuscita.'));
    } finally {
      setBusy(null);
    }
  };

  const openPortal = async () => {
    setBusy('portal');
    try {
      const { data } = await billing.portal();
      window.location.assign(data.url);
    } catch (err) {
      setError(apiErrorMessage(err, 'Portale di pagamento non disponibile.'));
      setBusy(null);
    }
  };

  const dismissCheckout = () => {
    params.delete('checkout');
    setParams(params, { replace: true });
  };

  const PayButtons = ({ plan }: { plan: SubscriptionPlan }) => {
    const pendingForPlan = state?.pending.some((s) => s.planName === plan.name);
    if (!cardEnabled && !bankEnabled) {
      return <p className="text-xs text-text-muted text-center">I pagamenti online non sono ancora attivi: contattaci per attivare il piano.</p>;
    }
    return (
      <div className="flex flex-col gap-2">
        {cardEnabled && (
          <Button onClick={() => payByCard(plan)} isLoading={busy === `card-${plan.id}`} disabled={!!busy || stripeAutoRenew}>
            <CreditCard size={16} /> Paga con carta
          </Button>
        )}
        {bankEnabled && (
          <Button variant="outline" onClick={() => payByBank(plan)} isLoading={busy === `bank-${plan.id}`} disabled={!!busy}>
            <Landmark size={16} /> {pendingForPlan ? 'Istruzioni per il bonifico' : 'Paga con bonifico'}
          </Button>
        )}
        {stripeAutoRenew && cardEnabled && (
          <p className="text-[11px] text-text-muted text-center">Hai già un rinnovo automatico: per cambiare piano usa “Gestisci pagamento”.</p>
        )}
      </div>
    );
  };

  if (!plans || !state) {
    return (
      <div className="flex justify-center py-16">
        {error ? <p className="text-sm text-danger">{error}</p> : <Loader2 className="animate-spin text-primary" />}
      </div>
    );
  }

  const active = state.active;

  return (
    <div className="max-w-lg mx-auto pb-24 space-y-4">
      <div className="flex items-center gap-3">
        <button onClick={() => navigate(-1)} aria-label="Indietro" className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center shrink-0">
          <ArrowLeft size={18} />
        </button>
        <h1 className="text-xl font-bold text-text flex-1">Il tuo piano</h1>
      </div>

      {checkout === 'success' && (
        <div className="p-3 rounded-xl bg-green-50 text-green-800 text-sm flex gap-2">
          {active?.paymentMethod === 'Stripe' ? <Check size={18} className="shrink-0" /> : <Loader2 size={18} className="animate-spin shrink-0" />}
          <span className="flex-1">
            {active?.paymentMethod === 'Stripe' ? `Pagamento ricevuto: ${active.planName} è attivo. Grazie!` : 'Pagamento ricevuto, stiamo attivando il piano…'}
          </span>
          <button onClick={dismissCheckout} className="text-xs font-semibold">OK</button>
        </div>
      )}
      {checkout === 'cancelled' && (
        <div className="p-3 rounded-xl bg-amber-50 text-amber-800 text-sm flex gap-2">
          <span className="flex-1">Pagamento annullato: non ti è stato addebitato nulla.</span>
          <button onClick={dismissCheckout} className="text-xs font-semibold">OK</button>
        </div>
      )}
      {error && <div className="p-3 rounded-xl bg-red-50 text-danger text-sm">{error}</div>}
      {notice && <div className="p-3 rounded-xl bg-blue-50 text-blue-800 text-sm">{notice}</div>}

      {/* Stato attuale */}
      <section className="p-4 rounded-2xl bg-white border border-border space-y-2">
        <div className="flex items-center gap-3">
          <span className={`w-10 h-10 rounded-xl flex items-center justify-center shrink-0 ${state.tier === 'Free' ? 'bg-surface-dark text-text-secondary' : 'bg-secondary/15 text-amber-700'}`}>
            {state.tier === 'Store' ? <StoreIcon size={20} /> : <Crown size={20} />}
          </span>
          <div className="flex-1 min-w-0">
            <p className="text-[11px] text-text-muted">Piano attuale</p>
            <p className="text-base font-bold text-text">{active ? active.planName : TIER_LABEL.Free}</p>
          </div>
        </div>
        {active && (
          <p className="text-sm text-text-secondary">
            {active.autoRenew ? 'Si rinnova il ' : 'Attivo fino al '}<strong>{day(active.endDate)}</strong>
            {' · '}pagato con {METHOD_LABEL[active.paymentMethod]}
          </p>
        )}
        {state.queued.map((q) => (
          <p key={q.id} className="text-xs text-text-secondary">
            Rinnovo già pagato: {q.planName} dal {day(q.startDate)} al {day(q.endDate)}
          </p>
        ))}
        {active && (
          <div className="flex flex-wrap gap-2 pt-1">
            {active.paymentMethod === 'Stripe' && cardEnabled && (
              <Button size="sm" variant="outline" onClick={openPortal} isLoading={busy === 'portal'}>Gestisci pagamento</Button>
            )}
            {active.autoRenew && (
              <Button size="sm" variant="ghost" onClick={cancelRenewal} isLoading={busy === 'cancel'}>Disdici rinnovo</Button>
            )}
          </div>
        )}
      </section>

      {/* Bonifici in attesa */}
      {state.pending.map((p) => (
        <section key={p.id} className="p-4 rounded-2xl bg-amber-50 border border-amber-200 space-y-2">
          <p className="text-sm font-semibold text-amber-900">Bonifico in attesa · {p.planName}</p>
          <p className="text-xs text-amber-900/80">
            Causale <strong className="select-all">{p.paymentReference}</strong>. Il piano si attiva appena registriamo il pagamento.
          </p>
          <div className="flex gap-2">
            <Button size="sm" variant="outline" onClick={() => showInstructions(p)} disabled={!!busy}>Istruzioni</Button>
            <Button size="sm" variant="ghost" onClick={() => cancelRequest(p)} isLoading={busy === `del-${p.id}`}>Annulla richiesta</Button>
          </div>
        </section>
      ))}

      {/* Scelta piano */}
      <div className="flex rounded-xl border border-border overflow-hidden bg-white">
        {(['private', 'store'] as const).map((a) => (
          <button
            key={a}
            onClick={() => setAudience(a)}
            className={`flex-1 py-2 text-sm font-medium ${audience === a ? 'bg-primary text-white' : 'text-text-secondary'}`}
          >
            {a === 'private' ? 'Collezionisti' : 'Negozi'}
          </button>
        ))}
      </div>

      {audience === 'private' ? (
        <>
          {shownPremium && (
            <section className="p-4 rounded-2xl bg-white border-2 border-secondary space-y-3">
              <div className="flex items-center justify-between gap-2">
                <h2 className="text-lg font-bold text-text flex items-center gap-1.5"><Crown size={18} className="text-amber-600" /> Premium</h2>
                {premium.length > 1 && (
                  <div className="flex rounded-lg bg-surface-dark p-0.5 text-xs">
                    {(['Monthly', 'Annual'] as const).map((c) => (
                      <button
                        key={c}
                        onClick={() => setCycle(c)}
                        className={`px-2.5 py-1 rounded-md font-medium ${cycle === c ? 'bg-white shadow-sm text-text' : 'text-text-secondary'}`}
                      >
                        {c === 'Monthly' ? 'Mensile' : `Annuale${annualSaving > 0 ? ` −${annualSaving}%` : ''}`}
                      </button>
                    ))}
                  </div>
                )}
              </div>
              <p>
                <span className="text-3xl font-bold text-text">{euro(shownPremium.price)}</span>
                <span className="text-sm text-text-secondary"> / {shownPremium.cycle === 'Annual' ? 'anno' : 'mese'}</span>
                {shownPremium.cycle === 'Annual' && (
                  <span className="block text-xs text-text-muted">pari a {euro(shownPremium.price / 12)} al mese</span>
                )}
              </p>
              <ul className="space-y-1.5">
                {planFeatures(shownPremium).map((f) => (
                  <li key={f} className="flex gap-2 text-sm text-text"><Check size={16} className="text-green-600 shrink-0 mt-0.5" />{f}</li>
                ))}
              </ul>
              <PayButtons plan={shownPremium} />
            </section>
          )}

          <section className="p-4 rounded-2xl bg-white border border-border space-y-2">
            <h2 className="text-base font-bold text-text">Gratuito</h2>
            <ul className="space-y-1.5">
              {FREE_FEATURES.map((f) => (
                <li key={f} className="flex gap-2 text-sm text-text-secondary"><Check size={16} className="text-text-muted shrink-0 mt-0.5" />{f}</li>
              ))}
            </ul>
          </section>
        </>
      ) : storePlans.length === 0 ? (
        <p className="text-sm text-text-muted text-center py-6">Il piano per i negozi arriverà presto.</p>
      ) : (
        storePlans.map((plan) => (
          <section key={plan.id} className="p-4 rounded-2xl bg-white border-2 border-primary space-y-3">
            <h2 className="text-lg font-bold text-text flex items-center gap-1.5"><StoreIcon size={18} className="text-primary" /> {plan.name}</h2>
            {plan.description && <p className="text-sm text-text-secondary">{plan.description}</p>}
            <p>
              <span className="text-3xl font-bold text-text">{euro(plan.price)}</span>
              <span className="text-sm text-text-secondary"> / {plan.cycle === 'Annual' ? 'anno' : 'mese'}</span>
            </p>
            <ul className="space-y-1.5">
              {planFeatures(plan).map((f) => (
                <li key={f} className="flex gap-2 text-sm text-text"><Check size={16} className="text-green-600 shrink-0 mt-0.5" />{f}</li>
              ))}
            </ul>
            <p className="text-xs text-text-muted flex gap-1.5">
              <BadgeCheck size={14} className="shrink-0 mt-0.5" />
              La scheda del negozio deve essere verificata dalla moderazione per comparire in evidenza. Ti serve la fattura? Scegli il bonifico e indicacelo.
            </p>
            <PayButtons plan={plan} />
          </section>
        ))
      )}

      <p className="text-xs text-text-muted flex gap-1.5 px-1">
        <Info size={14} className="shrink-0 mt-0.5" />
        Il piano riguarda solo l'uso di CardExchange. Scambi e pagamenti tra utenti restano accordi diretti tra voi, fuori dall'app.
      </p>

      <BottomSheet open={!!instructions} onClose={() => setInstructions(null)} title="Paga con bonifico">
        {instructions && (
          <div className="space-y-3">
            <p className="text-sm text-text-secondary">{instructions.message}</p>
            <div className="rounded-xl border border-border px-3">
              <CopyRow label="Importo" value={euro(instructions.amount)} />
              <CopyRow label="Intestatario" value={instructions.accountHolder} />
              <CopyRow label="IBAN" value={instructions.iban} />
              <CopyRow label="BIC / SWIFT" value={instructions.bic} />
              <CopyRow label="Banca" value={instructions.bankName} />
              <CopyRow label="Causale (obbligatoria)" value={instructions.reference} />
            </div>
            <p className="text-xs text-text-muted">Se la causale manca non possiamo collegare il bonifico al tuo account. La richiesta resta valida 30 giorni.</p>
            <Button className="w-full" onClick={() => setInstructions(null)}>Ho capito</Button>
          </div>
        )}
      </BottomSheet>
    </div>
  );
}
