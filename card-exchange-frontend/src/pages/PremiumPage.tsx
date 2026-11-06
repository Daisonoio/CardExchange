import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { ArrowLeft, BadgeCheck, Check, Copy, CreditCard, Crown, Info, Landmark, Loader2, Minus, Store as StoreIcon, Tent } from 'lucide-react';
import { Trans, useTranslation } from 'react-i18next';
import { billing } from '../api';
import { formatDate, formatEuro } from '../i18n/format';
import type { BankTransferInstructions, BillingOptions, SubscriptionPlan, SubscriptionState, UserSubscription } from '../types';
import { isStoreTier } from '../types';
import { refreshEntitlements } from '../hooks/useEntitlements';
import { apiErrorMessage } from '../utils/apiError';
import Button from '../components/ui/Button';
import BottomSheet from '../components/ui/BottomSheet';

const euro = (v: number) => formatEuro(v);
const day = (iso: string) => formatDate(iso, { day: 'numeric', month: 'long', year: 'numeric' });

// Chiavi del namespace "premium"
const TIER_LABEL = { Free: 'tier.free', Premium: 'tier.premium', Store: 'tier.store', Seller: 'tier.seller', StorePro: 'tier.storePro' } as const;
const METHOD_LABEL: Record<UserSubscription['paymentMethod'], string> = {
  Stripe: 'method.stripe',
  BankTransfer: 'method.bankTransfer',
  Manual: 'method.manual',
  Simulated: 'method.simulated',
};

const FREE_FEATURES = [
  'features.free.collection',
  'features.free.match',
  'features.free.stores',
  'features.free.limits',
];

const has = (p: SubscriptionPlan, c: string) => !!p.capabilities?.includes(c);
const isStore = (p: SubscriptionPlan) => isStoreTier(p.tier);

/** Funzioni incluse nel piano: [chiave nel namespace "premium", valori]. */
function planFeatures(p: SubscriptionPlan): [string, Record<string, unknown>?][] {
  const f: [string, Record<string, unknown>?][] = [];
  if (has(p, 'FeaturedStore')) f.push(['features.storeListing']);
  if (has(p, 'LocalDemand')) f.push(['features.localDemand']);
  if (has(p, 'Buylist')) f.push(['features.buylist']);
  if (has(p, 'Reservations')) f.push(['features.reservations']);
  if (has(p, 'SellerShowcase')) f.push(['features.sellerShowcase']);
  if ((p.monthlyPromotionCredits ?? 0) > 0) f.push(['features.promoCredits', { count: p.monthlyPromotionCredits }]);
  if ((p.maxBroadcastsPerDay ?? 3) > 3) f.push(['features.broadcasts', { count: p.maxBroadcastsPerDay }]);
  if (has(p, 'InstantAlerts')) f.push(['features.instantAlerts']);
  if (p.maxRadiusKm) f.push(['features.radius', { km: p.maxRadiusKm }]);
  if (has(p, 'EarlyAccess')) f.push(['features.earlyAccess']);
  f.push(['features.unlimited']);
  if (has(p, 'AdvancedSearch')) f.push(['features.advancedSearch']);
  if (has(p, 'Statistics')) f.push(['features.statistics']);
  if (has(p, 'ExportCollection')) f.push(['features.export']);
  if (has(p, 'PriorityListing')) f.push(['features.priority']);
  if (has(p, 'VerifiedBadge')) f.push([isStore(p) ? 'features.storeBadge' : 'features.premiumBadge']);
  return f;
}

