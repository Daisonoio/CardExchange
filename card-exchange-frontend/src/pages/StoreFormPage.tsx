import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { AlertCircle, ArrowLeft, Loader2 } from 'lucide-react';
import { isAxiosError } from 'axios';
import { stores } from '../api';
import type { StoreInput } from '../types';
import { useGame } from '../context/GameContext';
import { apiErrorMessage } from '../utils/apiError';
import AddressPicker from '../components/map/AddressPicker';
import Button from '../components/ui/Button';
import { checkStoreSubmission, type StoreField, type SubmissionProblems } from '../utils/storeSubmission';

const EMPTY: StoreInput = {
  name: '', description: '', address: '', city: '', province: '', postalCode: '', country: 'IT',
  latitude: 0, longitude: 0, phone: '', website: '', instagram: '', openingHours: '', gameIds: [], isSafeMeetupSpot: true,
};

const ADDRESS_FIELDS: StoreField[] = ['address', 'city', 'province', 'postalCode', 'location'];

/**
 * Inserimento (in attesa di verifica) o modifica della scheda di un negozio. Una richiesta nuova, o una scheda
 * rifiutata che torna in verifica, deve avere tutti i campi tranne Instagram: si controlla qui e di nuovo sul server.
 */
export default function StoreFormPage() {
  const { t } = useTranslation('stores');
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { games } = useGame();
  const [form, setForm] = useState<StoreInput>(EMPTY);
  const [loading, setLoading] = useState(!!id);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [problems, setProblems] = useState<SubmissionProblems | null>(null);
  const needsFullCheck = !id || status === 'Rejected';

  useEffect(() => {
    if (!id) return;
    stores.get(Number(id))
      .then(({ data }) => {
        // Solo i campi modificabili (StoreInput), non stato e metadati
        const input = Object.fromEntries(Object.keys(EMPTY).map((k) => [k, data[k as keyof StoreInput] ?? EMPTY[k as keyof StoreInput]]));
        setForm(input as unknown as StoreInput);
        setStatus(data.status);
      })
      .catch(() => setError(t('form.notFound')))
      .finally(() => setLoading(false));
  }, [id, t]);

  const set = <K extends keyof StoreInput>(key: K, value: StoreInput[K]) => setForm((f) => ({ ...f, [key]: value }));

  // Dopo il primo tentativo i campi si ricontrollano a ogni modifica, così l'evidenziazione sparisce man mano
  const shown = problems && needsFullCheck ? checkStoreSubmission(form) : null;
  const flagged = (key: StoreField) => !!shown && (shown.missing.includes(key) || shown.invalid.includes(key));
  const hasProblems = !!shown && (shown.missing.length > 0 || shown.invalid.length > 0);

  const submit = async () => {
    setError(null);
    if (needsFullCheck) {
      const check = checkStoreSubmission(form);
      setProblems(check);
      if (check.missing.length > 0 || check.invalid.length > 0) return;
    }
    setBusy(true);
    try {
      const { data } = id ? await stores.update(Number(id), form) : await stores.create(form);
      navigate(`/stores/${data.id}`, { replace: true });
    } catch (err) {
      // Il server rifà gli stessi controlli: i campi che segnala si evidenziano come quelli trovati qui
      const data = isAxiosError(err) ? err.response?.data as Partial<SubmissionProblems> | undefined : undefined;
      if (data?.missing || data?.invalid) setProblems({ missing: data.missing ?? [], invalid: data.invalid ?? [] });
      setError(apiErrorMessage(err, t('form.saveError')));
    } finally {
      setBusy(false);
    }
  };

  if (loading) return <div className="flex justify-center py-16"><Loader2 className="animate-spin text-primary" /></div>;

  const marker = (optional = false) => needsFullCheck && (
    <span className={`ml-1 font-normal ${optional ? 'text-text-muted' : 'text-primary'}`}>{optional ? `(${t('form.optional')})` : '*'}</span>
  );
  const borderOf = (key?: StoreField) => (key && flagged(key) ? 'border-danger' : 'border-border');

  const input = (key: keyof StoreInput, label: string, placeholder?: string, field?: StoreField) => (
    <label className="block text-xs font-semibold text-text-secondary">
      {label}{marker(!field)}
      <input
        value={(form[key] as string) ?? ''}
        placeholder={placeholder}
        aria-invalid={field ? flagged(field) : undefined}
        onChange={(e) => set(key, e.target.value as never)}
        className={`mt-1 w-full px-3 py-2 rounded-xl border text-sm font-normal text-text ${borderOf(field)}`}
      />
      {field && shown?.invalid.includes(field) && (
        <span className="block mt-0.5 text-[11px] font-normal text-danger">{t(`form.invalidHints.${field}`)}</span>
      )}
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
          {t('form.reviewNote')} {t('form.allRequired')}
        </p>
      )}

      <section className="space-y-2 bg-surface-card rounded-2xl border border-border p-3">
        {input('name', t('form.fields.name'), t('form.fields.namePlaceholder'), 'name')}
        <label className="block text-xs font-semibold text-text-secondary">
          {t('form.fields.description')}{marker()}
          <textarea
            aria-invalid={flagged('description')}
            value={form.description ?? ''}
            onChange={(e) => set('description', e.target.value)}
            rows={3}
            maxLength={1000}
            placeholder={t('form.fields.descriptionPlaceholder')}
            className={`mt-1 w-full px-3 py-2 rounded-xl border text-sm font-normal text-text resize-none ${borderOf('description')}`}
          />
          {shown?.invalid.includes('description') && (
            <span className="block mt-0.5 text-[11px] font-normal text-danger">{t('form.invalidHints.description')}</span>
          )}
        </label>
      </section>

      <section className={`bg-surface-card rounded-2xl border p-3 ${ADDRESS_FIELDS.some(flagged) ? 'border-danger' : 'border-border'}`}>
        <h2 className="text-sm font-semibold mb-2">{t('form.location')}{marker()}</h2>
        <AddressPicker
          value={{ address: form.address, city: form.city, province: form.province, postalCode: form.postalCode, latitude: form.latitude, longitude: form.longitude }}
          onChange={(v) => setForm((f) => ({ ...f, ...v }))}
        />
      </section>

      <section className="space-y-2 bg-surface-card rounded-2xl border border-border p-3">
        <h2 className="text-sm font-semibold">{t('form.contacts')}</h2>
        {input('openingHours', t('form.fields.openingHours'), t('form.fields.openingHoursPlaceholder'), 'openingHours')}
        {input('phone', t('form.fields.phone'), undefined, 'phone')}
        {input('website', t('form.fields.website'), t('form.fields.websitePlaceholder'), 'website')}
        {input('instagram', 'Instagram', t('form.fields.instagramPlaceholder'))}
      </section>

      <section className={`space-y-2 bg-surface-card rounded-2xl border p-3 ${borderOf('games')}`}>
        <h2 className="text-sm font-semibold">{t('form.games')}{marker()}</h2>
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

      {hasProblems && shown && (
        <div role="alert" className="p-3 rounded-xl bg-red-50 border border-red-200 text-sm text-red-700 space-y-1">
          {shown.missing.length > 0 && (
            <p className="flex gap-2"><AlertCircle size={16} className="shrink-0 mt-0.5" />
              <span><strong>{t('form.missingTitle')}</strong> {shown.missing.map((k) => t(`form.labels.${k}`)).join(', ')}</span>
            </p>
          )}
          {shown.invalid.length > 0 && (
            <p className="flex gap-2"><AlertCircle size={16} className="shrink-0 mt-0.5" />
              <span><strong>{t('form.invalidTitle')}</strong> {shown.invalid.map((k) => `${t(`form.labels.${k}`)} (${t(`form.invalidHints.${k}`)})`).join(', ')}</span>
            </p>
          )}
        </div>
      )}
      {error && !hasProblems && <p className="text-sm text-danger">{error}</p>}
      <Button
        className="w-full"
        onClick={submit}
        isLoading={busy}
        disabled={needsFullCheck ? false : !form.name.trim() || !form.address.trim() || !form.city.trim()}
      >
        {id ? t('form.saveChanges') : t('form.submit')}
      </Button>
      {!id && form.latitude === 0 && form.longitude === 0 && (
        <p className="text-[11px] text-center text-text-muted">{t('form.locationHint')}</p>
      )}
    </div>
  );
}
