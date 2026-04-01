import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  ArrowLeftRight, Loader2, Check, X, Clock, MessageSquare,
  ChevronRight, RotateCcw, ExternalLink,
} from 'lucide-react';
import { cards, tradeOffers, users } from '../api';
import { useAuth } from '../context/AuthContext';
import type { TradeOffer, TradeOfferStatus, User } from '../types';
import Button from '../components/ui/Button';
import EmptyState from '../components/ui/EmptyState';
import BottomSheet from '../components/ui/BottomSheet';

const STATUS_CONFIG: Record<TradeOfferStatus, { label: string; color: string; icon: typeof Check }> = {
  Pending: { label: 'In attesa', color: 'bg-amber-50 text-amber-600', icon: Clock },
  Accepted: { label: 'Accettata', color: 'bg-green-50 text-green-600', icon: Check },
  Rejected: { label: 'Rifiutata', color: 'bg-red-50 text-red-600', icon: X },
  Cancelled: { label: 'Annullata', color: 'bg-gray-100 text-gray-500', icon: X },
  Completed: { label: 'Completata', color: 'bg-blue-50 text-blue-600', icon: Check },
  CounterOffer: { label: 'Controproposta', color: 'bg-purple-50 text-purple-600', icon: RotateCcw },
};

type TabFilter = 'all' | 'received' | 'sent';

