import { useCallback, useEffect, useState } from 'react';
import { CalendarClock, CalendarPlus, MapPin } from 'lucide-react';
import { meetups } from '../../api';
import type { TradeMeetup } from '../../types';
import { apiErrorMessage } from '../../utils/apiError';
import Button from '../ui/Button';

const formatWhen = (iso: string) =>
  new Date(iso).toLocaleString('it-IT', { weekday: 'long', day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' });
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
    `SUMMARY:${esc(`Scambio carte con @${otherUsername}`)}`,
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
  const [list, setList] = useState<TradeMeetup[] | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState({ when: '', locationName: '', address: '', notes: '' });
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
      setError(apiErrorMessage(err, 'Operazione non riuscita.'));
    } finally {
      setBusy(false);
    }
  };

  const openForm = () => {
    const suggested = new Date(Date.now() + 24 * 60 * 60 * 1000);
    suggested.setMinutes(0, 0, 0);
    setForm({ when: toLocalInput(suggested), locationName: active?.locationName ?? '', address: active?.address ?? '', notes: '' });
    setShowForm(true);
  };

  const submit = () => run(async () => {
    await meetups.propose(tradeOfferId, {
      scheduledAt: new Date(form.when).toISOString(),
      locationName: form.locationName.trim(),
      address: form.address.trim() || undefined,
      notes: form.notes.trim() || undefined,
    });
    setShowForm(false);
  });

  if (list === null) return null;

  return (
    <section className="rounded-2xl border border-border bg-white p-3 space-y-2.5">
      <div className="flex items-center gap-2">
        <CalendarClock size={16} className="text-primary" />
        <h3 className="text-sm font-semibold flex-1">Appuntamento</h3>
        {active && (
          <span className={`text-[10px] px-2 py-0.5 rounded-full ${active.status === 'Accepted' ? 'bg-green-50 text-green-700' : 'bg-amber-50 text-amber-700'}`}>
            {active.status === 'Accepted' ? 'Confermato' : 'In attesa di risposta'}
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
                Proposto da {active.proposedById === userId ? 'te' : `@${active.proposedByUsername}`}
              </p>
            )}
          </div>
          <div className="flex flex-wrap gap-3 text-xs font-semibold text-primary">
            <a href={mapUrl(active)} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1 hover:underline">
              <MapPin size={13} /> Apri nella mappa
            </a>
            {active.status === 'Accepted' && (
              <button type="button" onClick={() => downloadIcs(active, otherUsername)} className="inline-flex items-center gap-1 hover:underline">
                <CalendarPlus size={13} /> Aggiungi al calendario
              </button>
            )}
          </div>

          {active.status === 'Proposed' && active.proposedById !== userId ? (
            <div className="grid grid-cols-2 gap-1.5">
              <Button size="sm" disabled={busy} onClick={() => run(() => meetups.accept(tradeOfferId, active.id))}>Accetta</Button>
              <Button size="sm" variant="outline" disabled={busy} onClick={() => run(() => meetups.decline(tradeOfferId, active.id))}>Non posso</Button>
            </div>
          ) : null}
          {!showForm && (
            <div className="flex gap-3 text-xs">
              <button type="button" onClick={openForm} className="font-semibold text-primary hover:underline">Proponi un altro orario</button>
              <button type="button" disabled={busy} onClick={() => run(() => meetups.cancel(tradeOfferId, active.id))} className="text-danger hover:underline">
                Annulla appuntamento
              </button>
            </div>
          )}
        </div>
      ) : (
        !showForm && (
          <div className="space-y-2">
            <p className="text-xs text-text-secondary">
              {lastDeclined
                ? `@${lastDeclined.proposedById === userId ? otherUsername : lastDeclined.proposedByUsername} non poteva ${formatWhen(lastDeclined.scheduledAt)}: proponi un altro orario.`
                : `Accordatevi su quando e dove incontrarvi. Meglio un luogo pubblico, come un negozio di carte.`}
            </p>
            <Button size="sm" className="w-full" onClick={openForm}>Proponi appuntamento</Button>
          </div>
        )
      )}

      {showForm && (
        <div className="space-y-2 pt-1">
          <label className="block text-xs font-semibold text-text-secondary">
            Data e ora
            <input
              type="datetime-local"
              value={form.when}
              min={toLocalInput(new Date(Date.now() + 30 * 60 * 1000))}
              onChange={(e) => setForm({ ...form, when: e.target.value })}
              className="mt-1 w-full px-3 py-2 rounded-xl border border-border text-sm"
            />
          </label>
          <label className="block text-xs font-semibold text-text-secondary">
            Luogo
            <input
              value={form.locationName}
              onChange={(e) => setForm({ ...form, locationName: e.target.value })}
              maxLength={200}
              placeholder="Es. Negozio Games Corner"
              className="mt-1 w-full px-3 py-2 rounded-xl border border-border text-sm"
            />
          </label>
          <input
            value={form.address}
            onChange={(e) => setForm({ ...form, address: e.target.value })}
            maxLength={300}
            placeholder="Indirizzo (facoltativo)"
            className="w-full px-3 py-2 rounded-xl border border-border text-sm"
          />
          <input
            value={form.notes}
            onChange={(e) => setForm({ ...form, notes: e.target.value })}
            maxLength={500}
            placeholder="Note (facoltative), es. 'al bancone'"
            className="w-full px-3 py-2 rounded-xl border border-border text-sm"
          />
          <div className="grid grid-cols-2 gap-1.5">
            <Button size="sm" variant="outline" onClick={() => setShowForm(false)} disabled={busy}>Annulla</Button>
            <Button size="sm" onClick={submit} isLoading={busy} disabled={!form.when || !form.locationName.trim()}>Invia proposta</Button>
          </div>
        </div>
      )}

      {error && <p className="text-xs text-danger">{error}</p>}
    </section>
  );
}