/** Righe della tabella di confronto: funzione o limite. */
const COMPARE: { key: string; cap?: string; value?: (p: SubscriptionPlan | null) => string | null }[] = [
  { key: 'compare.radius', value: (p) => `${p?.maxRadiusKm ?? 30} km` },
  { key: 'compare.alerts', value: (p) => (p && has(p, 'InstantAlerts') ? 'instant' : 'evening') },
  { key: 'compare.earlyAccess', cap: 'EarlyAccess' },
  { key: 'compare.unlimited', value: (p) => (p ? 'yes' : null) },
  { key: 'compare.advancedSearch', cap: 'AdvancedSearch' },
  { key: 'compare.statistics', cap: 'Statistics' },
  { key: 'compare.broadcasts', value: (p) => String(p?.maxBroadcastsPerDay ?? 3) },
  { key: 'compare.promoCredits', value: (p) => String(p?.monthlyPromotionCredits ?? 0) },
  { key: 'compare.sellerShowcase', cap: 'SellerShowcase' },
  { key: 'compare.storeListing', cap: 'FeaturedStore' },
  { key: 'compare.localDemand', cap: 'LocalDemand' },
  { key: 'compare.buylist', cap: 'Buylist' },
  { key: 'compare.reservations', cap: 'Reservations' },
];

function CopyRow({ label, value }: { label: string; value?: string }) {
  const { t } = useTranslation('premium');
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
      <button onClick={copy} aria-label={t('bank.copy', { label })} className="w-8 h-8 rounded-lg hover:bg-surface-dark flex items-center justify-center text-primary shrink-0">
        {copied ? <Check size={16} /> : <Copy size={16} />}
      </button>
    </div>
  );
}

