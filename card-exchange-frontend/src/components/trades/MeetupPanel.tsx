import { useCallback, useEffect, useState } from 'react';
import { CalendarClock, CalendarPlus, MapPin, ShieldCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import i18n from '../../i18n';
import { formatDateTime } from '../../i18n/format';
import { meetups, stores } from '../../api';
import type { Store, TradeMeetup } from '../../types';
import { apiErrorMessage } from '../../utils/apiError';
import Button from '../ui/Button';

const formatWhen = (iso: string) =>
  formatDateTime(iso, { weekday: 'long', day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' });
const capitalizeFirst = (text: string) => text.charAt(0).toUpperCase() + text.slice(1);

const mapUrl = (m: TradeMeetup) =>
  m.latitude != null && m.longitude != null
    ? `https://www.openstreetmap.org/?mlat=${m.latitude}&mlon=${m.longitude}#map=17/${m.latitude}/${m.longitude}`
    : `https://www.openstreetmap.org/search?query=${encodeURIComponent([m.locationName, m.address].filter(Boolean).join(', '))}`;

/** Valore per <input type="datetime-local"> nel fuso del browser. */
const toLocalInput = (d: Date) => {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

function downloadIcs(m: TradeMeetup, otherUsername: string) {
  const stamp = (d: Date) => d.toISOString().replace(/[-:]/g, '').replace(/\.\d{3}/, '');
  const start = new Date(m.scheduledAt);
  const end = new Date(start.getTime() + 60 * 60 * 1000);
  const esc = (t: string) => t.replace(/[\\,;]/g, (c) => `\\${c}`).replace(/\n/g, '\\n');
  const ics = [
    'BEGIN:VCALENDAR', 'VERSION:2.0', 'PRODID:-//CardExchange//IT', 'BEGIN:VEVENT',
    `UID:meetup-${m.id}@cardexchange`, `DTSTAMP:${stamp(new Date())}`, `DTSTART:${stamp(start)}`, `DTEND:${stamp(end)}`,
    `SUMMARY:${esc(i18n.t('trades:meetup.icsSummary', { username: otherUsername }))}`,
    `LOCATION:${esc([m.locationName, m.address].filter(Boolean).join(', '))}`,
    ...(m.notes ? [`DESCRIPTION:${esc(m.notes)}`] : []),
    'END:VEVENT', 'END:VCALENDAR',
  ].join('\r\n');
  const url = URL.createObjectURL(new Blob([ics], { type: 'text/calendar' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = `scambio-${m.tradeOfferId}.ics`;
  a.click();
  URL.revokeObjectURL(url);
}

/** Appuntamento di uno scambio accettato: proposta, risposta, annullamento. */
export default function MeetupPanel({ tradeOfferId, userId, otherUsername }: { tradeOfferId: number; userId: number; otherUsername: string }) {
  const { t } = useTranslation('trades');
  const [list, setList] = useState<TradeMeetup[] | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState<{ when: string; locationName: string; address: string; notes: string; latitude?: number; longitude?: number }>(
    { when: '', locationName: '', address: '', notes: '' });
  // Negozi verificati vicini, proposti come luoghi d'incontro sicuri
  const [nearbyStores, setNearbyStores] = useState<Store[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const { data } = await meetups.list(tradeOfferId);
      setList(data);
    } catch {
      setList([]);
    }
  }, [tradeOfferId]);

  useEffect(() => { load(); }, [load]);

  const active = list?.find((m) => m.status === 'Proposed' || m.status === 'Accepted');
  const lastDeclined = !active ? list?.find((m) => m.status === 'Declined') : undefined;

  const run = async (fn: () => Promise<unknown>) => {
    setBusy(true);
    setError(null);
    try {
      await fn();
      await load();
    } catch (err) {
      setError(apiErrorMessage(err, t('meetup.actionError')));
    } finally {
      setBusy(false);
    }
  };

  const openForm = () => {
    const suggested = new Date(Date.now() + 24 * 60 * 60 * 1000);
    suggested.setMinutes(0, 0, 0);
    setForm({
      when: toLocalInput(suggested), locationName: active?.locationName ?? '', address: active?.address ?? '', notes: '',
      latitude: active?.latitude, longitude: active?.longitude,
    });
    setShowForm(true);
    stores.list({ meetupOnly: true, radiusKm: 30 }).then(({ data }) => setNearbyStores(data.slice(0, 5))).catch(() => setNearbyStores([]));
  };

  const pickStore = (s: Store) =>
    setForm((f) => ({ ...f, locationName: s.name, address: `${s.address}, ${s.city}`, latitude: s.latitude, longitude: s.longitude }));

  const submit = () => run(async () => {
    await meetups.propose(tradeOfferId, {
      scheduledAt: new Date(form.when).toISOString(),
      locationName: form.locationName.trim(),
      address: form.address.trim() || undefined,
      latitude: form.latitude,
      longitude: form.longitude,
      notes: form.notes.trim() || undefined,
    });
    setShowForm(false);
  });

  if (list === null) return null;

  return (
    <section className="rounded-2xl border border-border bg-white p-3 space-y-2.5">
      <div className="flex items-center gap-2">
        <CalendarClock size={16} className="text-primary" />
        <h3 className="text-sm font-semibold flex-1">{t('meetup.title')}</h3>
        {active && (
          <span className={`text-[10px] px-2 py-0.5 rounded-full ${active.status === 'Accepted' ? 'bg-green-50 text-green-700' : 'bg-amber-50 text-amber-700'}`}>
            {active.status === 'Accepted' ? t('meetup.confirmed') : t('meetup.awaitingReply')}
          </span>
        )}
      </div>

      {active ? (
        <div className="space-y-2">
          <div className="text-sm">
            <p className="font-semibold">{capitalizeFirst(formatWhen(active.scheduledAt))}</p>
            <p className="text-text-secondary">{active.locationName}{active.address ? ` · ${active.address}` : ''}</p>
            {active.notes && <p className="text-xs text-text-muted mt-0.5">{active.notes}</p>}
            {active.status === 'Proposed' && (
              <p className="text-[11px] text-text-muted mt-0.5">
                {active.proposedById === userId ? t('meetup.proposedByYou') : t('meetup.proposedBy', { username: active.proposedByUsername })}
              </p>
            )}
          </div>
          <div className="flex flex-wrap gap-3 text-xs font-semibold text-primary">
            <a href={mapUrl(active)} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1 hover:underline">
              <MapPin size={13} /> {t('meetup.openMap')}
            </a>
            {active.status === 'Accepted' && (
              <button type="button" onClick={() => downloadIcs(active, otherUsername)} className="inline-flex items-center gap-1 hover:underline">
                <CalendarPlus size={13} /> {t('meetup.addToCalendar')}
              </button>
            )}
          </div>

          {active.status === 'Proposed' && active.proposedById !== userId ? (
            <div className="grid grid-cols-2 gap-1.5">
              <Button size="sm" disabled={busy} onClick={() => run(() => meetups.accept(tradeOfferId, active.id))}>{t('meetup.accept')}</Button>
              <Button size="sm" variant="outline" disabled={busy} onClick={() => run(() => meetups.decline(tradeOfferId, active.id))}>{t('meetup.decline')}</Button>
            </div>
          ) : null}
          {!showForm && (
            <div className="flex gap-3 text-xs">
              <button type="button" onClick={openForm} className="font-semibold text-primary hover:underline">{t('meetup.proposeAnother')}</button>
              <button type="button" disabled={busy} onClick={() => run(() => meetups.cancel(tradeOfferId, active.id))} className="text-danger hover:underline">
                {t('meetup.cancelMeetup')}
              </button>
            </div>
          )}
        </div>
      ) : (
        !showForm && (
          <div className="space-y-2">
            <p className="text-xs text-text-secondary">
              {lastDeclined
                ? t('meetup.declinedInfo', { username: lastDeclined.proposedById === userId ? otherUsername : lastDeclined.proposedByUsername, when: formatWhen(lastDeclined.scheduledAt) })
                : t('meetup.intro')}
            </p>
            <Button size="sm" className="w-full" onClick={openForm}>{t('meetup.propose')}</Button>
          </div>
        )
      )}

      {showForm && (
        <div className="space-y-2 pt-1">
          <label className="block text-xs font-semibold text-text-secondary">
            {t('meetup.form.when')}
            <input
              type="datetime-local"
              value={form.when}
              min={toLocalInput(new Date(Date.now() + 30 * 60 * 1000))}
              onChange={(e) => setForm({ ...form, when: e.target.value })}
              className="mt-1 w-full px-3 py-2 rounded-xl border border-border text-sm"
            />
          </label>
          {nearbyStores.length > 0 && (
            <div>
              <p className="text-xs font-semibold text-text-secondary mb-1 flex items-center gap-1">
                <ShieldCheck size={13} className="text-green-600" /> {t('meetup.form.suggestedStores')}
              </p>
              <div className="flex flex-wrap gap-1.5">
                {nearbyStores.map((s) => (
                  <button
                    key={s.id}
                    type="button"
                    onClick={() => pickStore(s)}
                    className={`px-2.5 py-1 rounded-full text-xs border ${form.locationName === s.name ? 'border-primary bg-primary/10 text-primary' : 'border-border text-text-secondary'}`}
                  >
                    {s.name}{s.distanceKm != null ? ` · ${s.distanceKm} km` : ''}
                  </button>
                ))}
              </div>
            </div>
          )}
          <label className="block text-xs font-semibold text-text-secondary">
            {t('meetup.form.place')}
            <input
              value={form.locationName}
              onChange={(e) => setForm({ ...form, locationName: e.target.value, latitude: undefined, longitude: undefined })}
              maxLength={200}
              placeholder={t('meetup.form.placePlaceholder')}
              className="mt-1 w-full px-3 py-2 rounded-xl border border-border text-sm"
            />
          </label>
          <input
            value={form.address}
            onChange={(e) => setForm({ ...form, address: e.target.value })}
            maxLength={300}
            placeholder={t('meetup.form.addressPlaceholder')}
            className="w-full px-3 py-2 rounded-xl border border-border text-sm"
          />
          <input
            value={form.notes}
            onChange={(e) => setForm({ ...form, notes: e.target.value })}
            maxLength={500}
            placeholder={t('meetup.form.notesPlaceholder')}
            className="w-full px-3 py-2 rounded-xl border border-border text-sm"
          />
          <div className="grid grid-cols-2 gap-1.5">
            <Button size="sm" variant="outline" onClick={() => setShowForm(false)} disabled={busy}>{t('common:actions.cancel')}</Button>
            <Button size="sm" onClick={submit} isLoading={busy} disabled={!form.when || !form.locationName.trim()}>{t('meetup.form.submit')}</Button>
          </div>
        </div>
      )}

      {error && <p className="text-xs text-danger">{error}</p>}
    </section>
  );
}