export default function TradesPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [offers, setOffers] = useState<TradeOffer[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [tab, setTab] = useState<TabFilter>('all');
  const [selectedOffer, setSelectedOffer] = useState<TradeOffer | null>(null);
  const [actionLoading, setActionLoading] = useState<number | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const checkOfferCardsAvailability = async (offer: TradeOffer) => {
    const cardIds = new Set<number>([
      ...offer.requestedCards.map((c) => c.cardId),
      ...offer.offeredCards.map((c) => c.cardId),
    ]);

    await Promise.all(Array.from(cardIds).map(async (cardId) => {
      const { data } = await cards.getById(cardId);
      if (!data || data.quantity < 1 || data.isAvailableForTrade === false) {
        throw new Error('Alcune carte non sono piu disponibili per questo scambio');
      }
    }));
  };

  const loadOffers = async () => {
    if (!user) return;
    setIsLoading(true);
    try {
      const { data } = await tradeOffers.getMine({ pageSize: 50 });
      const items = Array.isArray(data) ? data : (data as any)?.items ?? [];
      setOffers(Array.isArray(items) ? items : []);
    } catch (err) {
      console.error('Errore caricamento richieste:', err);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => { loadOffers(); }, [user]);

  const filteredOffers = offers.filter((o) => {
    if (tab === 'received') return o.receiverId === user?.id;
    if (tab === 'sent') return o.senderId === user?.id;
    return true;
  });

  const handleAction = async (offerId: number, action: 'accept' | 'reject' | 'cancel' | 'complete') => {
    setActionError(null);
    setActionLoading(offerId);
    try {
      if (action === 'accept') {
        const target = offers.find((o) => o.id === offerId);
        if (target) {
          await checkOfferCardsAvailability(target);
        }
      }
      if (action === 'accept') await tradeOffers.accept(offerId);
      else if (action === 'reject') await tradeOffers.reject(offerId);
      else if (action === 'cancel') await tradeOffers.cancel(offerId);
      else if (action === 'complete') await tradeOffers.complete(offerId);
      setSelectedOffer(null);
      await loadOffers();
    } catch (err: any) {
      setActionError(err?.response?.data?.message || err?.message || 'Errore durante l\'azione sullo scambio');
      console.error('Errore azione:', err);
    } finally {
      setActionLoading(null);
    }
  };

  return (
    <div>
      <h1 className="text-xl font-bold text-text mb-1">Scambi</h1>
      <p className="text-sm text-text-secondary mb-4">Le tue richieste di scambio</p>

      {/* Filter tabs */}
      <div className="flex gap-2 mb-4">
        {([
          { key: 'all' as const, label: 'Tutti' },
          { key: 'received' as const, label: 'Ricevute' },
          { key: 'sent' as const, label: 'Inviate' },
        ]).map(({ key, label }) => (
          <button
            key={key}
            onClick={() => setTab(key)}
            className={`flex-1 py-2.5 rounded-xl text-sm font-semibold transition-colors ${
              tab === key
                ? 'bg-primary text-white shadow-sm'
                : 'bg-white text-text-secondary border border-border'
            }`}
          >
            {label}
          </button>
        ))}
      </div>

      {/* List */}
      {isLoading ? (
        <div className="flex justify-center py-12">
          <Loader2 size={28} className="animate-spin text-primary" />
        </div>
      ) : filteredOffers.length === 0 ? (
        <EmptyState
          icon={ArrowLeftRight}
          title="Nessuna richiesta"
          description={tab === 'received'
            ? 'Non hai ricevuto richieste di scambio'
            : tab === 'sent'
            ? 'Non hai inviato richieste di scambio'
            : 'Nessuna richiesta di scambio. Esplora le carte disponibili per iniziare!'
          }
        />
      ) : (
        <div className="space-y-2">
          {actionError && (
            <div className="mb-2 p-3 bg-red-50 border border-red-200 rounded-xl text-sm text-red-700">
              {actionError}
            </div>
          )}
          {filteredOffers.map((offer) => {
            const isSender = offer.senderId === user?.id;
            const otherUser = isSender ? offer.receiverUsername : offer.senderUsername;
            const statusCfg = STATUS_CONFIG[offer.status] || STATUS_CONFIG.Pending;
            const StatusIcon = statusCfg.icon;

            return (
              <button
                key={offer.id}
                onClick={() => setSelectedOffer(offer)}
                className="w-full bg-white rounded-2xl p-4 shadow-sm border border-border/50 text-left hover:border-primary/30 transition-colors"
              >
                <div className="flex items-start gap-3">
                  <div className="w-10 h-10 rounded-full bg-primary/10 flex items-center justify-center shrink-0">
                    <ArrowLeftRight size={18} className="text-primary" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-1">
                      <span className="text-sm font-semibold truncate">
                        {isSender ? `A @${otherUser}` : `Da @${otherUser}`}
                      </span>
                      <span className={`px-2 py-0.5 rounded-full text-[10px] font-semibold ${statusCfg.color}`}>
                        <StatusIcon size={10} className="inline mr-0.5" />
                        {statusCfg.label}
                      </span>
                    </div>
                    <div className="flex gap-4 text-xs text-text-muted">
                      <span>{offer.requestedCards.length} richieste</span>
                      <span>{offer.offeredCards.length} offerte</span>
                    </div>
                    {offer.message && (
                      <p className="text-xs text-text-secondary mt-1 truncate flex items-center gap-1">
                        <MessageSquare size={10} /> {offer.message}
                      </p>
                    )}
                    <p className="text-[10px] text-text-muted mt-1">
                      {new Date(offer.createdAt).toLocaleDateString('it-IT', {
                        day: 'numeric', month: 'short', year: 'numeric',
                      })}
                    </p>
                  </div>
                  <ChevronRight size={16} className="text-text-muted shrink-0 mt-2" />
                </div>
              </button>
            );
          })}
        </div>
      )}

      {/* Detail Bottom Sheet */}
      <BottomSheet
        open={!!selectedOffer}
        onClose={() => setSelectedOffer(null)}
        title={`Dettaglio Richiesta #${selectedOffer?.id ?? ''}`}
      >
        {selectedOffer && (
          <OfferDetail
            offer={selectedOffer}
            userId={user?.id ?? 0}
            onAction={handleAction}
            actionLoading={actionLoading}
            actionError={actionError}
            onNavigateToCounter={(offerId) => {
              setSelectedOffer(null);
              navigate(`/trades/${offerId}/counter`);
            }}
          />
        )}
      </BottomSheet>
    </div>
  );
}

/* ---- Offer Detail Component ---- */
function OfferDetail({
  offer, userId, onAction, actionLoading, actionError, onNavigateToCounter,
}: {
  offer: TradeOffer;
  userId: number;
  onAction: (id: number, action: 'accept' | 'reject' | 'cancel' | 'complete') => void;
  actionLoading: number | null;
  actionError: string | null;
  onNavigateToCounter: (offerId: number) => void;
}) {
  const [otherUserPayment, setOtherUserPayment] = useState<Pick<User, 'paypalUsername' | 'satispayUsername' | 'paymentQrCodeUrl'> | null>(null);
  const isSender = offer.senderId === userId;
  const isReceiver = offer.receiverId === userId;
  const isPending = offer.status === 'Pending';
  const isAccepted = offer.status === 'Accepted';
  const loading = actionLoading === offer.id;

  useEffect(() => {
    const loadOtherUserPayment = async () => {
      try {
        const username = isSender ? offer.receiverUsername : offer.senderUsername;
        const { data } = await users.getByUsername(username);
        setOtherUserPayment({
          paypalUsername: data.paypalUsername,
          satispayUsername: data.satispayUsername,
          paymentQrCodeUrl: data.paymentQrCodeUrl,
        });
      } catch {
        setOtherUserPayment(null);
      }
    };
    loadOtherUserPayment();
  }, [isSender, offer.receiverUsername, offer.senderUsername]);

  const openPaypal = () => {
    const username = otherUserPayment?.paypalUsername?.trim();
    if (!username) return;
    window.open(`https://www.paypal.me/${encodeURIComponent(username)}`, '_blank', 'noopener,noreferrer');
  };

  const openQr = () => {
    if (!otherUserPayment?.paymentQrCodeUrl) return;
    window.open(otherUserPayment.paymentQrCodeUrl, '_blank', 'noopener,noreferrer');
  };

  return (
    <div className="space-y-4">
      {/* Status */}
      <div className="flex items-center justify-between">
        <div>
          <p className="text-xs text-text-muted">
            {isSender ? 'Inviata a' : 'Ricevuta da'}{' '}
            <span className="font-semibold text-text">
              @{isSender ? offer.receiverUsername : offer.senderUsername}
            </span>
          </p>
          <p className="text-[10px] text-text-muted mt-0.5">
            {new Date(offer.createdAt).toLocaleDateString('it-IT', {
              day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit',
            })}
          </p>
        </div>
        <span className={`px-2.5 py-1 rounded-full text-xs font-semibold ${STATUS_CONFIG[offer.status]?.color ?? ''}`}>
          {STATUS_CONFIG[offer.status]?.label ?? offer.status}
        </span>
      </div>

      {/* Message */}
      {offer.message && (
        <div className="bg-surface-dark rounded-xl p-3">
          <p className="text-xs font-semibold text-text-secondary mb-1">Messaggio</p>
          <p className="text-sm text-text">{offer.message}</p>
        </div>
      )}

      {/* Requested cards */}
      <div>
        <p className="text-xs font-semibold text-text-secondary mb-1.5 uppercase">
          Carte richieste ({offer.requestedCards.length})
        </p>
        <div className="space-y-1">
          {offer.requestedCards.map((item) => (
            <div key={item.id} className="flex items-center gap-2 p-2 bg-red-50/50 rounded-lg border border-red-100">
              <div className="flex-1 min-w-0">
                <p className="text-xs font-semibold truncate">{item.cardName}</p>
                <p className="text-[10px] text-text-muted">{item.cardSetName} · {item.condition}</p>
              </div>
              <span className="text-[10px] text-text-muted shrink-0">×{item.quantity}</span>
            </div>
          ))}
        </div>
      </div>

      {/* Offered cards */}
      {offer.offeredCards.length > 0 && (
        <div>
          <p className="text-xs font-semibold text-text-secondary mb-1.5 uppercase">
            Carte offerte ({offer.offeredCards.length})
          </p>
          <div className="space-y-1">
            {offer.offeredCards.map((item) => (
              <div key={item.id} className="flex items-center gap-2 p-2 bg-green-50/50 rounded-lg border border-green-100">
                <div className="flex-1 min-w-0">
                  <p className="text-xs font-semibold truncate">{item.cardName}</p>
                  <p className="text-[10px] text-text-muted">{item.cardSetName} · {item.condition}</p>
                </div>
                <span className="text-[10px] text-text-muted shrink-0">×{item.quantity}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {isAccepted && (otherUserPayment?.paypalUsername || otherUserPayment?.satispayUsername || otherUserPayment?.paymentQrCodeUrl) && (
        <div className="bg-surface-dark rounded-xl p-3 space-y-2">
          <p className="text-xs font-semibold text-text-secondary uppercase">Pagamento</p>
          {otherUserPayment?.paypalUsername && (
            <button
              onClick={openPaypal}
              className="w-full py-2.5 rounded-xl text-sm font-medium border border-border bg-white hover:bg-surface-dark transition-colors"
            >
              <ExternalLink size={14} className="inline mr-1" />
              Paga con PayPal (@{otherUserPayment.paypalUsername})
            </button>
          )}
          {otherUserPayment?.satispayUsername && (
            <p className="text-xs text-text-muted">Satispay: {otherUserPayment.satispayUsername}</p>
          )}
          {otherUserPayment?.paymentQrCodeUrl && (
            <button
              onClick={openQr}
              className="w-full py-2.5 rounded-xl text-sm font-medium border border-border bg-white hover:bg-surface-dark transition-colors"
            >
              <ExternalLink size={14} className="inline mr-1" />
              Apri QR pagamento
            </button>
          )}
        </div>
      )}

      {actionError && (
        <div className="p-3 bg-red-50 border border-red-200 rounded-xl text-sm text-red-700">
          {actionError}
        </div>
      )}

      {/* Actions */}
      {isPending && isReceiver && (
        <div className="space-y-2 pt-2">
          <Button onClick={() => onAction(offer.id, 'accept')} className="w-full" isLoading={loading}>
            <Check size={16} /> Accetta
          </Button>
          <div className="flex gap-2">
            <button
              onClick={() => onNavigateToCounter(offer.id)}
              className="flex-1 py-2.5 rounded-xl text-sm font-medium border border-primary text-primary hover:bg-primary/5 transition-colors"
            >
              <RotateCcw size={14} className="inline mr-1" />
              Controproposta
            </button>
            <button
              onClick={() => onAction(offer.id, 'reject')}
              disabled={loading}
              className="flex-1 py-2.5 rounded-xl text-sm font-medium border border-red-300 text-red-600 hover:bg-red-50 transition-colors"
            >
              <X size={14} className="inline mr-1" />
              Rifiuta
            </button>
          </div>
        </div>
      )}

      {isPending && isSender && (
        <Button onClick={() => onAction(offer.id, 'cancel')} variant="outline" className="w-full" isLoading={loading}>
          <X size={16} /> Annulla richiesta
        </Button>
      )}

      {isAccepted && (
        <Button onClick={() => onAction(offer.id, 'complete')} className="w-full" isLoading={loading}>
          <Check size={16} /> Segna come completato
        </Button>
      )}
    </div>
  );
}
