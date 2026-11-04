import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ArrowLeftRight, Radio, RefreshCw, Users } from 'lucide-react';
import { events } from '../../api';
import type { Card, EventLive, LiveMatchCard } from '../../types';
import { apiErrorMessage } from '../../utils/apiError';
import Button from '../ui/Button';
import CardVariantBadges from '../cards/CardVariantBadges';

const REFRESH_MS = 30_000;
const time = (iso: string) => new Date(iso).toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' });

/** Serata di scambio: check-in, presenti e match con ciascuno, aggiornati ogni 30 secondi. */
export default function TradeNightPanel({ eventId }: { eventId: number }) {
  const navigate = useNavigate();
  const [live, setLive] = useState<EventLive | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    events.live(eventId).then(({ data }) => setLive(data)).catch(() => setLive(null));
  }, [eventId]);

  useEffect(() => {
    load();
    const timer = setInterval(load, REFRESH_MS);
    return () => clearInterval(timer);
  }, [load]);

  if (!live) return null;

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

  const proposeTrade = (userId: number, cards: LiveMatchCard[]) => {
    // La pagina di richiesta accetta le carte preselezionate: bastano id e dati da mostrare
    const selectedCards = cards.map((c) => ({
      id: c.cardId, userId, cardInfoId: 0, cardName: c.name, condition: c.condition, language: c.language,
      finish: c.finish, quantity: 1, isAvailableForTrade: true, estimatedValue: c.priceEur,
    }) as Card);
    navigate(`/trades/new/${userId}`, { state: { selectedCards } });
  };

  const notStarted = new Date(live.opensAt).getTime() > Date.now();

  return (
    <section className="rounded-2xl border-2 border-primary/30 bg-white p-3 space-y-3">
      <div className="flex items-center gap-2">
        <Radio size={16} className={live.isOpen ? 'text-red-500 animate-pulse' : 'text-text-muted'} />
        <h2 className="text-sm font-semibold flex-1">Serata di scambio</h2>
        <span className="text-xs text-text-secondary flex items-center gap-1"><Users size={13} /> {live.presentCount} presenti</span>
      </div>

      {!live.isOpen ? (
        <p className="text-xs text-text-secondary">
          {notStarted
            ? `Il check-in apre alle ${time(live.opensAt)}: quando sei sul posto fai check-in per vedere chi c'è e con chi puoi scambiare.`
            : 'La serata è terminata.'}
        </p>
      ) : !live.checkedIn ? (
        <div className="space-y-2">
          <p className="text-xs text-text-secondary">
            Sei sul posto? Fai check-in: vedrai chi è presente e le carte che potete scambiarvi. Gli altri ti vedranno solo dopo il tuo check-in.
          </p>
          <Button className="w-full" isLoading={busy} onClick={() => act(() => events.checkIn(eventId))}>Sono qui, fai check-in</Button>
        </div>
      ) : (
        <div className="space-y-2">
          {live.attendees.length === 0 ? (
            <p className="text-xs text-text-muted text-center py-3">Sei il primo! Gli altri presenti compariranno qui.</p>
          ) : live.attendees.map((a) => {
            const hasMatch = a.theyHaveCount + a.iHaveCount > 0;
            return (
              <div key={a.userId} className={`p-3 rounded-xl border ${a.isMutual ? 'border-green-300 bg-green-50/50' : 'border-border'}`}>
                <div className="flex items-center justify-between gap-2">
                  <Link to={`/collection/${a.userId}`} className="text-sm font-semibold hover:underline">@{a.username}</Link>
                  {a.isMutual && <span className="text-[10px] px-2 py-0.5 rounded-full bg-green-100 text-green-800 font-semibold">Scambio reciproco</span>}
                </div>
                {!hasMatch ? (
                  <p className="text-[11px] text-text-muted mt-0.5">Nessuna carta in comune con le vostre wishlist.</p>
                ) : (
                  <div className="mt-1.5 space-y-1.5 text-xs">
                    {a.theyHaveCount > 0 && (
                      <div>
                        <p className="text-text-secondary font-medium">Ha {a.theyHaveCount} {a.theyHaveCount === 1 ? 'carta che cerchi' : 'carte che cerchi'}</p>
                        <ul className="mt-0.5 space-y-0.5">
                          {a.theyHave.map((c) => (
                            <li key={c.cardId} className="flex items-center gap-1.5">
                              <span className="truncate">{c.name}</span>
                              <CardVariantBadges language={c.language} finish={c.finish} />
                              {c.priceEur != null && <span className="ml-auto text-text-muted">€{c.priceEur.toFixed(2)}</span>}
                            </li>
                          ))}
                        </ul>
                      </div>
                    )}
                    {a.iHaveCount > 0 && (
                      <p className="text-text-secondary">
                        Cerca {a.iHaveCount === 1 ? 'una tua carta' : `${a.iHaveCount} tue carte`}: {a.iHave.map((c) => c.name).join(', ')}
                      </p>
                    )}
                    {a.theyHaveCount > 0 && (
                      <button
                        type="button"
                        onClick={() => proposeTrade(a.userId, a.theyHave)}
                        className="inline-flex items-center gap-1 text-xs font-semibold text-primary hover:underline"
                      >
                        <ArrowLeftRight size={13} /> Proponi scambio
                      </button>
                    )}
                  </div>
                )}
              </div>
            );
          })}
          <div className="flex items-center justify-between text-[11px] text-text-muted">
            <span className="inline-flex items-center gap-1"><RefreshCw size={11} /> Aggiornamento automatico ogni 30 secondi</span>
            <button type="button" disabled={busy} onClick={() => act(() => events.checkOut(eventId))} className="text-danger hover:underline">Esci dalla serata</button>
          </div>
        </div>
      )}
      {error && <p className="text-xs text-danger">{error}</p>}
    </section>
  );
}
