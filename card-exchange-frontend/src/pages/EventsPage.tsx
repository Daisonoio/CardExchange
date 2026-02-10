import { useState, useEffect } from 'react';
import { CalendarDays, MapPin, Users, Loader2, Navigation } from 'lucide-react';
import { events } from '../api';
import type { CardEvent } from '../types';
import { useGeolocation } from '../hooks/useGeolocation';
import Button from '../components/ui/Button';
import EmptyState from '../components/ui/EmptyState';

export default function EventsPage() {
  const [eventList, setEventList] = useState<CardEvent[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [mode, setMode] = useState<'upcoming' | 'nearby'>('upcoming');
  const { position, requestPosition, isLoading: geoLoading } = useGeolocation();

  useEffect(() => {
    const load = async () => {
      setIsLoading(true);
      try {
        if (mode === 'nearby' && position) {
          const { data } = await events.getNearby(position.latitude, position.longitude, 100);
          setEventList(data);
        } else {
          const { data } = await events.getUpcoming();
          setEventList(data.items || data as unknown as CardEvent[]);
        }
      } catch (err) {
        console.error('Errore caricamento eventi:', err);
      } finally {
        setIsLoading(false);
      }
    };
    load();
  }, [mode, position]);

  const handleJoin = async (eventId: number) => {
    try {
      await events.join(eventId);
      setEventList((prev) =>
        prev.map((e) =>
          e.id === eventId
            ? { ...e, isParticipating: true, participantCount: e.participantCount + 1 }
            : e
        )
      );
    } catch (err) {
      console.error('Errore iscrizione:', err);
    }
  };

  const handleLeave = async (eventId: number) => {
    try {
      await events.leave(eventId);
      setEventList((prev) =>
        prev.map((e) =>
          e.id === eventId
            ? { ...e, isParticipating: false, participantCount: e.participantCount - 1 }
            : e
        )
      );
    } catch (err) {
      console.error('Errore disiscrizione:', err);
    }
  };

  const formatDate = (date: string) => {
    const d = new Date(date);
    return d.toLocaleDateString('it-IT', {
      weekday: 'short',
      day: 'numeric',
      month: 'short',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  return (
    <div>
      <h1 className="text-xl font-bold text-text mb-1">Eventi</h1>
      <p className="text-sm text-text-secondary mb-4">Meetup e tornei di scambio</p>

      {/* Mode tabs */}
      <div className="flex gap-2 mb-4">
        <button
          onClick={() => setMode('upcoming')}
          className={`flex-1 py-2.5 rounded-xl text-sm font-semibold transition-colors ${
            mode === 'upcoming'
              ? 'bg-primary text-white'
              : 'bg-white text-text-secondary border border-border'
          }`}
        >
          Prossimi
        </button>
        <button
          onClick={() => {
            if (!position) requestPosition();
            setMode('nearby');
          }}
          className={`flex-1 py-2.5 rounded-xl text-sm font-semibold transition-colors flex items-center justify-center gap-1 ${
            mode === 'nearby'
              ? 'bg-primary text-white'
              : 'bg-white text-text-secondary border border-border'
          }`}
        >
          <Navigation size={14} />
          Vicino a me
        </button>
      </div>

      {isLoading || geoLoading ? (
        <div className="flex justify-center py-12">
          <Loader2 size={28} className="animate-spin text-primary" />
        </div>
      ) : eventList.length === 0 ? (
        <EmptyState
          icon={CalendarDays}
          title="Nessun evento trovato"
          description={mode === 'nearby'
            ? 'Nessun evento nelle vicinanze'
            : 'Non ci sono eventi in programma'}
        />
      ) : (
        <div className="space-y-3">
          {eventList.map((event) => (
            <div key={event.id} className="bg-white rounded-2xl overflow-hidden shadow-sm border border-border/50">
              {event.imageUrl && (
                <img
                  src={event.imageUrl}
                  alt={event.title}
                  className="w-full h-36 object-cover"
                  loading="lazy"
                />
              )}
              <div className="p-4">
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <span className="text-xs font-semibold text-primary uppercase tracking-wide">
                      {event.type}
                    </span>
                    <h3 className="text-base font-bold text-text mt-0.5">{event.title}</h3>
                  </div>
                </div>

                <div className="flex flex-col gap-1.5 mt-3 text-sm text-text-secondary">
                  <div className="flex items-center gap-2">
                    <CalendarDays size={14} className="text-text-muted shrink-0" />
                    <span>{formatDate(event.startDate)}</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <MapPin size={14} className="text-text-muted shrink-0" />
                    <span className="truncate">{event.city}{event.province && `, ${event.province}`}</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <Users size={14} className="text-text-muted shrink-0" />
                    <span>
                      {event.participantCount} partecipanti
                      {event.maxParticipants && ` / ${event.maxParticipants}`}
                    </span>
                  </div>
                </div>

                {event.description && (
                  <p className="text-xs text-text-muted mt-2 line-clamp-2">{event.description}</p>
                )}

                <div className="flex items-center justify-between mt-4">
                  <div className="flex items-center gap-2">
                    <div className="w-6 h-6 rounded-full bg-primary/10 flex items-center justify-center">
                      <span className="text-xs font-bold text-primary">
                        {event.organizer.username[0].toUpperCase()}
                      </span>
                    </div>
                    <span className="text-xs text-text-secondary">@{event.organizer.username}</span>
                  </div>

                  {event.isParticipating ? (
                    <Button size="sm" variant="outline" onClick={() => handleLeave(event.id)}>
                      Annulla
                    </Button>
                  ) : (
                    <Button size="sm" onClick={() => handleJoin(event.id)}>
                      Partecipa
                    </Button>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
