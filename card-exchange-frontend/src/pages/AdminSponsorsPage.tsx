import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Loader2, Megaphone, Trash2 } from 'lucide-react';
import { sponsored, type AdminSponsor } from '../api';
import { apiErrorMessage } from '../utils/apiError';
import Button from '../components/ui/Button';

const SCOPES = { Event: 1, CityTradeNights: 2, Month: 3 } as const;
const PLACEMENTS = { EventPage: 1, TradeNightBanner: 2, Slot: 3 } as const;
const SLOTS = { HomeFeed: 1, ExploreCards: 2, ExploreStores: 3 } as const;
const input = 'w-full px-3 py-2 rounded-xl border border-border text-sm';
const today = () => new Date().toISOString().slice(0, 10);
const inDays = (n: number) => new Date(Date.now() + n * 864e5).toISOString().slice(0, 10);

function ShipForm({ sponsorId, onSaved }: { sponsorId: number; onSaved: () => void }) {
  const { t } = useTranslation('promotions');
  const [f, setF] = useState({ scope: 'CityTradeNights' as keyof typeof SCOPES, scopeRef: '', placement: 'TradeNightBanner' as keyof typeof PLACEMENTS,
    from: today(), to: inDays(30), slot: 'HomeFeed' as keyof typeof SLOTS, slotTitle: '', slotBody: '' });
  const [error, setError] = useState<string | null>(null);
  const save = async () => {
    setError(null);
    try {
      await sponsored.adminSaveShip({
        sponsorId, scope: SCOPES[f.scope], scopeRef: f.scopeRef || undefined, placement: PLACEMENTS[f.placement],
        startsAt: new Date(f.from).toISOString(), endsAt: new Date(f.to + 'T23:59:59').toISOString(),
        ...(f.placement === 'Slot' ? { slotPlacement: SLOTS[f.slot], slotTitle: f.slotTitle, slotBody: f.slotBody } : {}),
      });
      onSaved();
    } catch (err) { setError(apiErrorMessage(err, t('errors.generic'))); }
  };
  return (
    <div className="mt-2 p-3 rounded-xl bg-surface-dark space-y-2">
      <strong className="text-xs">{t('admin.newShip')}</strong>
      <div className="grid grid-cols-2 gap-2">
        <select className={input} value={f.scope} onChange={(e) => setF({ ...f, scope: e.target.value as keyof typeof SCOPES })} aria-label={t('admin.scope')}>
          {Object.keys(SCOPES).map((k) => <option key={k} value={k}>{t(`admin.scopes.${k}`)}</option>)}
        </select>
        <input className={input} value={f.scopeRef} placeholder={t('admin.scopeRef')} onChange={(e) => setF({ ...f, scopeRef: e.target.value })} />
        <select className={input} value={f.placement} onChange={(e) => setF({ ...f, placement: e.target.value as keyof typeof PLACEMENTS })} aria-label={t('admin.placement')}>
          {Object.keys(PLACEMENTS).map((k) => <option key={k} value={k}>{t(`admin.placements.${k}`)}</option>)}
        </select>
        <div className="flex gap-1">
          <input type="date" className={input} value={f.from} onChange={(e) => setF({ ...f, from: e.target.value })} aria-label={t('admin.from')} />
          <input type="date" className={input} value={f.to} onChange={(e) => setF({ ...f, to: e.target.value })} aria-label={t('admin.to')} />
        </div>
        {f.placement === 'Slot' && (
          <>
            <select className={input} value={f.slot} onChange={(e) => setF({ ...f, slot: e.target.value as keyof typeof SLOTS })} aria-label={t('admin.slotPlacement')}>
              {Object.keys(SLOTS).map((k) => <option key={k} value={k}>{t(`admin.slots.${k}`)}</option>)}
            </select>
            <input className={input} value={f.slotTitle} placeholder={t('admin.slotTitle')} maxLength={80} onChange={(e) => setF({ ...f, slotTitle: e.target.value })} />
            <input className={`${input} col-span-2`} value={f.slotBody} placeholder={t('admin.slotBody')} maxLength={200} onChange={(e) => setF({ ...f, slotBody: e.target.value })} />
          </>
        )}
      </div>
      {error && <p role="alert" className="text-xs text-red-700">{error}</p>}
      <Button size="sm" onClick={save}>{t('admin.add')}</Button>
    </div>
  );
}

