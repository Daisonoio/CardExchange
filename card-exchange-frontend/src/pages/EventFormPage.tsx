import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft } from 'lucide-react';
import { events, stores } from '../api';
import type { Store } from '../types';
import { EVENT_TYPE_LABELS, EVENT_TYPE_VALUES } from '../types';
import { useGame } from '../context/GameContext';
import { apiErrorMessage } from '../utils/apiError';
import AddressPicker from '../components/map/AddressPicker';
import type { AddressValue } from '../components/map/AddressPicker';
import Button from '../components/ui/Button';

const toLocalInput = (d: Date) => {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

/** Creazione di un evento: in un proprio negozio verificato o in un luogo indicato sulla mappa. */
export default function EventFormPage() {
  const { t } = useTranslation('events');
  const navigate = useNavigate();
  const { games } = useGame();
  const [myStores, setMyStores] = useState<Store[]>([]);
  const nextWeek = new Date(Date.now() + 7 * 24 * 3600 * 1000);
  nextWeek.setHours(20, 30, 0, 0);
  const [form, setForm] = useState({
    title: '', description: '', type: 'Trademeetup', start: toLocalInput(nextWeek), durationHours: 3,
    maxParticipants: '', storeId: 0, gameIds: [] as number[], entryFee: '', format: '', deadline: '',
  });
  const [place, setPlace] = useState<AddressValue>({ address: '', city: '', province: '', postalCode: '', latitude: 0, longitude: 0 });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    stores.mine()
      .then(({ data }) => {
        const verified = data.filter((s) => s.status === 'Verified');
        setMyStores(verified);
        if (verified.length) setForm((f) => ({ ...f, storeId: verified[0].id }));
      })
      .catch(() => setMyStores([]));
  }, []);

  const store = myStores.find((s) => s.id === form.storeId);
  const hasPlace = !!store || (place.latitude !== 0 && !!place.address && !!place.city);

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      const start = new Date(form.start);
      const end = new Date(start.getTime() + form.durationHours * 3600 * 1000);
      const where = store
        ? { address: store.address, city: store.city, province: store.province, postalCode: store.postalCode, country: store.country, latitude: store.latitude, longitude: store.longitude, storeId: store.id }
        : { address: place.address, city: place.city, province: place.province, postalCode: place.postalCode, country: 'IT', latitude: place.latitude, longitude: place.longitude };
      const { data } = await events.create({
        title: form.title.trim(),
        description: form.description.trim() || undefined,
        type: EVENT_TYPE_VALUES[form.type],
        startDate: start.toISOString(),
        endDate: end.toISOString(),
        maxParticipants: form.maxParticipants ? Number(form.maxParticipants) : undefined,
        isPublic: true,
        supportedGameIds: form.gameIds.length ? form.gameIds.join(',') : undefined,
        entryFeeEur: form.entryFee ? Number(form.entryFee) : undefined,
        format: form.format.trim() || undefined,
        registrationDeadline: form.deadline ? new Date(form.deadline).toISOString() : undefined,
        gameId: form.gameIds.length === 1 ? form.gameIds[0] : undefined,
        ...where,
      });
      navigate(`/events/${data.id}`, { replace: true });
    } catch (err) {
      setError(apiErrorMessage(err, t('form.createError')));
    } finally {
      setBusy(false);
    }
  };

  const label = 'block text-xs font-semibold text-text-secondary';
  const inputCls = 'mt-1 w-full px-3 py-2 rounded-xl border border-border text-sm font-normal text-text';

  return (
    <div className="pb-24 space-y-4">
      <div className="flex items-center gap-3">
        <button onClick={() => navigate(-1)} aria-label={t('common:actions.back')} className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center">
          <ArrowLeft size={18} />
        </button>
        <h1 className="text-xl font-bold text-text">{t('form.title')}</h1>
      </div>

      <section className="space-y-2 bg-white rounded-2xl border border-border p-3">
        <label className={label}>{t('form.fields.title')}
          <input value={form.title} maxLength={200} onChange={(e) => setForm({ ...form, title: e.target.value })} placeholder={t('form.fields.titlePlaceholder')} className={inputCls} />
        </label>
        <label className={label}>{t('form.fields.type')}
          <select value={form.type} onChange={(e) => setForm({ ...form, type: e.target.value })} className={inputCls}>
            {Object.entries(EVENT_TYPE_LABELS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select>
        </label>
        <label className={label}>{t('form.fields.description')}
          <textarea value={form.description} maxLength={2000} rows={3} onChange={(e) => setForm({ ...form, description: e.target.value })} placeholder={t('form.fields.descriptionPlaceholder')} className={`${inputCls} resize-none`} />
        </label>
        <div className="grid grid-cols-3 gap-2">
          <label className={`${label} col-span-2`}>{t('form.fields.start')}
            <input type="datetime-local" value={form.start} min={toLocalInput(new Date())} onChange={(e) => setForm({ ...form, start: e.target.value })} className={inputCls} />
          </label>
          <label className={label}>{t('form.fields.duration')}
            <input type="number" min={1} max={72} value={form.durationHours} onChange={(e) => setForm({ ...form, durationHours: Math.max(1, Number(e.target.value)) })} className={inputCls} />
          </label>
        </div>
        <label className={label}>{t('form.fields.maxParticipants')}
          <input type="number" min={2} value={form.maxParticipants} onChange={(e) => setForm({ ...form, maxParticipants: e.target.value })} className={inputCls} />
        </label>
        {form.maxParticipants && (
          <label className={label}>{t('form.fields.deadline')}
            <input type="datetime-local" value={form.deadline} max={form.start} onChange={(e) => setForm({ ...form, deadline: e.target.value })} className={inputCls} />
          </label>
        )}
        {form.type !== 'Trademeetup' && (
          <div className="grid grid-cols-2 gap-2">
            <label className={label}>{t('form.fields.entryFee')}
              <input type="number" min={0} max={1000} step="0.5" inputMode="decimal" value={form.entryFee} onChange={(e) => setForm({ ...form, entryFee: e.target.value })} className={inputCls} />
            </label>
            <label className={label}>{t('form.fields.format')}
              <input value={form.format} maxLength={60} onChange={(e) => setForm({ ...form, format: e.target.value })} placeholder={t('form.fields.formatPlaceholder')} className={inputCls} />
            </label>
            <p className="col-span-2 text-[11px] font-normal text-text-muted">{t('form.fields.entryFeeHint')}</p>
          </div>
        )}
      </section>

      <section className="space-y-2 bg-white rounded-2xl border border-border p-3">
        <h2 className="text-sm font-semibold">{t('form.where')}</h2>
        {myStores.length > 0 && (
          <label className={label}>{t('form.place')}
            <select value={form.storeId} onChange={(e) => setForm({ ...form, storeId: Number(e.target.value) })} className={inputCls}>
              {myStores.map((s) => <option key={s.id} value={s.id}>{s.name} — {s.city}</option>)}
              <option value={0}>{t('form.otherPlace')}</option>
            </select>
          </label>
        )}
        {store ? (
          <p className="text-xs text-text-secondary">{store.address}, {store.city}</p>
        ) : (
          <AddressPicker value={place} onChange={setPlace} />
        )}
      </section>

      <section className="space-y-2 bg-white rounded-2xl border border-border p-3">
        <h2 className="text-sm font-semibold">{t('form.games')}</h2>
        <div className="flex flex-wrap gap-1.5">
          {games.map((g) => {
            const on = form.gameIds.includes(g.id);
            return (
              <button key={g.id} type="button"
                onClick={() => setForm({ ...form, gameIds: on ? form.gameIds.filter((x) => x !== g.id) : [...form.gameIds, g.id] })}
                className={`px-3 py-1.5 rounded-full text-xs font-medium border ${on ? 'border-primary bg-primary/10 text-primary' : 'border-border text-text-secondary'}`}>
                {g.name}
              </button>
            );
          })}
        </div>
      </section>

      {error && <p className="text-sm text-danger">{error}</p>}
      <Button className="w-full" onClick={submit} isLoading={busy} disabled={!form.title.trim() || !form.start || !hasPlace}>
        {t('form.publish')}
      </Button>
    </div>
  );
}
