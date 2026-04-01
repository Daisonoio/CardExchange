import { useState, useEffect, useCallback } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import {
  ArrowLeftRight, DollarSign, Send, ArrowLeft, Loader2, X, Plus, Check, Search,
} from 'lucide-react';
import { cards, wishlist, tradeOffers } from '../api';
import { useAuth } from '../context/AuthContext';
import type { Card, WishlistItem } from '../types';
import Button from '../components/ui/Button';

type ProposalMode = 'trade' | 'buy' | 'request';

export default function TradeRequestPage() {
  const { userId } = useParams<{ userId: string }>();
  const { user: currentUser } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const receiverId = Number(userId);
  const preselectedCards: Card[] = (location.state as any)?.selectedCards ?? [];

  // Requested cards (from the other user)
  const [requestedCards, setRequestedCards] = useState<Card[]>(preselectedCards);

  // The other user's full collection (for adding more)
  const [otherUserCards, setOtherUserCards] = useState<Card[]>([]);
  const [otherUsername, setOtherUsername] = useState('');

  // My cards (for offering in trade)
  const [myCards, setMyCards] = useState<Card[]>([]);
  const [offeredCards, setOfferedCards] = useState<Card[]>([]);

  // Wishlist matching: cards I own that the other user is searching
  const [matchingCards, setMatchingCards] = useState<Card[]>([]);

  const [mode, setMode] = useState<ProposalMode>('trade');
  const [buyPrice, setBuyPrice] = useState('');
  const [message, setMessage] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [isSending, setIsSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);
  const [searchMyCards, setSearchMyCards] = useState('');
  const [searchOtherCards, setSearchOtherCards] = useState('');

  // Load data
  useEffect(() => {
    const load = async () => {
      if (!currentUser || !receiverId) return;
      setIsLoading(true);
      try {
        // Load other user's cards
        const { data: otherData } = await cards.getByUser(receiverId);
        const otherCards = Array.isArray(otherData) ? otherData : (otherData as any).cards ?? [];
        setOtherUserCards(otherCards.filter((c: Card) => c.isAvailableForTrade));
        if (otherCards.length > 0) {
          setOtherUsername(otherCards[0].userUsername || '');
        }

        // Load my cards
        const { data: myData } = await cards.getByUser(currentUser.id);
        const allMyCards = Array.isArray(myData) ? myData : (myData as any).cards ?? [];
        setMyCards(allMyCards.filter((c: Card) => c.isAvailableForTrade));

        // Load other user's wishlist to find matching cards
        try {
          const { data: wlData } = await wishlist.getByUser(receiverId);
          const wlItems: WishlistItem[] = Array.isArray(wlData)
            ? wlData
            : (wlData as any)?.items ?? [];
          const wantedCardInfoIds = new Set(wlItems.map((w) => w.cardInfoId));
          const matches = allMyCards.filter(
            (c: Card) => c.isAvailableForTrade && wantedCardInfoIds.has(c.cardInfoId)
          );
          setMatchingCards(matches);
        } catch {
          // Wishlist might not be accessible
          setMatchingCards([]);
        }
      } catch (err) {
        console.error('Errore caricamento:', err);
      } finally {
        setIsLoading(false);
      }
    };
    load();
  }, [currentUser, receiverId]);

  // Toggle card in requested list
  const toggleRequested = useCallback((card: Card) => {
    setRequestedCards((prev) =>
      prev.find((c) => c.id === card.id)
        ? prev.filter((c) => c.id !== card.id)
        : [...prev, card]
    );
  }, []);

  // Toggle card in offered list
  const toggleOffered = useCallback((card: Card) => {
    setOfferedCards((prev) =>
      prev.find((c) => c.id === card.id)
        ? prev.filter((c) => c.id !== card.id)
        : [...prev, card]
    );
  }, []);

  // Send the trade request
  const handleSubmit = async () => {
    if (!currentUser || requestedCards.length === 0) return;
    setIsSending(true);
    setError(null);

    try {
      let finalMessage = message;

      if (mode === 'buy') {
        const price = parseFloat(buyPrice);
        if (!price || price <= 0) {
          setError('Inserisci un prezzo valido');
          setIsSending(false);
          return;
        }
        finalMessage = `[PROPOSTA ACQUISTO: €${price.toFixed(2)}]${message ? ` — ${message}` : ''}`;
      }

      if (mode === 'request' && !finalMessage) {
        finalMessage = 'Richiesta carte senza proposta specifica';
      }

      if (mode === 'trade' && offeredCards.length === 0) {
        setError('Seleziona almeno una carta da offrire per lo scambio');
        setIsSending(false);
        return;
      }

      // For 'buy' and 'request' modes, we still need to send offered cards
      // (backend requires at least 1). We'll use the requested cards as both
      // or handle via message only. Actually backend requires both lists...
      // For now, in buy/request mode we send a "dummy" trade with message.

      const payload = {
        receiverId,
        message: finalMessage || undefined,
        offeredCards: mode === 'trade'
          ? offeredCards.map((c) => ({ cardId: c.id, quantity: 1 }))
          : myCards.length > 0
            ? [{ cardId: myCards[0].id, quantity: 0 }]
            : [],
        requestedCards: requestedCards.map((c) => ({ cardId: c.id, quantity: 1 })),
      };

      // If buy/request mode and backend requires offeredCards min 1,
      // we include the offered cards the user may have selected optionally
      if (mode !== 'trade') {
        payload.offeredCards = offeredCards.length > 0
          ? offeredCards.map((c) => ({ cardId: c.id, quantity: 1 }))
          : [];
      }

      await tradeOffers.create(payload);
      setSuccess(true);
      setTimeout(() => navigate('/trades'), 1500);
    } catch (err: any) {
      const msg = err?.response?.data?.message || 'Errore durante l\'invio della richiesta';
      setError(msg);
    } finally {
      setIsSending(false);
    }
  };

  // Filter helpers
  const filteredMyCards = searchMyCards
    ? myCards.filter((c) =>
        (c.cardName || '').toLowerCase().includes(searchMyCards.toLowerCase()) ||
        (c.cardSetName || '').toLowerCase().includes(searchMyCards.toLowerCase())
      )
    : myCards;

  const filteredOtherCards = searchOtherCards
    ? otherUserCards.filter((c) =>
        !requestedCards.find((r) => r.id === c.id) &&
        ((c.cardName || '').toLowerCase().includes(searchOtherCards.toLowerCase()) ||
        (c.cardSetName || '').toLowerCase().includes(searchOtherCards.toLowerCase()))
      )
    : otherUserCards.filter((c) => !requestedCards.find((r) => r.id === c.id));

  const totalRequestedValue = requestedCards.reduce((sum, c) => sum + (c.estimatedValue || 0), 0);
  const totalOfferedValue = offeredCards.reduce((sum, c) => sum + (c.estimatedValue || 0), 0);

  if (success) {
    return (
      <div className="flex flex-col items-center justify-center py-20">
        <div className="w-16 h-16 rounded-full bg-green-100 flex items-center justify-center mb-4">
          <Check size={32} className="text-green-600" />
        </div>
        <h2 className="text-xl font-bold text-text mb-2">Richiesta inviata!</h2>
        <p className="text-sm text-text-secondary">Reindirizzamento alle tue richieste...</p>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="flex justify-center py-20">
        <Loader2 size={32} className="animate-spin text-primary" />
      </div>
    );
  }

  return (
    <div className="pb-24">
      {/* Header */}
      <div className="flex items-center gap-3 mb-4">
        <button onClick={() => navigate(-1)} className="p-2 rounded-xl hover:bg-surface-dark">
          <ArrowLeft size={20} />
        </button>
        <div>
          <h1 className="text-xl font-bold text-text">Nuova Richiesta</h1>
          <p className="text-sm text-text-secondary">a @{otherUsername || `utente #${receiverId}`}</p>
        </div>
      </div>

      {/* Selected cards I want */}
      <section className="mb-5">
        <h2 className="text-sm font-semibold text-text-secondary mb-2 uppercase tracking-wide">
          Carte richieste ({requestedCards.length})
        </h2>
        {requestedCards.length === 0 ? (
          <p className="text-xs text-text-muted bg-surface-dark rounded-xl p-4 text-center">
            Nessuna carta selezionata. Aggiungi carte dalla collezione qui sotto.
          </p>
        ) : (
          <div className="space-y-1.5">
            {requestedCards.map((card) => (
              <MiniCard key={card.id} card={card} onRemove={() => toggleRequested(card)} />
            ))}
          </div>
        )}
        {totalRequestedValue > 0 && (
          <p className="text-xs text-text-muted mt-1 text-right">
            Valore stimato: €{totalRequestedValue.toFixed(2)}
          </p>
        )}
      </section>

    

      {/* Proposal mode */}
      <section className="mb-5">
        <h2 className="text-sm font-semibold text-text-secondary mb-2 uppercase tracking-wide">
          Tipo di proposta
        </h2>
        <div className="flex gap-2">
          {([
            { key: 'trade' as const, icon: ArrowLeftRight, label: 'Scambio' },
            { key: 'buy' as const, icon: DollarSign, label: 'Acquisto' },
            { key: 'request' as const, icon: Send, label: 'Richiesta' },
          ]).map(({ key, icon: Icon, label }) => (
            <button
              key={key}
              onClick={() => setMode(key)}
              className={`flex-1 flex flex-col items-center gap-1 py-3 rounded-xl text-xs font-semibold transition-colors border ${
                mode === key
                  ? 'border-primary bg-primary/10 text-primary'
                  : 'border-border text-text-secondary hover:bg-surface-dark'
              }`}
            >
              <Icon size={18} />
              {label}
            </button>
          ))}
        </div>
      </section>

      {/* Trade mode: select cards to offer */}
      {mode === 'trade' && (
        <section className="mb-5">
          <h2 className="text-sm font-semibold text-text-secondary mb-2 uppercase tracking-wide">
            Carte che offri ({offeredCards.length})
          </h2>

          {/* Matching cards (cards I have that they want) */}
          {matchingCards.length > 0 && (
            <div className="mb-3">
              <p className="text-xs text-green-600 font-medium mb-1.5 flex items-center gap-1">
                <Check size={12} />
                Carte che @{otherUsername} sta cercando
              </p>
              <div className="space-y-1">
                {matchingCards.map((card) => {
                  const isSelected = offeredCards.find((c) => c.id === card.id);
                  return (
                    <MiniCardToggle
                      key={card.id}
                      card={card}
                      selected={!!isSelected}
                      onToggle={() => toggleOffered(card)}
                      highlight
                    />
                  );
                })}
              </div>
            </div>
          )}

          {/* All my cards */}
          <div className="relative mb-2">
            <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
            <input
              type="text"
              placeholder="Cerca nelle tue carte..."
              value={searchMyCards}
              onChange={(e) => setSearchMyCards(e.target.value)}
              className="w-full pl-8 pr-4 py-2 rounded-xl border border-border bg-white text-xs focus:outline-none focus:ring-2 focus:ring-primary/30"
            />
          </div>
          <div className="max-h-52 overflow-y-auto space-y-1 border border-border/50 rounded-xl p-2 bg-surface-dark">
            {filteredMyCards.length === 0 ? (
              <p className="text-xs text-text-muted text-center py-3">Nessuna carta disponibile</p>
            ) : (
              filteredMyCards.slice(0, 30).map((card) => {
                const isSelected = offeredCards.find((c) => c.id === card.id);
                return (
                  <MiniCardToggle
                    key={card.id}
                    card={card}
                    selected={!!isSelected}
                    onToggle={() => toggleOffered(card)}
                  />
                );
              })
            )}
          </div>

          {offeredCards.length > 0 && (
            <div className="mt-2 space-y-1">
              {offeredCards.map((card) => (
                <MiniCard key={card.id} card={card} onRemove={() => toggleOffered(card)} offered />
              ))}
              {totalOfferedValue > 0 && (
                <p className="text-xs text-text-muted text-right">
                  Valore offerto: €{totalOfferedValue.toFixed(2)}
                </p>
              )}
            </div>
          )}
        </section>
      )}

      {/* Buy mode: price input */}
      {mode === 'buy' && (
        <section className="mb-5">
          <h2 className="text-sm font-semibold text-text-secondary mb-2 uppercase tracking-wide">
            Offerta di acquisto
          </h2>
          <div className="relative">
            <span className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted font-semibold">€</span>
            <input
              type="number"
              step="0.01"
              min="0"
              value={buyPrice}
              onChange={(e) => setBuyPrice(e.target.value)}
              placeholder="0.00"
              className="w-full pl-8 pr-4 py-3 rounded-xl border border-border text-lg font-bold focus:outline-none focus:ring-2 focus:ring-primary/30"
            />
          </div>
          {totalRequestedValue > 0 && (
            <p className="text-xs text-text-muted mt-1">
              Valore di mercato stimato: €{totalRequestedValue.toFixed(2)}
            </p>
          )}
        </section>
      )}

      {/* Request mode: info */}
      {mode === 'request' && (
        <section className="mb-5">
          <div className="bg-blue-50 rounded-xl p-3 text-sm text-blue-700">
            Invierai una richiesta senza una proposta specifica. L'altro utente potrà rispondere
            con una controproposta o accettare di contattarti.
          </div>
        </section>
      )}

      {/* Message */}
      <section className="mb-5">
        <h2 className="text-sm font-semibold text-text-secondary mb-2 uppercase tracking-wide">
          Messaggio (opzionale)
        </h2>
        <textarea
          value={message}
          onChange={(e) => setMessage(e.target.value)}
          placeholder="Aggiungi un messaggio alla tua richiesta..."
          rows={3}
          maxLength={1000}
          className="w-full px-4 py-3 rounded-xl border border-border text-sm focus:outline-none focus:ring-2 focus:ring-primary/30 resize-none"
        />
      </section>

      {/* Error */}
      {error && (
        <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-xl text-sm text-red-700">
          {error}
        </div>
      )}

      {/* Submit */}
      <div className="fixed bottom-16 md:bottom-0 left-0 right-0 bg-white border-t border-border p-4 z-40">
        <Button
          onClick={handleSubmit}
          className="w-full"
          size="lg"
          isLoading={isSending}
          disabled={requestedCards.length === 0}
        >
          {mode === 'trade' && <><ArrowLeftRight size={16} /> Proponi scambio</>}
          {mode === 'buy' && <><DollarSign size={16} /> Proponi acquisto</>}
          {mode === 'request' && <><Send size={16} /> Invia richiesta</>}
        </Button>
      </div>
    </div>
  );
}

