import { useState, useEffect, useCallback } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  ArrowLeftRight, DollarSign, Send, ArrowLeft, Loader2, X, Check, Search, MessageSquare,
} from 'lucide-react';
import { cards, wishlist, tradeOffers } from '../api';
import { useAuth } from '../context/AuthContext';
import type { Card, TradeEvaluation, WishlistItem } from '../types';
import Button from '../components/ui/Button';
import TradeBalanceMeter from '../components/trades/TradeBalanceMeter';
import OfflineDealNotice from '../components/trades/OfflineDealNotice';
import PriceTag from '../components/cards/PriceTag';
import { useFormat } from '../i18n/format';

type ProposalMode = 'trade' | 'buy' | 'request';

export default function TradeRequestPage() {
  const { t } = useTranslation('trades');
  const { euro } = useFormat();
  const { userId } = useParams<{ userId: string }>();
  const { user: currentUser } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const receiverId = Number(userId);
  const preselectedCards: Card[] = (location.state as any)?.selectedCards ?? [];
  // Se presente, la pagina crea una contro-offerta all'offerta indicata
  const counterOfferId: number | null = (location.state as any)?.counterOfferId ?? null;

  // Requested cards (from the other user)
  const [requestedCards, setRequestedCards] = useState<Card[]>(preselectedCards);

  // The other user's full collection (for adding more)
  const [, setOtherUserCards] = useState<Card[]>([]);
  const [otherUsername, setOtherUsername] = useState('');

  // My cards (for offering in trade)
  const [myCards, setMyCards] = useState<Card[]>([]);
  const [offeredCards, setOfferedCards] = useState<Card[]>([]);
  // Quantità offerta per carta (default 1)
  const [offeredQty, setOfferedQty] = useState<Record<number, number>>({});
  const qtyOf = (cardId: number) => offeredQty[cardId] ?? 1;

  // Equilibrio dello scambio (calcolato dal backend con prezzi di mercato)
  const [evaluation, setEvaluation] = useState<TradeEvaluation | null>(null);
  const [evaluating, setEvaluating] = useState(false);
  const [suggesting, setSuggesting] = useState(false);
  const [suggestMessage, setSuggestMessage] = useState<string | null>(null);

  // Wishlist matching: cards I own that the other user is searching
  const [matchingCards, setMatchingCards] = useState<Card[]>([]);

  const [mode, setMode] = useState<ProposalMode>('trade');
  const [buyPrice, setBuyPrice] = useState('');
  const [message, setMessage] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [isSending, setIsSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);
  const [createdOfferId, setCreatedOfferId] = useState<number | null>(null);
  const [searchMyCards, setSearchMyCards] = useState('');

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
        // L'API restituisce { username, cards }: userUsername sulle singole carte può essere vuoto
        setOtherUsername((otherData as any)?.username || otherCards[0]?.userUsername || '');

        // Load my cards
        const { data: myData } = await cards.getByUser(currentUser.id);
        const allMyCards = Array.isArray(myData) ? myData : (myData as any).cards ?? [];
        setMyCards(allMyCards.filter((c: Card) => c.isAvailableForTrade));

        // Contro-offerta: parte dall'offerta originale a ruoli invertiti
        if (counterOfferId) {
          const { data: original } = await tradeOffers.getById(counterOfferId);
          const theirIds = new Set(original.offeredCards.map((i) => i.cardId));
          const myIds = new Set(original.requestedCards.map((i) => i.cardId));
          setRequestedCards(otherCards.filter((c: Card) => theirIds.has(c.id)));
          setOfferedCards(allMyCards.filter((c: Card) => myIds.has(c.id)));
          if (original.senderUsername) setOtherUsername(original.senderUsername);
        }

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
  }, [currentUser, receiverId, counterOfferId]);

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

  useEffect(() => {
    if (requestedCards.length === 0 && offeredCards.length === 0) {
      setEvaluation(null);
      return;
    }
    let cancelled = false;
    const timer = setTimeout(async () => {
      setEvaluating(true);
      try {
        const { data } = await tradeOffers.evaluate(
          mode === 'trade' ? offeredCards.map((c) => ({ cardId: c.id, quantity: offeredQty[c.id] ?? 1 })) : [],
          requestedCards.map((c) => ({ cardId: c.id, quantity: 1 })),
        );
        if (!cancelled) setEvaluation(data);
      } catch {
        if (!cancelled) setEvaluation(null);
      } finally {
        if (!cancelled) setEvaluating(false);
      }
    }, 350);
    return () => { cancelled = true; clearTimeout(timer); };
  }, [requestedCards, offeredCards, offeredQty, mode]);

  // Prezzi dal backend (aggiornati); in mancanza quelli già presenti sulla carta
  const priceOf = (card: Card) => {
    const item = evaluation?.items.find((i) => i.cardId === card.id);
    return {
      asking: item ? item.askingPrice : card.askingPrice,
      market: item?.unitValue ?? card.marketValue ?? card.estimatedValue,
    };
  };

  const handleSuggest = async () => {
    if (requestedCards.length === 0) {
      setSuggestMessage(t('request.suggestPickFirst'));
      return;
    }
    setSuggesting(true);
    setSuggestMessage(null);
    try {
      const { data } = await tradeOffers.suggest(receiverId, requestedCards.map((c) => ({ cardId: c.id, quantity: 1 })));
      if (data.offeredCards.length > 0) {
        const byId = new Map(myCards.map((c) => [c.id, c]));
        setOfferedCards(data.offeredCards.map((o) => byId.get(o.cardId)).filter((c): c is Card => !!c));
        setOfferedQty(Object.fromEntries(data.offeredCards.map((o) => [o.cardId, o.quantity])));
        setSuggestMessage(t('request.suggestResult', { count: data.offeredCards.length, username: otherUsername ? `@${otherUsername}` : t('request.theUser') }));
      } else {
        setSuggestMessage(data.message ?? t('request.suggestNone'));
      }
    } catch {
      setSuggestMessage(t('request.suggestError'));
    } finally {
      setSuggesting(false);
    }
  };

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
          setError(t('request.invalidPrice'));
          setIsSending(false);
          return;
        }
        finalMessage = `${t('request.buyProposalTag', { price: price.toFixed(2) })}${message ? ` — ${message}` : ''}`;
      }

      if (mode === 'request' && !finalMessage) {
        finalMessage = t('request.defaultRequestMessage');
      }

      if (mode === 'trade' && offeredCards.length === 0) {
        setError(t('request.selectOffered'));
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
          ? offeredCards.map((c) => ({ cardId: c.id, quantity: qtyOf(c.id) }))
          : myCards.length > 0
            ? [{ cardId: myCards[0].id, quantity: 0 }]
            : [],
        requestedCards: requestedCards.map((c) => ({ cardId: c.id, quantity: 1 })),
      };

      // If buy/request mode and backend requires offeredCards min 1,
      // we include the offered cards the user may have selected optionally
      if (mode !== 'trade') {
        payload.offeredCards = offeredCards.length > 0
          ? offeredCards.map((c) => ({ cardId: c.id, quantity: qtyOf(c.id) }))
          : [];
      }

      const { data: createdOffer } = counterOfferId
        ? await tradeOffers.counter(counterOfferId, {
            message: payload.message,
            offeredCards: payload.offeredCards,
            requestedCards: payload.requestedCards,
          })
        : await tradeOffers.create(payload);
      setCreatedOfferId((createdOffer as any)?.id ?? null);
      setSuccess(true);
    } catch (err: any) {
      const msg = err?.response?.data?.message || t('request.sendError');
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


  if (success) {
    const chatUrl = `/chat?userId=${receiverId}${createdOfferId ? `&tradeOfferId=${createdOfferId}` : ''}`;
    return (
      <div className="flex flex-col items-center justify-center py-20 px-4">
        <div className="w-16 h-16 rounded-full bg-green-100 flex items-center justify-center mb-4">
          <Check size={32} className="text-green-600" />
        </div>
        <h2 className="text-xl font-bold text-text mb-2">{counterOfferId ? t('request.success.counterSent') : t('request.success.requestSent')}</h2>
        <p className="text-sm text-text-secondary mb-8 text-center">
          {t('request.success.sentTo')}{' '}
          <span className="font-semibold">@{otherUsername || t('request.userFallback', { id: receiverId })}</span>
        </p>
        <div className="flex flex-col gap-3 w-full max-w-xs">
          <Button onClick={() => navigate(chatUrl)} className="w-full" size="lg">
            <MessageSquare size={16} />
            {t('request.success.chatWith', { username: otherUsername || t('request.userFallback', { id: receiverId }) })}
          </Button>
          <Button onClick={() => navigate('/trades')} variant="outline" className="w-full" size="lg">
            <ArrowLeftRight size={16} />
            {t('request.success.goToTrades')}
          </Button>
        </div>
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
          <h1 className="text-xl font-bold text-text">{counterOfferId ? t('request.counterTitle') : t('request.newTitle')}</h1>
          <p className="text-sm text-text-secondary">{t('request.toUser', { username: otherUsername || t('request.userFallback', { id: receiverId }) })}</p>
        </div>
      </div>

      {/* Selected cards I want */}
      <section className="mb-5">
        <h2 className="text-sm font-semibold text-text-secondary mb-2 uppercase tracking-wide">
          {t('request.requestedCards', { n: requestedCards.length })}
        </h2>
        {requestedCards.length === 0 ? (
          <p className="text-xs text-text-muted bg-surface-dark rounded-xl p-4 text-center">
            {t('request.noneSelected')}
          </p>
        ) : (
          <div className="space-y-1.5">
            {requestedCards.map((card) => (
              <MiniCard key={card.id} card={card} price={priceOf(card)} onRemove={() => toggleRequested(card)} />
            ))}
          </div>
        )}
      </section>

      {/* Proposal mode */}
      <section className="mb-5">
        <h2 className="text-sm font-semibold text-text-secondary mb-2 uppercase tracking-wide">
          {t('request.proposalType')}
        </h2>
        <div className="flex gap-2">
          {([
            { key: 'trade' as const, icon: ArrowLeftRight, label: t('request.modes.trade') },
            { key: 'buy' as const, icon: DollarSign, label: t('request.modes.buy') },
            { key: 'request' as const, icon: Send, label: t('request.modes.request') },
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

      {mode === 'trade' && (
        <TradeBalanceMeter
          evaluation={evaluation}
          loading={evaluating}
          onSuggest={handleSuggest}
          suggesting={suggesting}
          suggestMessage={suggestMessage}
        />
      )}

      {/* Trade mode: select cards to offer */}
      {mode === 'trade' && (
        <section className="mb-5">
          <h2 className="text-sm font-semibold text-text-secondary mb-2 uppercase tracking-wide">
            {t('request.offeredCards', { n: offeredCards.length })}
          </h2>

          {/* Matching cards (cards I have that they want) */}
          {matchingCards.length > 0 && (
            <div className="mb-3">
              <p className="text-xs text-green-600 font-medium mb-1.5 flex items-center gap-1">
                <Check size={12} />
                {t('request.theyAreLooking', { username: otherUsername })}
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
              placeholder={t('request.searchMyCards')}
              value={searchMyCards}
              onChange={(e) => setSearchMyCards(e.target.value)}
              className="w-full pl-8 pr-4 py-2 rounded-xl border border-border bg-white text-xs focus:outline-none focus:ring-2 focus:ring-primary/30"
            />
          </div>
          <div className="max-h-52 overflow-y-auto space-y-1 border border-border/50 rounded-xl p-2 bg-surface-dark">
            {filteredMyCards.length === 0 ? (
              <p className="text-xs text-text-muted text-center py-3">{t('request.noCardsAvailable')}</p>
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
                <MiniCard
                  key={card.id}
                  card={card}
                  price={priceOf(card)}
                  onRemove={() => toggleOffered(card)}
                  offered
                  quantity={qtyOf(card.id)}
                  onQuantityChange={card.quantity > 1
                    ? (q) => setOfferedQty((prev) => ({ ...prev, [card.id]: Math.min(Math.max(1, q), card.quantity) }))
                    : undefined}
                />
              ))}
            </div>
          )}
        </section>
      )}

      {/* Buy mode: price input */}
      {mode === 'buy' && (
        <section className="mb-5">
          <h2 className="text-sm font-semibold text-text-secondary mb-2 uppercase tracking-wide">
            {t('request.buyOffer')}
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
          {evaluation && evaluation.requestedTotal > 0 && (
            <p className="text-xs text-text-muted mt-1">
              {t('request.estimatedMarketValue', { value: euro(evaluation.requestedTotal) })}
            </p>
          )}
        </section>
      )}

      {/* Request mode: info */}
      {mode === 'request' && (
        <section className="mb-5">
          <div className="bg-blue-50 rounded-xl p-3 text-sm text-blue-700">
            {t('request.requestInfo')}
          </div>
        </section>
      )}

      {/* Message */}
      <section className="mb-5">
        <h2 className="text-sm font-semibold text-text-secondary mb-2 uppercase tracking-wide">
          {t('request.messageOptional')}
        </h2>
        <textarea
          value={message}
          onChange={(e) => setMessage(e.target.value)}
          placeholder={t('request.messagePlaceholder')}
          rows={3}
          maxLength={1000}
          className="w-full px-4 py-3 rounded-xl border border-border text-sm focus:outline-none focus:ring-2 focus:ring-primary/30 resize-none"
        />
      </section>

      <OfflineDealNotice className="mb-4" />

      {/* Error */}
      {error && (
        <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-xl text-sm text-red-700">
          {error}
        </div>
      )}

      {/* Submit */}
      <div className="fixed bottom-16 md:bottom-0 left-0 right-0 bg-white border-t border-border p-4 pb-10 md:pb-4 z-40">
        <Button
          onClick={handleSubmit}
          className="w-full"
          size="lg"
          isLoading={isSending}
          disabled={requestedCards.length === 0}
        >
          {mode === 'trade' && <><ArrowLeftRight size={16} /> {t('request.submit.trade')}</>}
          {mode === 'buy' && <><DollarSign size={16} /> {t('request.submit.buy')}</>}
          {mode === 'request' && <><Send size={16} /> {t('request.submit.request')}</>}
        </Button>
      </div>
    </div>
  );
}

/* ---- Mini Card Components ---- */

function MiniCard({ card, onRemove, offered, price, quantity, onQuantityChange }: {
  card: Card;
  onRemove: () => void;
  offered?: boolean;
  /** Prezzo indicato dal proprietario e valore di mercato (unitari). */
  price?: { asking?: number | null; market?: number | null };
  quantity?: number;
  onQuantityChange?: (q: number) => void;
}) {
  const { t } = useTranslation('trades');
  return (
    <div className={`flex items-center gap-2.5 p-2 rounded-xl border ${offered ? 'border-amber-200 bg-amber-50/50' : 'border-border/50 bg-white'}`}>
      {/* {image ? (
        <img src={image} alt={card.cardName} className="w-9 h-12 rounded-md object-cover shrink-0" />
      ) : (
        <div className="w-9 h-12 rounded-md bg-gray-200 shrink-0" />
      )} */}
      <div className="flex-1 min-w-0">
        <p className="text-sm font-semibold truncate">{card.cardName || t('request.cardFallback')}</p>
        <p className="text-xs text-text-secondary truncate">{card.cardSetName}</p>
        <PriceTag askingPrice={price?.asking ?? card.askingPrice} marketValue={price?.market ?? card.marketValue ?? card.estimatedValue} />
        {quantity != null && quantity > 1 && <span className="text-[10px] text-text-muted"> {t('request.each')}</span>}
      </div>
      {onQuantityChange && quantity != null && (
        <div className="flex items-center gap-1 shrink-0" aria-label={t('request.offeredQuantity')}>
          <button type="button" onClick={() => onQuantityChange(quantity - 1)} className="w-6 h-6 rounded-lg border border-border text-xs">-</button>
          <span className="w-5 text-center text-xs font-semibold">{quantity}</span>
          <button type="button" onClick={() => onQuantityChange(quantity + 1)} className="w-6 h-6 rounded-lg border border-border text-xs">+</button>
        </div>
      )}
      <button onClick={onRemove} className="p-1 rounded-lg hover:bg-red-50 text-text-muted hover:text-red-500 shrink-0">
        <X size={16} />
      </button>
    </div>
  );
}

function MiniCardToggle({ card, selected, onToggle, highlight }: {
  card: Card; selected: boolean; onToggle: () => void; highlight?: boolean;
}) {
  const { t } = useTranslation('trades');
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
        <p className="text-xs font-semibold truncate">{card.cardName || t('request.cardFallback')}</p>
        <p className="text-[10px] text-text-muted truncate">
          {card.cardSetName}
        </p>
        <PriceTag compact askingPrice={card.askingPrice} marketValue={card.marketValue ?? card.estimatedValue} />
      </div>
      <div className={`w-5 h-5 rounded-full border-2 flex items-center justify-center shrink-0 ${
        selected ? 'border-primary bg-primary' : 'border-border'
      }`}>
        {selected && <Check size={12} className="text-white" />}
      </div>
    </button>
  );
}
