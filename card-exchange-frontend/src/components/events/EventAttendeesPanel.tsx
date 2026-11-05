import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CheckCircle2, Circle, Loader2 } from 'lucide-react';
import { events } from '../../api';
import type { EventAttendee } from '../../types';

/** Per l'organizzatore: iscritti con presenza segnabile a un tocco e lista d'attesa. */
export default function EventAttendeesPanel({ eventId }: { eventId: number }) {
  const { t } = useTranslation('events');
  const [list, setList] = useState<EventAttendee[] | null>(null);
  const [busy, setBusy] = useState<number | null>(null);

  const load = useCallback(() => {
    events.attendees(eventId).then(({ data }) => setList(data)).catch(() => setList([]));
  }, [eventId]);
  useEffect(() => { load(); }, [load]);

  if (list === null) return <div className="flex justify-center py-4"><Loader2 className="animate-spin text-primary" size={18} /></div>;

  const seated = list.filter((a) => a.status !== 'Waitlisted');
  const waiting = list.filter((a) => a.status === 'Waitlisted');
  const present = seated.filter((a) => a.checkedInAt).length;

  const toggle = async (a: EventAttendee) => {
    setBusy(a.userId);
    try {
      const { data } = await events.markPresent(eventId, a.userId, !a.checkedInAt);
      setList((l) => l?.map((x) => (x.userId === a.userId ? { ...x, checkedInAt: data.checkedInAt ?? null, status: data.checkedInAt ? 'Attended' : 'Registered' } : x)) ?? l);
    } finally {
      setBusy(null);
    }
  };

  return (
    <section className="bg-white rounded-2xl border border-border p-3 space-y-2">
      <div className="flex items-baseline justify-between gap-2">
        <h2 className="text-sm font-semibold">{t('detail.attendees')} · {seated.length}</h2>
        {seated.length > 0 && <span className="text-xs text-text-secondary tabular-nums">{t('detail.present')}: {present}/{seated.length}</span>}
      </div>
      {seated.length === 0 ? (
        <p className="text-sm text-text-muted">{t('detail.noAttendees')}</p>
      ) : (
        <>
          <p className="text-xs text-text-muted">{t('detail.attendeesHint')}</p>
          <ul className="divide-y divide-border/60">
            {seated.map((a) => (
              <li key={a.userId}>
                <button type="button" disabled={busy === a.userId} onClick={() => toggle(a)}
                  aria-pressed={!!a.checkedInAt} aria-label={`${a.username}: ${t('detail.markPresent')}`}
                  className="w-full flex items-center gap-3 py-2.5 text-left disabled:opacity-60">
                  {a.checkedInAt ? <CheckCircle2 size={20} className="text-green-600 shrink-0" /> : <Circle size={20} className="text-text-muted shrink-0" />}
                  <span className="flex-1 text-sm text-text">@{a.username}</span>
                  {a.status === 'Offered' && <span className="text-[11px] px-2 py-0.5 rounded-full bg-amber-50 text-amber-700">{t('detail.offeredBadge')}</span>}
                </button>
              </li>
            ))}
          </ul>
        </>
      )}
      {waiting.length > 0 && (
        <div className="pt-2 border-t border-border/60">
          <h3 className="text-xs font-semibold text-text-secondary">{t('detail.waitlistTitle')} · {waiting.length}</h3>
          <ol className="mt-1 space-y-1">
            {waiting.map((a) => (
              <li key={a.userId} className="text-sm text-text-secondary flex gap-2">
                <span className="tabular-nums w-7 text-text-muted">{t('detail.waitlistPos', { position: a.waitlistPosition })}</span>@{a.username}
              </li>
            ))}
          </ol>
        </div>
      )}
    </section>
  );
}