/** Amministrazione degli sponsor di marca (contratto diretto): anagrafica, coperture, risultati aggregati. */
export default function AdminSponsorsPage() {
  const { t } = useTranslation('promotions');
  const [list, setList] = useState<AdminSponsor[] | null>(null);
  const [form, setForm] = useState({ name: '', logoUrl: '', websiteUrl: '', contactEmail: '', description: '' });
  const [error, setError] = useState<string | null>(null);
  const load = useCallback(() => { sponsored.adminList().then(({ data }) => setList(data)).catch(() => setList([])); }, []);
  useEffect(() => { load(); }, [load]);

  const create = async (e: React.FormEvent) => {
    e.preventDefault(); setError(null);
    try {
      await sponsored.adminSave({ ...form, logoUrl: form.logoUrl || undefined, websiteUrl: form.websiteUrl || undefined, isActive: true });
      setForm({ name: '', logoUrl: '', websiteUrl: '', contactEmail: '', description: '' });
      load();
    } catch (err) { setError(apiErrorMessage(err, t('errors.generic'))); }
  };

  return (
    <div className="pb-24 space-y-4 max-w-3xl">
      <h1 className="text-xl font-bold text-text flex items-center gap-2"><Megaphone size={20} className="text-primary" /> {t('admin.title')}</h1>
      <form onSubmit={create} className="bg-white rounded-2xl border border-border/60 p-4 grid sm:grid-cols-2 gap-2">
        <strong className="sm:col-span-2 text-sm">{t('admin.newSponsor')}</strong>
        <input className={input} required placeholder={t('admin.name')} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
        <input className={input} type="email" placeholder={t('admin.email')} value={form.contactEmail} onChange={(e) => setForm({ ...form, contactEmail: e.target.value })} />
        <input className={input} type="url" placeholder={t('admin.logo')} value={form.logoUrl} onChange={(e) => setForm({ ...form, logoUrl: e.target.value })} />
        <input className={input} type="url" placeholder={t('admin.website')} value={form.websiteUrl} onChange={(e) => setForm({ ...form, websiteUrl: e.target.value })} />
        <textarea className={`${input} sm:col-span-2`} rows={2} placeholder={t('admin.description')} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
        {error && <p role="alert" className="sm:col-span-2 text-xs text-red-700">{error}</p>}
        <Button type="submit" size="sm">{t('admin.save')}</Button>
      </form>

      {list === null ? <Loader2 className="animate-spin text-primary mx-auto" /> : list.length === 0 ? (
        <p className="text-sm text-text-muted">{t('admin.empty')}</p>
      ) : list.map((s) => (
        <section key={s.id} className="bg-white rounded-2xl border border-border/60 p-4">
          <div className="flex items-center gap-3">
            {s.logoUrl && <img src={s.logoUrl} alt="" className="w-10 h-10 object-contain" />}
            <div className="min-w-0">
              <strong className="block text-sm">{s.name}</strong>
              <span className="text-xs text-text-muted">{s.contactEmail} · {s.websiteClicks} {t('admin.clicks')}</span>
            </div>
          </div>
          <ul className="mt-2 space-y-1">
            {s.sponsorships.map((x) => (
              <li key={x.id} className="flex items-center justify-between gap-2 text-xs p-2 rounded-lg border border-border/60">
                <span>
                  {t(`admin.placements.${x.placement}`)} · {t(`admin.scopes.${x.scope}`)}{x.scopeRef ? ` (${x.scopeRef})` : ''} ·{' '}
                  {new Date(x.startsAt).toLocaleDateString()}–{new Date(x.endsAt).toLocaleDateString()}
                  {x.placement === 'Slot' && <> · {t('admin.stats', { impressions: x.impressions, clicks: x.clicks })}</>}
                </span>
                <button type="button" onClick={() => sponsored.adminDeleteShip(x.id).then(load)} aria-label={t('admin.delete')} className="p-1 text-danger"><Trash2 size={13} /></button>
              </li>
            ))}
          </ul>
          <ShipForm sponsorId={s.id} onSaved={load} />
        </section>
      ))}
    </div>
  );
}
