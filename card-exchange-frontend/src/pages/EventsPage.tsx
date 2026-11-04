import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { CalendarDays, ChevronRight, Loader2, MapPin, Plus, Store as StoreIcon, Users } from 'lucide-react';
import { events } from '../api';
import type { CardEvent } from '../types';
import { EVENT_TYPE_LABELS } from '../types';
import { apiErrorMessage } from '../utils/apiError';
import { formatEventDate } from '../utils/eventDate';
import EmptyState from '../components/ui/EmptyState';
import { useFormat } from '../i18n/format';

type Mode = 'nearby' | 'all';

/** Eventi della zona (posizione del profilo) o tutti i prossimi eventi. Usata nella scheda "Eventi" di "Vicino a te". */
export default function EventsPage() {
  const { t } = useTranslation('events');
  const { date } = useFormat();
  const [mode, setMode] = useState<Mode>('nearby');
  const [result, setResult] = useState<{ mode: Mode; items: CardEvent[]; error?: string } | null>(null);

  useEffect(() => {
    let cancelled = false;
    const request = mode === 'nearby'
      ? events.getNearby(undefined, undefined, 50).then(({ data }) => data)
      : events.getUpcoming().then(({ data }) => data.items ?? (data as unknown as CardEvent[]));
    request
      .then((items) => { if (!cancelled) setResult({ mode, items }); })
      .catch((err) => { if (!cancelled) setResult({ mode, items: [], error: apiErrorMessage(err, t('list.loadError')) }); });
    return () => { cancelled = true; };
  }, [mode, t]);

  const loading = result?.mode !== mode;

  return (
    <div className="space-y-3">
      <div className="flex gap-2">
        {([['nearby', 'list.modes.nearby'], ['all', 'list.modes.all']] as [Mode, string][]).map(([key, labelKey]) => (
          <button
            key={key}
            onClick={() => setMode(key)}
            className={`flex-1 py-2 rounded-xl text-sm font-semibold border ${mode === key ? 'bg-primary text-white border-primary' : 'bg-white text-text-secondary border-border'}`}
          >
            {t(labelKey)}
          </button>
        ))}
      </div>

      <Link to="/events/new" className="flex items-center justify-center gap-2 p-3 rounded-2xl border-2 border-dashed border-border text-sm font-semibold text-primary hover:bg-surface-dark">
        <Plus size={16} /> {t('list.organize')}
      </Link>

      {loading ? (
        <div className="flex justify-center py-10"><Loader2 className="animate-spin text-primary" /></div>
      ) : result?.error ? (
        <EmptyState icon={MapPin} title={t('list.unavailable')} description={result.error} />
      ) : !result || result.items.length === 0 ? (
        <EmptyState icon={CalendarDays} title={t('list.emptyTitle')} description={t('list.emptyDescription')} />
      ) : (
        <ul className="space-y-2">
          {result.items.map((e) => (
            <li key={e.id}>
              <Link to={`/events/${e.id}`} className="block p-3 rounded-2xl bg-white border border-border hover:shadow-sm">
                <div className="flex items-start gap-3">
                  <div className="w-12 shrink-0 rounded-xl bg-primary/10 text-primary text-center py-1">
                    <div className="text-[10px] uppercase font-semibold">{date(e.startDate, { month: 'short' })}</div>
                    <div className="text-lg font-bold leading-none">{new Date(e.startDate).getDate()}</div>
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-semibold truncate">{e.title}</p>
                    <p className="text-xs text-text-muted">{EVENT_TYPE_LABELS[e.type] ?? e.type} · {formatEventDate(e.startDate)}</p>
                    <p className="text-xs text-text-secondary truncate flex items-center gap-1 mt-0.5">
                      {e.storeName ? <><StoreIcon size={11} /> {e.storeName}</> : <><MapPin size={11} /> {e.city}</>}
                    </p>
                    <p className="text-[11px] text-text-muted flex items-center gap-1 mt-0.5">
                      <Users size={11} /> {e.maxParticipants
                        ? t('list.participantsMax', { count: e.participantCount, max: e.maxParticipants })
                        : t('list.participants', { count: e.participantCount })}
                      {e.isParticipating && <span className="ml-1 text-green-700 font-semibold">{t('list.joined')}</span>}
                    </p>
                  </div>
                  <ChevronRight size={16} className="text-text-muted mt-3 shrink-0" />
                </div>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