/* ---- Mini Card Components ---- */

function MiniCard({ card, onRemove, offered }: { card: Card; onRemove: () => void; offered?: boolean }) {
  const image = card.imageSmall || card.imageNormal || '';
  return (
    <div className={`flex items-center gap-2.5 p-2 rounded-xl border ${offered ? 'border-amber-200 bg-amber-50/50' : 'border-border/50 bg-white'}`}>
      {/* {image ? (
        <img src={image} alt={card.cardName} className="w-9 h-12 rounded-md object-cover shrink-0" />
      ) : (
        <div className="w-9 h-12 rounded-md bg-gray-200 shrink-0" />
      )} */}
      <div className="flex-1 min-w-0">
        <p className="text-sm font-semibold truncate">{card.cardName || 'Carta'}</p>
        <p className="text-xs text-text-secondary truncate">{card.cardSetName}</p>
        {card.estimatedValue != null && card.estimatedValue > 0 && (
          <span className="text-xs font-medium text-accent">€{card.estimatedValue.toFixed(2)}</span>
        )}
      </div>
      <button onClick={onRemove} className="p-1 rounded-lg hover:bg-red-50 text-text-muted hover:text-red-500 shrink-0">
        <X size={16} />
      </button>
    </div>
  );
}

function MiniCardAdd({ card, onAdd }: { card: Card; onAdd: () => void }) {
  const image = card.imageSmall || card.imageNormal || '';
  return (
    <button onClick={onAdd} className="w-full flex items-center gap-2 p-1.5 rounded-lg hover:bg-white transition-colors text-left">
      {image ? (
        <img src={image} alt={card.cardName} className="w-7 h-10 rounded object-cover shrink-0" />
      ) : (
        <div className="w-7 h-10 rounded bg-gray-200 shrink-0" />
      )}
      <div className="flex-1 min-w-0">
        <p className="text-xs font-semibold truncate">{card.cardName || 'Carta'}</p>
        <p className="text-[10px] text-text-muted truncate">{card.cardSetName}</p>
      </div>
      <Plus size={14} className="text-primary shrink-0" />
    </button>
  );
}

