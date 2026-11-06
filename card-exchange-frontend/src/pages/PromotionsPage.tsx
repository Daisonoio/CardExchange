import { useCallback, useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, CalendarDays, Coins, LayoutList, Loader2, Megaphone, Radio, Store, Users } from 'lucide-react';
import { promotions, type Promotion, type PromotionEstimate, type PromotionPricing, type PromotionTypeName } from '../api';
import { apiErrorMessage } from '../utils/apiError';
import { formatEventDate } from '../utils/eventDate';
import { useFormat } from '../i18n/format';
import Button from '../components/ui/Button';
import EmptyState from '../components/ui/EmptyState';

type Targets = Awaited<ReturnType<typeof promotions.targets>>['data'];

/** Creazione guidata in 3 passi: cosa promuovere, dove, anteprima con persone raggiungibili e prezzo. */
function Wizard({ pricing, initialType, initialTarget, onDone }: {
  pricing: PromotionPricing; initialType?: PromotionTypeName; initialTarget?: number; onDone: () => void;
}) {
  const { t } = useTranslation('promotions');
  const { euro } = useFormat();
  const [targets, setTargets] = useState<Targets | null>(null);
  const [step, setStep] = useState(initialTarget ? 2 : 1);
  const [type, setType] = useState<PromotionTypeName>(initialType ?? 'FeaturedEvent');
  const [targetId, setTargetId] = useState<number | null>(initialTarget ?? null);
  const [radius, setRadius] = useState(10);
  const [estimate, setEstimate] = useState<PromotionEstimate | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [slotText, setSlotText] = useState({ title: '', body: '' });
  const isSlot = type === 'StoreSlot' || type === 'ShowcaseSlot';

  useEffect(() => { promotions.targets().then(({ data }) => setTargets(data)).catch(() => setTargets({ events: [], broadcasts: [], stores: [], showcases: [] })); }, []);
  useEffect(() => {
    if (step !== 3 || targetId == null) return;
    let cancelled = false;
    promotions.estimate(type, targetId, radius)
      .then(({ data }) => { if (!cancelled) { setEstimate(data); setError(null); } })
      .catch((err) => { if (!cancelled) { setEstimate(null); setError(apiErrorMessage(err, t('errors.generic'))); } });
    return () => { cancelled = true; };
  }, [step, type, targetId, radius, t]);

  const pay = async (method: 'credits' | 'card') => {
    if (targetId == null) return;
    setBusy(true); setError(null);
    try {
      const { data: promo } = await promotions.create(type, targetId, radius, undefined, isSlot ? slotText : undefined);
      const { data } = await promotions.pay(promo.id, method);
      if (data.url) { window.location.href = data.url; return; }
      onDone();
    } catch (err) {
      setError(apiErrorMessage(err, t('errors.generic')));
    } finally {
      setBusy(false);
    }
  };

  const options = type === 'FeaturedEvent'
    ? (targets?.events ?? []).filter((e) => e.hasLocation).map((e) => ({ id: e.id, title: e.title, sub: `${formatEventDate(e.startsAt)} · ${e.city}` }))
    : type === 'SponsoredBroadcast'
      ? (targets?.broadcasts ?? []).map((b) => ({ id: b.id, title: b.title, sub: t('wizard.until', { time: new Date(b.endsAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) }) }))
      : type === 'StoreSlot'
        ? (targets?.stores ?? []).map((x) => ({ id: x.id, title: x.title, sub: x.city }))
        : (targets?.showcases ?? []).map((x) => ({ id: x.id, title: x.title, sub: `/v/${x.slug}` }));
  const icons = { FeaturedEvent: CalendarDays, SponsoredBroadcast: Radio, StoreSlot: Store, ShowcaseSlot: LayoutList };
  const price = pricing.types.find((x) => x.type === type)?.prices.find((p) => radius <= p.upToKm);
  const enough = price != null && pricing.balance >= price.credits;

  return (
    <section className="bg-white rounded-2xl border border-border/60 p-4 space-y-4" aria-label={t('wizard.title')}>
      <ol className="flex gap-2 text-[11px] font-semibold">
        {[1, 2, 3].map((n) => (
          <li key={n} className={`flex-1 text-center py-1 rounded-full ${step === n ? 'bg-primary text-white' : 'bg-surface-dark text-text-muted'}`}>{t(`wizard.step${n}`)}</li>
        ))}
      </ol>

      {step === 1 && (
        <div className="space-y-3">
          <div className="grid grid-cols-2 gap-2">
            {(['FeaturedEvent', 'SponsoredBroadcast', 'StoreSlot', 'ShowcaseSlot'] as PromotionTypeName[]).map((k) => {
              const Icon = icons[k];
              return (
              <button key={k} type="button" onClick={() => { setType(k); setTargetId(null); }} aria-pressed={type === k}
                className={`p-3 rounded-xl border text-left ${type === k ? 'border-primary bg-primary/5' : 'border-border'}`}>
                <Icon size={18} className="text-primary" />
                <strong className="block text-sm mt-1">{t(`types.${k}.name`)}</strong>
                <span className="block text-[11px] text-text-secondary">{t(`types.${k}.hint`)}</span>
              </button>
              );
            })}
          </div>
          {targets === null ? <Loader2 className="animate-spin text-primary mx-auto" /> : options.length === 0 ? (
            <p className="text-sm text-text-muted">{t(`wizard.none.${type}`)}</p>
          ) : (
            <ul className="space-y-1.5">
              {options.map((o) => (
                <li key={o.id}>
                  <button type="button" onClick={() => setTargetId(o.id)} aria-pressed={targetId === o.id}
                    className={`w-full p-2.5 rounded-xl border text-left ${targetId === o.id ? 'border-primary bg-primary/5' : 'border-border'}`}>
                    <span className="block text-sm font-semibold truncate">{o.title}</span>
                    <span className="block text-[11px] text-text-muted">{o.sub}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
          <Button className="w-full" disabled={targetId == null} onClick={() => setStep(2)}>{t('wizard.next')}</Button>
        </div>
      )}

      {step === 2 && (
        <div className="space-y-3">
          <label className="block">
            <span className="text-xs font-semibold text-text-secondary">{t('wizard.radius', { km: radius })}</span>
            <input type="range" min={pricing.minRadiusKm} max={pricing.maxRadiusKm} step={5} value={radius}
              onChange={(e) => setRadius(Number(e.target.value))} className="w-full accent-primary" />
          </label>
          <p className="text-xs text-text-secondary">{t(`wizard.duration.${type}`)}</p>
          {isSlot && (
            <>
              <label className="block">
                <span className="text-xs font-semibold text-text-secondary">{t('wizard.slotTitle')}</span>
                <input value={slotText.title} maxLength={80} onChange={(e) => setSlotText({ ...slotText, title: e.target.value })}
                  className="mt-1 w-full px-3 py-2 rounded-xl border border-border text-sm" placeholder={t('wizard.slotTitlePlaceholder')} />
              </label>
              <label className="block">
                <span className="text-xs font-semibold text-text-secondary">{t('wizard.slotBody')}</span>
                <textarea value={slotText.body} maxLength={200} rows={2} onChange={(e) => setSlotText({ ...slotText, body: e.target.value })}
                  className="mt-1 w-full px-3 py-2 rounded-xl border border-border text-sm" />
              </label>
            </>
          )}
          <div className="flex gap-2">
            <Button variant="outline" onClick={() => setStep(1)}>{t('wizard.back')}</Button>
            <Button className="flex-1" disabled={isSlot && slotText.title.trim().length < 3} onClick={() => setStep(3)}>{t('wizard.preview')}</Button>
          </div>
        </div>
      )}

      {step === 3 && (
        <div className="space-y-3">
          <div className="grid grid-cols-2 gap-2 text-sm">
            <div className="p-3 rounded-xl bg-surface-dark">
              <dt className="text-[11px] text-text-muted">{t('wizard.inRadius')}</dt>
              <dd className="text-lg font-bold">{estimate ? (estimate.inRadius || t('fewer')) : '…'}</dd>
            </div>
            <div className="p-3 rounded-xl bg-surface-dark">
              <dt className="text-[11px] text-text-muted">{isSlot ? t('wizard.couldSee') : t('wizard.reachable')}</dt>
              <dd className="text-lg font-bold" data-testid="promo-reachable">{estimate ? (estimate.reachable || t('fewer')) : '…'}</dd>
            </div>
          </div>
          {isSlot && (
            <div className="p-3 rounded-2xl border border-amber-200 bg-amber-50/60">
              <span className="block text-[10px] font-bold uppercase tracking-wide text-amber-800">{t('label')}</span>
              <span className="block text-sm font-bold">{slotText.title}</span>
              {slotText.body && <span className="block text-xs text-text-secondary">{slotText.body}</span>}
            </div>
          )}
          <p className="text-[11px] text-text-muted">{t(`wizard.how.${type}`)}</p>
          {price && (
            <p className="text-sm font-semibold">{t('wizard.price', { credits: price.credits, eur: euro(price.eur) })}</p>
          )}
          {error && <p role="alert" className="text-sm p-3 rounded-xl bg-red-50 text-red-700">{error}</p>}
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" onClick={() => setStep(2)}>{t('wizard.back')}</Button>
            <Button className="flex-1" isLoading={busy} disabled={!enough} onClick={() => pay('credits')}>
              <Coins size={15} /> {t('wizard.payCredits')}
            </Button>
          </div>
          {!enough && (
            <p className="text-xs text-text-secondary">
              {t('wizard.notEnough', { balance: pricing.balance })}{' '}
              <Link to="/purchases" className="text-primary font-semibold">{t('wizard.buyCredits')}</Link>{' · '}
              <button type="button" className="text-primary font-semibold" onClick={() => pay('card')}>{t('wizard.payCard')}</button>
            </p>
          )}
        </div>
      )}
    </section>
  );
}

const STATUS_STYLE: Record<string, string> = {
  Active: 'bg-green-100 text-green-800', PendingPayment: 'bg-amber-100 text-amber-800', Completed: 'bg-surface-dark text-text-secondary',
  Rejected: 'bg-red-100 text-red-700', Cancelled: 'bg-surface-dark text-text-muted', Draft: 'bg-surface-dark text-text-muted',
};

/** Le mie promozioni: saldo crediti, creazione guidata, risultati. */
export default function PromotionsPage() {
  const { t } = useTranslation('promotions');
  const [params] = useSearchParams();
  const [pricing, setPricing] = useState<PromotionPricing | null>(null);
  const [list, setList] = useState<Promotion[] | null>(null);
  const preType = params.get('type') === 'broadcast' ? 'SponsoredBroadcast' : params.get('type') === 'event' ? 'FeaturedEvent' : undefined;
  const preTarget = params.get('target') ? Number(params.get('target')) : undefined;
  const [creating, setCreating] = useState(!!preType);
  const [notice, setNotice] = useState<string | null>(params.get('checkout') === 'success' ? t('paidByCard') : null);

  const load = useCallback(() => {
    promotions.pricing().then(({ data }) => setPricing(data)).catch(() => setPricing(null));
    promotions.mine().then(({ data }) => setList(data)).catch(() => setList([]));
  }, []);
  useEffect(() => { load(); }, [load]);

  const cancel = async (p: Promotion) => {
    if (!confirm(t('cancelConfirm'))) return;
    await promotions.cancel(p.id).catch(() => undefined);
    load();
  };

  return (
    <div className="pb-24 space-y-4 max-w-2xl">
      <div className="flex items-center gap-2">
        <Link to="/profile" className="p-1.5 -ml-1.5 rounded-lg hover:bg-surface-hover" aria-label={t('common:actions.back')}><ArrowLeft size={20} /></Link>
        <h1 className="text-xl font-bold text-text flex items-center gap-2"><Megaphone size={20} className="text-primary" /> {t('title')}</h1>
      </div>
      <p className="text-sm text-text-secondary">{t('intro')}</p>
      {notice && <p role="status" className="text-sm p-3 rounded-xl bg-green-50 text-green-800">{notice}</p>}

      {pricing && (
        <div className="flex items-center justify-between p-3 rounded-2xl bg-white border border-border/60">
          <span className="text-sm"><Coins size={15} className="inline text-amber-600 -mt-0.5" /> {t('balance', { count: pricing.balance })}</span>
          <span className="text-[11px] text-text-muted">{pricing.monthlyCredits > 0 ? t('monthly', { count: pricing.monthlyCredits }) : <Link to="/purchases" className="text-primary font-semibold">{t('wizard.buyCredits')}</Link>}</span>
        </div>
      )}

      {pricing && (creating
        ? <Wizard pricing={pricing} initialType={preType} initialTarget={preTarget} onDone={() => { setCreating(false); setNotice(t('activated')); load(); }} />
        : <Button className="w-full" onClick={() => setCreating(true)}><Megaphone size={16} /> {t('create')}</Button>)}

      {list === null ? (
        <div className="flex justify-center py-8"><Loader2 className="animate-spin text-primary" /></div>
      ) : list.length === 0 ? (
        !creating && <EmptyState icon={Megaphone} title={t('empty.title')} description={t('empty.description')} />
      ) : (
        <ul className="space-y-2">
          {list.map((p) => (
            <li key={p.id} className="bg-white rounded-2xl border border-border/60 p-3 space-y-2">
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <span className="block text-[11px] text-text-muted">{t(`types.${p.type}.name`)} · {p.radiusKm} km</span>
                  <strong className="block text-sm text-text truncate">{p.title ?? `#${p.targetId}`}</strong>
                </div>
                <span className={`shrink-0 px-2 py-0.5 rounded-full text-[11px] font-semibold ${STATUS_STYLE[p.status] ?? ''}`}>{t(`status.${p.status}`)}</span>
              </div>
              <dl className="grid grid-cols-3 gap-2 text-center">
                {(['reach', 'impressions', 'clicks'] as const).map((k) => (
                  <div key={k} className="p-2 rounded-xl bg-surface-dark">
                    <dt className="text-[10px] text-text-muted flex items-center justify-center gap-1">{k === 'reach' && <Users size={10} />}{t(`stats.${k}`)}</dt>
                    <dd className="text-sm font-bold tabular-nums">{p[k]}</dd>
                  </div>
                ))}
              </dl>
              {p.rejectionReason && <p className="text-xs text-red-700">{p.rejectionReason}</p>}
              {(p.status === 'PendingPayment' || (p.status === 'Active' && !p.deliveredAt)) && (
                <button type="button" onClick={() => cancel(p)} className="text-xs text-danger font-semibold">{t('cancel')}</button>
              )}
            </li>
          ))}
        </ul>
      )}
      <p className="text-[11px] text-text-muted">{t('rules')}</p>
    </div>
  );
}
