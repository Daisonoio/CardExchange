import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Check, Copy, Loader2, Package, Trash2 } from 'lucide-react';
import { eventPackages, type EventPackageInfo, type ManagedExhibitor, type PackageTier } from '../../api';
import { apiErrorMessage } from '../../utils/apiError';
import { useFormat } from '../../i18n/format';
import { useEntitlements } from '../../hooks/useEntitlements';
import Button from '../ui/Button';

const input = 'w-full px-3 py-2 rounded-xl border border-border text-sm';

/** Area dell'organizzatore: pacchetto (Base, Plus, Regione), espositori con link d'invito, piantina, risultati. */
export default function OrganizerPackagePanel({ eventId }: { eventId: number }) {
  const { t } = useTranslation('organizers');
  const { euro } = useFormat();
  const { paidPlans } = useEntitlements();
  const [info, setInfo] = useState<EventPackageInfo | null>(null);
  const [list, setList] = useState<ManagedExhibitor[]>([]);
  const [form, setForm] = useState({ name: '', boothCode: '', description: '' });
  const [floor, setFloor] = useState('');
  const [copied, setCopied] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      const { data } = await eventPackages.get(eventId);
      setInfo(data);
      setFloor(data.floorPlanImageUrl ?? '');
      if (data.active) setList((await eventPackages.manage(eventId)).data);
    } catch { setInfo(null); }
  }, [eventId]);
  useEffect(() => { load(); }, [load]);

  const run = async (fn: () => Promise<unknown>) => {
    setBusy(true); setError(null);
    try { await fn(); await load(); } catch (err) { setError(apiErrorMessage(err, t('error'))); } finally { setBusy(false); }
  };
  const buy = (tier: PackageTier) => run(async () => {
    const { data } = await eventPackages.buy(eventId, tier);
    window.location.href = data.url;
  });
  const copy = async (x: ManagedExhibitor) => {
    const url = `${window.location.origin}/exhibit/${x.inviteToken}`;
    try { await navigator.clipboard.writeText(url); } catch { prompt(t('copyPrompt'), url); }
    setCopied(x.id);
  };

  if (!info) return null;
  // Piani spenti: senza espositori nel profilo non c'è niente da comprare né da mostrare
  if (!info.active && !paidPlans) return null;
  return (
    <section className="bg-white rounded-2xl border border-border/60 p-4 space-y-3" data-testid="organizer-package">
      <h2 className="text-sm font-bold text-text flex items-center gap-2"><Package size={16} className="text-primary" /> {t('panel.title')}</h2>
      {error && <p role="alert" className="text-sm p-3 rounded-xl bg-red-50 text-red-700">{error}</p>}

      {!info.active ? (
        <>
          <p className="text-xs text-text-secondary">{t('panel.intro')}</p>
          <ul className="grid sm:grid-cols-3 gap-2">
            {info.tiers.map((tier) => (
              <li key={tier.tier} className="p-3 rounded-xl border border-border">
                <strong className="block text-sm">{t(`tiers.${tier.tier}.name`)} · {euro(tier.priceEur)}</strong>
                <span className="block text-[11px] text-text-secondary mt-1">{t(`tiers.${tier.tier}.hint`, { km: tier.featuredRadiusKm })}</span>
                {info.canBuyByCard && <Button size="sm" className="mt-2 w-full" isLoading={busy} onClick={() => buy(tier.tier)}>{t('panel.buy')}</Button>}
              </li>
            ))}
          </ul>
          {!info.canBuyByCard && <p className="text-[11px] text-text-muted">{t('panel.bankTransfer')}</p>}
        </>
      ) : (
        <>
          <p className="text-xs text-text-secondary">{info.byProfile ? t('panel.byProfile') : t('panel.active', { tier: t(`tiers.${info.tier}.name`) })}</p>
          {info.results && (
            <dl className="grid grid-cols-4 gap-2 text-center">
              {(['pageViews', 'accepted', 'broadcasts', 'newFollowers'] as const).map((k) => (
                <div key={k} className="p-2 rounded-xl bg-surface-dark">
                  <dt className="text-[10px] text-text-muted">{t(`results.${k}`)}</dt>
                  <dd className="text-sm font-bold tabular-nums">{info.results![k]}</dd>
                </div>
              ))}
            </dl>
          )}

          <form onSubmit={(e) => { e.preventDefault(); run(async () => { await eventPackages.add(eventId, form); setForm({ name: '', boothCode: '', description: '' }); }); }}
            className="grid grid-cols-2 gap-2">
            <input className={input} required placeholder={t('panel.name')} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
            <input className={input} required placeholder={t('panel.booth')} value={form.boothCode} maxLength={40} onChange={(e) => setForm({ ...form, boothCode: e.target.value })} />
            <input className={`${input} col-span-2`} placeholder={t('panel.description')} value={form.description} maxLength={200} onChange={(e) => setForm({ ...form, description: e.target.value })} />
            <Button type="submit" size="sm" isLoading={busy}>{t('panel.add')}</Button>
          </form>

          <ul className="divide-y divide-border/60">
            {list.map((x) => (
              <li key={x.id} className="py-2 flex items-center gap-2 text-sm">
                <span className="w-14 shrink-0 text-[11px] font-bold text-primary">{x.boothCode}</span>
                <span className="flex-1 min-w-0">
                  <span className="block truncate">{x.name}</span>
                  <span className="block text-[11px] text-text-muted">{x.acceptedAt ? t('panel.accepted', { who: x.acceptedAs }) : t('panel.pending')}</span>
                </span>
                {!x.acceptedAt && (
                  <button type="button" onClick={() => copy(x)} className="inline-flex items-center gap-1 text-xs font-semibold text-primary">
                    {copied === x.id ? <Check size={13} /> : <Copy size={13} />} {copied === x.id ? t('panel.copied') : t('panel.copyLink')}
                  </button>
                )}
                <button type="button" onClick={() => confirm(t('panel.removeConfirm')) && run(() => eventPackages.remove(eventId, x.id))} aria-label={t('panel.remove')} className="p-1 text-danger"><Trash2 size={13} /></button>
              </li>
            ))}
          </ul>

          {(info.floorPlan || info.tier === 'Plus' || info.tier === 'Region') && (
            <div className="flex gap-2">
              <input className={input} type="url" placeholder={t('panel.floorPlan')} value={floor} onChange={(e) => setFloor(e.target.value)} />
              <Button size="sm" variant="outline" isLoading={busy} onClick={() => run(() => eventPackages.floorPlan(eventId, floor))}>{t('panel.save')}</Button>
            </div>
          )}
        </>
      )}
      {busy && <Loader2 className="animate-spin text-primary" size={14} />}
    </section>
  );
}
