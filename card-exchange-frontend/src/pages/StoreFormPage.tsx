import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, Loader2 } from 'lucide-react';
import { stores } from '../api';
import type { StoreInput } from '../types';
import { useGame } from '../context/GameContext';
import { apiErrorMessage } from '../utils/apiError';
import AddressPicker from '../components/map/AddressPicker';
import Button from '../components/ui/Button';

const EMPTY: StoreInput = {
  name: '', description: '', address: '', city: '', province: '', postalCode: '', country: 'IT',
  latitude: 0, longitude: 0, phone: '', website: '', instagram: '', openingHours: '', gameIds: [], isSafeMeetupSpot: true,
};

/** Inserimento (in attesa di verifica) o modifica della scheda di un negozio. */
export default function StoreFormPage() {
  const { t } = useTranslation('stores');
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { games } = useGame();
  const [form, setForm] = useState<StoreInput>(EMPTY);
  const [loading, setLoading] = useState(!!id);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    stores.get(Number(id))
      .then(({ data }) => {
        // Solo i campi modificabili (StoreInput), non stato e metadati
        const input = Object.fromEntries(Object.keys(EMPTY).map((k) => [k, data[k as keyof StoreInput] ?? EMPTY[k as keyof StoreInput]]));
        setForm(input as unknown as StoreInput);
      })
      .catch(() => setError(t('form.notFound')))
      .finally(() => setLoading(false));
  }, [id, t]);

  const set = <K extends keyof StoreInput>(key: K, value: StoreInput[K]) => setForm((f) => ({ ...f, [key]: value }));

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      const { data } = id ? await stores.update(Number(id), form) : await stores.create(form);
      navigate(`/stores/${data.id}`, { replace: true });
    } catch (err) {
      setError(apiErrorMessage(err, t('form.saveError')));
    } finally {
      setBusy(false);
    }
  };

  if (loading) return <div className="flex justify-center py-16"><Loader2 className="animate-spin text-primary" /></div>;

  const input = (key: keyof StoreInput, label: string, placeholder?: string) => (
    <label className="block text-xs font-semibold text-text-secondary">
      {label}
      <input
        value={(form[key] as string) ?? ''}
        placeholder={placeholder}
        onChange={(e) => set(key, e.target.value as never)}
        className="mt-1 w-full px-3 py-2 rounded-xl border border-border text-sm font-normal text-text"
      />
    </label>
  );

  return (
    <div className="pb-24 space-y-4">
      <div className="flex items-center gap-3">
        <button onClick={() => navigate(-1)} aria-label={t('common:actions.back')} className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center">
          <ArrowLeft size={18} />
        </button>
        <h1 className="text-xl font-bold text-text">{id ? t('form.editTitle') : t('form.createTitle')}</h1>
      </div>
      {!id && (
        <p className="text-sm text-text-secondary">
          {t('form.reviewNote')}
        </p>
      )}

      <section className="space-y-2 bg-white rounded-2xl border border-border p-3">
        {input('name', t('form.fields.name'), t('form.fields.namePlaceholder'))}
        <label className="block text-xs font-semibold text-text-secondary">
          {t('form.fields.description')}
          <textarea
            value={form.description ?? ''}
            onChange={(e) => set('description', e.target.value)}
            rows={3}
            maxLength={1000}
            placeholder={t('form.fields.descriptionPlaceholder')}
            className="mt-1 w-full px-3 py-2 rounded-xl border border-border text-sm font-normal text-text resize-none"
          />
        </label>
      </section>

      <section className="bg-white rounded-2xl border border-border p-3">
        <h2 className="text-sm font-semibold mb-2">{t('form.location')}</h2>
        <AddressPicker
          value={{ address: form.address, city: form.city, province: form.province, postalCode: form.postalCode, latitude: form.latitude, longitude: form.longitude }}
          onChange={(v) => setForm((f) => ({ ...f, ...v }))}
        />
      </section>

      <section className="space-y-2 bg-white rounded-2xl border border-border p-3">
        <h2 className="text-sm font-semibold">{t('form.contacts')}</h2>
        {input('openingHours', t('form.fields.openingHours'), t('form.fields.openingHoursPlaceholder'))}
        {input('phone', t('form.fields.phone'))}
        {input('website', t('form.fields.website'), t('form.fields.websitePlaceholder'))}
        {input('instagram', 'Instagram', t('form.fields.instagramPlaceholder'))}
      </section>

      <section className="space-y-2 bg-white rounded-2xl border border-border p-3">
        <h2 className="text-sm font-semibold">{t('form.games')}</h2>
        <div className="flex flex-wrap gap-1.5">
          {games.map((g) => {
            const on = form.gameIds.includes(g.id);
            return (
              <button
                key={g.id}
                type="button"
                onClick={() => set('gameIds', on ? form.gameIds.filter((x) => x !== g.id) : [...form.gameIds, g.id])}
                className={`px-3 py-1.5 rounded-full text-xs font-medium border ${on ? 'border-primary bg-primary/10 text-primary' : 'border-border text-text-secondary'}`}
              >
                {g.name}
              </button>
            );
          })}
        </div>
        <label className="flex items-start gap-3 p-3 rounded-xl bg-surface-dark text-sm">
          <input
            type="checkbox"
            className="mt-0.5 w-5 h-5 accent-primary"
            checked={form.isSafeMeetupSpot}
            onChange={(e) => set('isSafeMeetupSpot', e.target.checked)}
          />
          <span>
            <span className="font-medium">{t('form.safeMeetupSpot')}</span>
            <span className="block text-xs text-text-muted">{t('form.safeMeetupSpotHint')}</span>
          </span>
        </label>
      </section>

      {error && <p className="text-sm text-danger">{error}</p>}
      <Button
        className="w-full"
        onClick={submit}
        isLoading={busy}
        disabled={!form.name.trim() || !form.address.trim() || !form.city.trim() || (form.latitude === 0 && form.longitude === 0)}
      >
        {id ? t('form.saveChanges') : t('form.submit')}
      </Button>
      {!id && form.latitude === 0 && form.longitude === 0 && (
        <p className="text-[11px] text-center text-text-muted">{t('form.locationHint')}</p>
      )}
    </div>
  );
}