function MiniCardToggle({ card, selected, onToggle, highlight }: {
  card: Card; selected: boolean; onToggle: () => void; highlight?: boolean;
}) {
  const image = card.imageSmall || card.imageNormal || '';
  return (
    <button
      onClick={onToggle}
      className={`w-full flex items-center gap-2 p-1.5 rounded-lg transition-colors text-left ${
        selected ? 'bg-primary/10 ring-1 ring-primary/30' : highlight ? 'bg-green-50 hover:bg-green-100' : 'hover:bg-white'
      }`}
    >
      {image ? (
        <img src={image} alt={card.cardName} className="w-7 h-10 rounded object-cover shrink-0" />
      ) : (
        <div className="w-7 h-10 rounded bg-gray-200 shrink-0" />
      )}
      <div className="flex-1 min-w-0">
        <p className="text-xs font-semibold truncate">{card.cardName || 'Carta'}</p>
        <p className="text-[10px] text-text-muted truncate">
          {card.cardSetName}
          {card.estimatedValue ? ` · €${card.estimatedValue.toFixed(2)}` : ''}
        </p>
      </div>
      <div className={`w-5 h-5 rounded-full border-2 flex items-center justify-center shrink-0 ${
        selected ? 'border-primary bg-primary' : 'border-border'
      }`}>
        {selected && <Check size={12} className="text-white" />}
      </div>
    </button>
  );
}