export default function PremiumPage() {
  const { t } = useTranslation('premium');
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
  const storeTier = isStoreTier(state?.tier);
  useEffect(() => {
    if (storeTier && !params.get('for')) setAudience('store');
  }, [storeTier]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    Promise.all([billing.plans(), billing.options(), loadState()])
      .then(([p, o]) => {
        setPlans(p.data);
        setOptions(o.data);
      })
      .catch((err) => setError(apiErrorMessage(err, t('errors.loadPlans'))));
  }, [loadState, t]);

  // Ritorno da Stripe: l'attivazione arriva dal webhook, quindi si attende qualche secondo
  useEffect(() => {
    if (checkout !== 'success') return;
    let tries = 0;
    let cancelled = false;
    const poll = async () => {
      const data = await loadState().catch(() => null);
      if (cancelled) return;
      if (data?.active?.paymentMethod === 'Stripe' || tries++ >= 10) { refreshEntitlements(); return; }
      setTimeout(poll, 2000);
    };
    poll();
    return () => { cancelled = true; };
  }, [checkout, loadState]);

  const premium = useMemo(() => (plans ?? []).filter((p) => p.tier === 'Premium'), [plans]);
  const storePlans = useMemo(() => (plans ?? []).filter(isStore), [plans]);
  const sellerPlans = useMemo(() => (plans ?? []).filter((p) => p.tier === 'Seller'), [plans]);
  // Colonne della tabella di confronto: Gratuito, Premium (mensile), Banco, Negozio, Negozio Pro
  const columns: (SubscriptionPlan | null)[] = useMemo(() => [
    null,
    (plans ?? []).find((p) => p.tier === 'Premium' && p.cycle === 'Monthly') ?? null,
    (plans ?? []).find((p) => p.tier === 'Seller') ?? null,
    (plans ?? []).find((p) => p.tier === 'Store') ?? null,
    (plans ?? []).find((p) => p.tier === 'StorePro') ?? null,
  ].filter((p, i) => i === 0 || p !== null), [plans]);
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
      setError(apiErrorMessage(err, t('errors.cardUnavailable')));
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
      setError(apiErrorMessage(err, t('errors.requestFailed')));
    } finally {
      setBusy(null);
    }
  };

  const showInstructions = (s: UserSubscription) => {
    const plan = plans?.find((p) => p.name === s.planName);
    if (plan) payByBank(plan);
  };

  const cancelRequest = async (s: UserSubscription) => {
    if (!confirm(t('confirmCancelRequest'))) return;
    setBusy(`del-${s.id}`);
    try {
      await billing.cancelBankTransfer(s.id);
      await loadState();
    } catch (err) {
      setError(apiErrorMessage(err, t('errors.operationFailed')));
    } finally {
      setBusy(null);
    }
  };

  const cancelRenewal = async () => {
    if (!confirm(t('confirmCancelRenewal'))) return;
    setBusy('cancel');
    try {
      const { data } = await billing.cancelRenewal();
      setNotice(data.message);
      await loadState();
    } catch (err) {
      setError(apiErrorMessage(err, t('errors.cancelRenewalFailed')));
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
      setError(apiErrorMessage(err, t('errors.portalUnavailable')));
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
      return <p className="text-xs text-text-muted text-center">{t('pay.unavailable')}</p>;
    }
    return (
      <div className="flex flex-col gap-2">
        {cardEnabled && (
          <Button onClick={() => payByCard(plan)} isLoading={busy === `card-${plan.id}`} disabled={!!busy || stripeAutoRenew}>
            <CreditCard size={16} /> {t('pay.card')}
          </Button>
        )}
        {bankEnabled && (
          <Button variant="outline" onClick={() => payByBank(plan)} isLoading={busy === `bank-${plan.id}`} disabled={!!busy}>
            <Landmark size={16} /> {pendingForPlan ? t('pay.bankInstructions') : t('pay.bank')}
          </Button>
        )}
        {stripeAutoRenew && cardEnabled && (
          <p className="text-[11px] text-text-muted text-center">{t('pay.alreadyAutoRenew')}</p>
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
        <button onClick={() => navigate(-1)} aria-label={t('common:actions.back')} className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center shrink-0">
          <ArrowLeft size={18} />
        </button>
        <h1 className="text-xl font-bold text-text flex-1">{t('title')}</h1>
      </div>

      {checkout === 'success' && (
        <div className="p-3 rounded-xl bg-green-50 text-green-800 text-sm flex gap-2">
          {active?.paymentMethod === 'Stripe' ? <Check size={18} className="shrink-0" /> : <Loader2 size={18} className="animate-spin shrink-0" />}
          <span className="flex-1">
            {active?.paymentMethod === 'Stripe' ? t('checkout.success', { plan: active.planName }) : t('checkout.activating')}
          </span>
          <button onClick={dismissCheckout} className="text-xs font-semibold">OK</button>
        </div>
      )}
      {checkout === 'cancelled' && (
        <div className="p-3 rounded-xl bg-amber-50 text-amber-800 text-sm flex gap-2">
          <span className="flex-1">{t('checkout.cancelled')}</span>
          <button onClick={dismissCheckout} className="text-xs font-semibold">OK</button>
        </div>
      )}
      {error && <div className="p-3 rounded-xl bg-red-50 text-danger text-sm">{error}</div>}
      {notice && <div className="p-3 rounded-xl bg-blue-50 text-blue-800 text-sm">{notice}</div>}

      {/* Stato attuale */}
      <section className="p-4 rounded-2xl bg-white border border-border space-y-2">
        <div className="flex items-center gap-3">
          <span className={`w-10 h-10 rounded-xl flex items-center justify-center shrink-0 ${state.tier === 'Free' ? 'bg-surface-dark text-text-secondary' : 'bg-secondary/15 text-amber-700'}`}>
            {isStoreTier(state.tier) ? <StoreIcon size={20} /> : <Crown size={20} />}
          </span>
          <div className="flex-1 min-w-0">
            <p className="text-[11px] text-text-muted">{t('current.label')}</p>
            <p className="text-base font-bold text-text">{active ? active.planName : t(TIER_LABEL.Free)}</p>
          </div>
        </div>
        {active && (
          <p className="text-sm text-text-secondary">
            <Trans
              t={t}
              i18nKey={active.autoRenew ? 'current.renewsOn' : 'current.activeUntil'}
              values={{ date: day(active.endDate) }}
              components={{ strong: <strong /> }}
            />
            {' · '}{t('current.paidWith', { method: t(METHOD_LABEL[active.paymentMethod]) })}
          </p>
        )}
        {state.queued.map((q) => (
          <p key={q.id} className="text-xs text-text-secondary">
            {t('current.queued', { plan: q.planName, from: day(q.startDate), to: day(q.endDate) })}
          </p>
        ))}
        {active && (
          <div className="flex flex-wrap gap-2 pt-1">
            {active.paymentMethod === 'Stripe' && cardEnabled && (
              <Button size="sm" variant="outline" onClick={openPortal} isLoading={busy === 'portal'}>{t('current.managePayment')}</Button>
            )}
            {active.autoRenew && (
              <Button size="sm" variant="ghost" onClick={cancelRenewal} isLoading={busy === 'cancel'}>{t('current.cancelRenewal')}</Button>
            )}
          </div>
        )}
      </section>

      {/* Bonifici in attesa */}
      {state.pending.map((p) => (
        <section key={p.id} className="p-4 rounded-2xl bg-amber-50 border border-amber-200 space-y-2">
          <p className="text-sm font-semibold text-amber-900">{t('pending.title', { plan: p.planName })}</p>
          <p className="text-xs text-amber-900/80">
            <Trans
              t={t}
              i18nKey="pending.reference"
              values={{ reference: p.paymentReference }}
              components={{ strong: <strong className="select-all" /> }}
            />
          </p>
          <div className="flex gap-2">
            <Button size="sm" variant="outline" onClick={() => showInstructions(p)} disabled={!!busy}>{t('pending.instructions')}</Button>
            <Button size="sm" variant="ghost" onClick={() => cancelRequest(p)} isLoading={busy === `del-${p.id}`}>{t('pending.cancel')}</Button>
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
            {a === 'private' ? t('audience.private') : t('audience.store')}
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
                        {c === 'Monthly' ? t('cycle.monthly') : annualSaving > 0 ? t('cycle.annualSaving', { saving: annualSaving }) : t('cycle.annual')}
                      </button>
                    ))}
                  </div>
                )}
              </div>
              <p>
                <span className="text-3xl font-bold text-text">{euro(shownPremium.price)}</span>
                <span className="text-sm text-text-secondary"> / {shownPremium.cycle === 'Annual' ? t('cycle.perYear') : t('cycle.perMonth')}</span>
                {shownPremium.cycle === 'Annual' && (
                  <span className="block text-xs text-text-muted">{t('cycle.monthlyEquivalent', { price: euro(shownPremium.price / 12) })}</span>
                )}
              </p>
              <ul className="space-y-1.5">
                {planFeatures(shownPremium).map(([f, v]) => (
                  <li key={f} className="flex gap-2 text-sm text-text"><Check size={16} className="text-green-600 shrink-0 mt-0.5" />{t(f, v)}</li>
                ))}
              </ul>
              <PayButtons plan={shownPremium} />
            </section>
          )}

          {sellerPlans.map((plan) => (
            <section key={plan.id} className="p-4 rounded-2xl bg-white border border-border space-y-3">
              <h2 className="text-lg font-bold text-text flex items-center gap-1.5"><Tent size={18} className="text-primary" /> {plan.name}</h2>
              {plan.description && <p className="text-sm text-text-secondary">{plan.description}</p>}
              <p>
                <span className="text-3xl font-bold text-text">{euro(plan.price)}</span>
                <span className="text-sm text-text-secondary"> / {plan.cycle === 'Annual' ? t('cycle.perYear') : t('cycle.perMonth')}</span>
              </p>
              <ul className="space-y-1.5">
                {planFeatures(plan).map(([f, v]) => (
                  <li key={f} className="flex gap-2 text-sm text-text"><Check size={16} className="text-green-600 shrink-0 mt-0.5" />{t(f, v)}</li>
                ))}
              </ul>
              <PayButtons plan={plan} />
            </section>
          ))}

          <section className="p-4 rounded-2xl bg-white border border-border space-y-2">
            <h2 className="text-base font-bold text-text">{t(TIER_LABEL.Free)}</h2>
            <ul className="space-y-1.5">
              {FREE_FEATURES.map((f) => (
                <li key={f} className="flex gap-2 text-sm text-text-secondary"><Check size={16} className="text-text-muted shrink-0 mt-0.5" />{t(f)}</li>
              ))}
            </ul>
          </section>
        </>
      ) : storePlans.length === 0 ? (
        <p className="text-sm text-text-muted text-center py-6">{t('store.comingSoon')}</p>
      ) : (
        storePlans.map((plan) => (
          <section key={plan.id} className={`p-4 rounded-2xl bg-white space-y-3 ${plan.tier === 'StorePro' ? 'border-2 border-primary' : 'border border-border'}`}>
            <h2 className="text-lg font-bold text-text flex items-center gap-1.5"><StoreIcon size={18} className="text-primary" /> {plan.name}</h2>
            {plan.description && <p className="text-sm text-text-secondary">{plan.description}</p>}
            <p>
              <span className="text-3xl font-bold text-text">{euro(plan.price)}</span>
              <span className="text-sm text-text-secondary"> / {plan.cycle === 'Annual' ? t('cycle.perYear') : t('cycle.perMonth')}</span>
            </p>
            <ul className="space-y-1.5">
              {planFeatures(plan).map(([f, v]) => (
                <li key={f} className="flex gap-2 text-sm text-text"><Check size={16} className="text-green-600 shrink-0 mt-0.5" />{t(f, v)}</li>
              ))}
            </ul>
            <p className="text-xs text-text-muted flex gap-1.5">
              <BadgeCheck size={14} className="shrink-0 mt-0.5" />
              {t('store.verificationNote')}
            </p>
            <PayButtons plan={plan} />
          </section>
        ))
      )}

      {columns.length > 1 && (
        <section className="bg-white rounded-2xl border border-border p-3">
          <h2 className="text-sm font-semibold text-text mb-2">{t('compare.title')}</h2>
          <div className="overflow-x-auto -mx-3 px-3">
            <table className="w-full min-w-[34rem] text-xs">
              <thead>
                <tr className="text-text-secondary">
                  <th className="sticky left-0 bg-white text-left font-medium py-1.5 pr-2" />
                  {columns.map((p, i) => (
                    <th key={i} className="font-semibold text-text px-1.5 text-center whitespace-nowrap">
                      {p ? p.name.replace(' Mensile', '') : t(TIER_LABEL.Free)}
                      <span className="block font-normal text-text-muted">{p ? `${euro(p.price)}/${t('cycle.perMonthShort')}` : '0 €'}</span>
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {COMPARE.map((row) => (
                  <tr key={row.key} className="border-t border-border/60">
                    <th scope="row" className="sticky left-0 bg-white text-left font-normal text-text-secondary py-1.5 pr-2">{t(row.key)}</th>
                    {columns.map((p, i) => {
                      const v = row.cap ? (p && has(p, row.cap) ? 'yes' : null) : row.value!(p);
                      return (
                        <td key={i} className="text-center px-1.5">
                          {v === 'yes' ? <Check size={14} className="inline text-green-600" aria-label={t('compare.yes')} />
                            : v === null || v === '0' ? <Minus size={14} className="inline text-text-muted" aria-label={t('compare.no')} />
                            : v === 'instant' ? t('compare.instant') : v === 'evening' ? t('compare.evening') : v}
                        </td>
                      );
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      <p className="text-xs text-text-muted flex gap-1.5 px-1">
        <Info size={14} className="shrink-0 mt-0.5" />
        {t('note')}
      </p>

      <BottomSheet open={!!instructions} onClose={() => setInstructions(null)} title={t('pay.bank')}>
        {instructions && (
          <div className="space-y-3">
            <p className="text-sm text-text-secondary">{instructions.message}</p>
            <div className="rounded-xl border border-border px-3">
              <CopyRow label={t('bank.amount')} value={euro(instructions.amount)} />
              <CopyRow label={t('bank.accountHolder')} value={instructions.accountHolder} />
              <CopyRow label="IBAN" value={instructions.iban} />
              <CopyRow label="BIC / SWIFT" value={instructions.bic} />
              <CopyRow label={t('bank.bankName')} value={instructions.bankName} />
              <CopyRow label={t('bank.reference')} value={instructions.reference} />
            </div>
            <p className="text-xs text-text-muted">{t('bank.referenceNote')}</p>
            <Button className="w-full" onClick={() => setInstructions(null)}>{t('bank.gotIt')}</Button>
          </div>
        )}
      </BottomSheet>
    </div>
  );
}
