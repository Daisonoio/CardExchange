import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, CalendarDays, Clock, Loader2, MapPin, Store as StoreIcon, Ticket, Users, Megaphone } from 'lucide-react';
import { events, referrals } from '../api';
import { shareLink } from '../utils/referral';
import type { CardEventDetail } from '../types';
import { EVENT_TYPE_LABELS } from '../types';
import { useAuth } from '../context/AuthContext';
import { useGame } from '../context/GameContext';
import { apiErrorMessage } from '../utils/apiError';
import Button from '../components/ui/Button';
import EmptyState from '../components/ui/EmptyState';
import EmptyStateLink from '../components/ui/EmptyStateLink';
import TradeNightPanel from '../components/events/TradeNightPanel';
import { useEntitlements } from '../hooks/useEntitlements';
import EventExhibitors from '../components/events/EventExhibitors';
import OrganizerPackagePanel from '../components/events/OrganizerPackagePanel';
import EventAttendeesPanel from '../components/events/EventAttendeesPanel';
import { formatDateTime, formatEuro, formatTime } from '../i18n/format';

const fmt = (iso: string) =>
  formatDateTime(iso, { weekday: 'long', day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' });

/** Dettaglio evento: informazioni, luogo, partecipanti, iscrizione. */
export default function EventPage() {
  const { t } = useTranslation('events');
  const { paidPlans } = useEntitlements();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { user } = useAuth();
  const { games } = useGame();
  const [event, setEvent] = useState<CardEventDetail | null | undefined>(undefined);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [shareInfo, setShareInfo] = useState<string | null>(null);

  const load = useCallback(() => {
    events.getById(Number(id)).then(({ data }) => setEvent(data)).catch(() => setEvent(null));
  }, [id]);
  useEffect(() => { load(); }, [load]);

  if (event === undefined) return <div className="flex justify-center py-16"><Loader2 className="animate-spin text-primary" /></div>;
  if (event === null) return <EmptyState icon={CalendarDays} title={t('detail.notFoundTitle')} description={t('detail.notFoundDescription')}
    action={<EmptyStateLink to="/local?tab=events" label={t('detail.notFoundAction')} />} />;

  const isOrganizer = user?.id === event.organizer.id;
  const isFull = event.spotsLeft != null ? event.spotsLeft <= 0 : !!event.maxParticipants && event.participantCount >= event.maxParticipants;
  const registrationOpen = event.registrationOpen ?? true;
  const myStatus = event.myStatus ?? (event.isParticipating ? 'Registered' : null);
  const ended = new Date(event.endDate ?? event.startDate).getTime() < Date.now();
  const gameNames = games.filter((g) => (event.supportedGameIds ?? '').split(',').map(Number).includes(g.id)).map((g) => g.name);
  const mapUrl = event.latitude != null && event.longitude != null
    ? `https://www.openstreetmap.org/?mlat=${event.latitude}&mlon=${event.longitude}#map=17/${event.latitude}/${event.longitude}`
    : `https://www.openstreetmap.org/search?query=${encodeURIComponent(`${event.address}, ${event.city}`)}`;

  const act = async (fn: () => Promise<unknown>) => {
    setBusy(true);
    setError(null);
    try {
      await fn();
      load();
    } catch (err) {
      setError(apiErrorMessage(err, t('detail.actionError')));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="pb-24 space-y-4">
      <div className="flex items-center gap-3">
        <button onClick={() => navigate(-1)} aria-label={t('common:actions.back')} className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center shrink-0">
          <ArrowLeft size={18} />
        </button>
        <div className="flex-1 min-w-0">
          <h1 className="text-xl font-bold text-text">{event.title}</h1>
          <p className="text-sm text-text-secondary">{EVENT_TYPE_LABELS[event.type] ?? event.type} · {t('detail.organizedBy', { username: event.organizer.username })}</p>
          {event.sponsors?.filter((s) => s.placement === 'EventPage').map((s) => (
            <Link key={s.id} to={`/sponsors/${s.id}`} className="mt-1 inline-flex items-center gap-1.5 text-xs text-text-secondary hover:underline" data-testid="event-sponsor">
              {s.logoUrl && <img src={s.logoUrl} alt="" className="w-4 h-4 rounded object-contain" />}
              {t('promotions:offeredBy', { sponsor: s.name })}
            </Link>
          ))}
        </div>
      </div>

      {event.status === 'Cancelled' && <div className="p-3 rounded-xl bg-red-50 text-red-700 text-sm font-medium">{t('detail.cancelled')}</div>}

      <ul className="bg-white rounded-2xl border border-border divide-y divide-border/60 text-sm">
        <li className="flex items-start gap-3 p-3">
          <CalendarDays size={16} className="mt-0.5 text-primary shrink-0" />
          <span className="first-letter:uppercase">{fmt(event.startDate)}{event.endDate ? ` – ${formatTime(event.endDate, { hour: '2-digit', minute: '2-digit' })}` : ''}</span>
        </li>
        <li className="flex items-start gap-3 p-3">
          {event.storeId ? <StoreIcon size={16} className="mt-0.5 text-primary shrink-0" /> : <MapPin size={16} className="mt-0.5 text-primary shrink-0" />}
          <div>
            {event.storeId && <Link to={`/stores/${event.storeId}`} className="font-semibold hover:underline block">{event.storeName}</Link>}
            {event.address}, {event.city}
            <a href={mapUrl} target="_blank" rel="noopener noreferrer" className="block text-xs font-semibold text-primary hover:underline">{t('detail.openMap')}</a>
          </div>
        </li>
        <li className="flex items-center gap-3 p-3">
          <Users size={16} className="text-primary shrink-0" />
          <span className="flex-1">
            {event.maxParticipants
              ? t('detail.participantsMax', { count: event.participantCount, max: event.maxParticipants })
              : t('detail.participants', { count: event.participantCount })}
            {event.spotsLeft != null && event.spotsLeft > 0 && <span className="text-text-secondary"> · {t('detail.spotsLeft', { count: event.spotsLeft })}</span>}
            {!!event.waitlistCount && <span className="text-text-secondary"> · {t('detail.waitlistCount', { count: event.waitlistCount })}</span>}
          </span>
        </li>
        {(event.entryFeeEur != null || event.format) && (
          <li className="flex items-start gap-3 p-3">
            <Ticket size={16} className="mt-0.5 text-primary shrink-0" />
            <span>
              {event.format && <span className="block">{t('detail.format', { format: event.format })}</span>}
              {event.entryFeeEur != null && <span className="block">{t('detail.fee', { fee: formatEuro(event.entryFeeEur) })}</span>}
            </span>
          </li>
        )}
        {event.registrationDeadline && (
          <li className="flex items-center gap-3 p-3">
            <Clock size={16} className="text-primary shrink-0" />
            <span className="first-letter:uppercase">{registrationOpen ? t('detail.deadline', { when: fmt(event.registrationDeadline) }) : t('detail.closed')}</span>
          </li>
        )}
      </ul>

      {event.sponsors?.filter((s) => s.placement === 'TradeNightBanner').map((s) => (
        <Link key={s.id} to={`/sponsors/${s.id}`} className="flex items-center gap-3 p-3 rounded-2xl bg-amber-50 border border-amber-200" data-testid="tradenight-sponsor">
          {s.logoUrl ? <img src={s.logoUrl} alt="" className="w-9 h-9 rounded-lg object-contain bg-white" /> : <span className="w-9 h-9 rounded-lg bg-white" />}
          <span className="text-sm text-text"><strong>{t('promotions:nightOfferedBy', { sponsor: s.name })}</strong></span>
        </Link>
      ))}
      {event.status === 'Published' && !ended && <TradeNightPanel eventId={event.id} />}
      {event.status === 'Published' && <EventExhibitors eventId={event.id} />}

      {event.description && <p className="text-sm text-text whitespace-pre-line">{event.description}</p>}
      {gameNames.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {gameNames.map((n) => <span key={n} className="px-2.5 py-1 rounded-full bg-primary/10 text-primary text-xs font-medium">{n}</span>)}
        </div>
      )}

      {error && <p className="text-sm text-danger">{error}</p>}

      {event.status === 'Published' && !ended && !isOrganizer && (
        myStatus === 'Offered' ? (
          <div className="p-3 rounded-2xl bg-amber-50 border border-amber-200 space-y-2" role="status">
            <p className="text-sm font-semibold text-amber-900">{t('detail.offered')}</p>
            {event.myOfferExpiresAt && <p className="text-xs text-amber-800 first-letter:uppercase">{t('detail.offeredUntil', { when: fmt(event.myOfferExpiresAt) })}</p>}
            <div className="flex gap-2">
              <Button className="flex-1" isLoading={busy} onClick={() => act(() => events.confirmSpot(event.id))}>{t('detail.confirmSpot')}</Button>
              <Button variant="outline" disabled={busy} onClick={() => act(() => events.leave(event.id))}>{t('detail.declineSpot')}</Button>
            </div>
          </div>
        ) : myStatus === 'Waitlisted' ? (
          <div className="p-3 rounded-2xl bg-white border border-border space-y-2" role="status">
            <p className="text-sm font-semibold text-text">{t('detail.waitlisted', { position: event.myWaitlistPosition ?? '–' })}</p>
            <p className="text-xs text-text-secondary">{t('detail.waitlistedHint')}</p>
            <Button variant="outline" className="w-full" isLoading={busy} onClick={() => act(() => events.leave(event.id))}>{t('detail.leaveWaitlist')}</Button>
          </div>
        ) : myStatus ? (
          <div className="space-y-2">
            <p className="text-sm font-semibold text-green-700 text-center">{myStatus === 'Attended' ? t('detail.attended') : t('detail.registered')}</p>
            {myStatus !== 'Attended' && (
              <Button variant="outline" className="w-full" isLoading={busy} onClick={() => act(() => events.leave(event.id))}>{t('detail.leave')}</Button>
            )}
          </div>
        ) : !registrationOpen ? (
          <Button className="w-full" disabled>{t('detail.closed')}</Button>
        ) : (
          <Button className="w-full" isLoading={busy} onClick={() => act(() => events.join(event.id))}>
            {isFull ? t('detail.joinWaitlist') : t('detail.join')}
          </Button>
        )
      )}
      {isOrganizer && event.status === 'Published' && <EventAttendeesPanel eventId={event.id} />}
      {isOrganizer && event.status === 'Published' && !ended && <OrganizerPackagePanel eventId={event.id} />}
      {isOrganizer && event.status === 'Published' && !ended && paidPlans && (
        <Link to={`/promotions?type=event&target=${event.id}`} className="flex items-center justify-center gap-2 w-full py-2.5 rounded-xl border border-amber-300 bg-amber-50 text-sm font-semibold text-amber-800">
          <Megaphone size={15} /> {t('promotions:featureEvent')}
        </Link>
      )}
      {isOrganizer && event.status === 'Published' && !ended && (
        <button
          type="button"
          disabled={busy}
          onClick={() => confirm(t('detail.cancelConfirm')) && act(() => events.cancel(event.id))}
          className="w-full text-sm text-danger hover:underline"
        >
          {t('detail.cancel')}
        </button>
      )}

      {event.status === 'Published' && !ended && (
        <button
          type="button"
          onClick={async () => {
            // Il link porta il codice invito: chi non è iscritto lo userà alla registrazione
            const code = await referrals.mine().then(({ data }) => data.code).catch(() => null);
            const url = `${window.location.origin}/events/${event.id}${code ? `?ref=${code}` : ''}`;
            const r = await shareLink(url, event.title, t('detail.shareText', { title: event.title }));
            setShareInfo(r === 'copied' ? t('detail.linkCopied') : null);
          }}
          className="w-full text-sm font-semibold text-primary hover:underline"
        >
          {t('detail.invite')}
        </button>
      )}
      {shareInfo && <p className="text-xs text-center text-text-secondary">{shareInfo}</p>}

      {event.participants.length > 0 && (
        <section className="bg-white rounded-2xl border border-border p-3">
          <h2 className="text-sm font-semibold mb-2">{t('detail.whoIsGoing')}</h2>
          <div className="flex flex-wrap gap-1.5">
            {event.participants.map((p) => (
              <Link key={p.userId} to={`/collection/${p.userId}`} className="px-2.5 py-1 rounded-full bg-surface-dark text-xs hover:underline">
                @{p.username}
              </Link>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
