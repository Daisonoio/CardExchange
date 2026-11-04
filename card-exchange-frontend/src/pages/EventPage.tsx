import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, CalendarDays, Loader2, MapPin, Store as StoreIcon, Users } from 'lucide-react';
import { events, referrals } from '../api';
import { shareLink } from '../utils/referral';
import type { CardEventDetail } from '../types';
import { EVENT_TYPE_LABELS } from '../types';
import { useAuth } from '../context/AuthContext';
import { useGame } from '../context/GameContext';
import { apiErrorMessage } from '../utils/apiError';
import Button from '../components/ui/Button';
import EmptyState from '../components/ui/EmptyState';
import TradeNightPanel from '../components/events/TradeNightPanel';

const fmt = (iso: string) =>
  new Date(iso).toLocaleString('it-IT', { weekday: 'long', day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' });

/** Dettaglio evento: informazioni, luogo, partecipanti, iscrizione. */
export default function EventPage() {
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
  if (event === null) return <EmptyState icon={CalendarDays} title="Evento non trovato" description="Potrebbe essere stato annullato." />;

  const isOrganizer = user?.id === event.organizer.id;
  const isFull = !!event.maxParticipants && event.participantCount >= event.maxParticipants;
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
      setError(apiErrorMessage(err, 'Operazione non riuscita.'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="pb-24 space-y-4">
      <div className="flex items-center gap-3">
        <button onClick={() => navigate(-1)} aria-label="Indietro" className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center shrink-0">
          <ArrowLeft size={18} />
        </button>
        <div className="flex-1 min-w-0">
          <h1 className="text-xl font-bold text-text">{event.title}</h1>
          <p className="text-sm text-text-secondary">{EVENT_TYPE_LABELS[event.type] ?? event.type} · organizzato da @{event.organizer.username}</p>
        </div>
      </div>

      {event.status === 'Cancelled' && <div className="p-3 rounded-xl bg-red-50 text-red-700 text-sm font-medium">Evento annullato</div>}

      <ul className="bg-white rounded-2xl border border-border divide-y divide-border/60 text-sm">
        <li className="flex items-start gap-3 p-3">
          <CalendarDays size={16} className="mt-0.5 text-primary shrink-0" />
          <span className="first-letter:uppercase">{fmt(event.startDate)}{event.endDate ? ` – ${new Date(event.endDate).toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' })}` : ''}</span>
        </li>
        <li className="flex items-start gap-3 p-3">
          {event.storeId ? <StoreIcon size={16} className="mt-0.5 text-primary shrink-0" /> : <MapPin size={16} className="mt-0.5 text-primary shrink-0" />}
          <div>
            {event.storeId && <Link to={`/stores/${event.storeId}`} className="font-semibold hover:underline block">{event.storeName}</Link>}
            {event.address}, {event.city}
            <a href={mapUrl} target="_blank" rel="noopener noreferrer" className="block text-xs font-semibold text-primary hover:underline">Apri nella mappa</a>
          </div>
        </li>
        <li className="flex items-center gap-3 p-3">
          <Users size={16} className="text-primary shrink-0" />
          {event.participantCount}{event.maxParticipants ? ` su ${event.maxParticipants}` : ''} partecipanti
        </li>
      </ul>

      {event.status === 'Published' && !ended && <TradeNightPanel eventId={event.id} />}

      {event.description && <p className="text-sm text-text whitespace-pre-line">{event.description}</p>}
      {gameNames.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {gameNames.map((n) => <span key={n} className="px-2.5 py-1 rounded-full bg-primary/10 text-primary text-xs font-medium">{n}</span>)}
        </div>
      )}

      {error && <p className="text-sm text-danger">{error}</p>}

      {event.status === 'Published' && !ended && !isOrganizer && (
        event.isParticipating ? (
          <Button variant="outline" className="w-full" isLoading={busy} onClick={() => act(() => events.leave(event.id))}>Annulla la partecipazione</Button>
        ) : (
          <Button className="w-full" isLoading={busy} disabled={isFull} onClick={() => act(() => events.join(event.id))}>
            {isFull ? 'Posti esauriti' : 'Partecipa'}
          </Button>
        )
      )}
      {isOrganizer && event.status === 'Published' && !ended && (
        <button
          type="button"
          disabled={busy}
          onClick={() => confirm('Annullare l\'evento? I partecipanti riceveranno una notifica.') && act(() => events.cancel(event.id))}
          className="w-full text-sm text-danger hover:underline"
        >
          Annulla evento
        </button>
      )}

      {event.status === 'Published' && !ended && (
        <button
          type="button"
          onClick={async () => {
            // Il link porta il codice invito: chi non è iscritto lo userà alla registrazione
            const code = await referrals.mine().then(({ data }) => data.code).catch(() => null);
            const url = `${window.location.origin}/events/${event.id}${code ? `?ref=${code}` : ''}`;
            const r = await shareLink(url, event.title, `Vieni a "${event.title}" con me su CardExchange!`);
            setShareInfo(r === 'copied' ? 'Link dell\'evento copiato negli appunti' : null);
          }}
          className="w-full text-sm font-semibold text-primary hover:underline"
        >
          Invita amici a questo evento
        </button>
      )}
      {shareInfo && <p className="text-xs text-center text-text-secondary">{shareInfo}</p>}

      {event.participants.length > 0 && (
        <section className="bg-white rounded-2xl border border-border p-3">
          <h2 className="text-sm font-semibold mb-2">Chi partecipa</h2>
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
