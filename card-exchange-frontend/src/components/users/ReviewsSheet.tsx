import { useEffect, useState } from 'react';
import { Check, Loader2, X } from 'lucide-react';
import { tradeOffers } from '../../api';
import type { UserReviewsPage } from '../../types';
import BottomSheet from '../ui/BottomSheet';
import StarRating from './StarRating';

/** Recensioni ricevute da un utente. */
export default function ReviewsSheet({ userId, open, onClose }: { userId: number | null; open: boolean; onClose: () => void }) {
  // Risultato associato all'utente per cui è stato caricato: il caricamento è "in corso" finché non coincide
  const [result, setResult] = useState<{ userId: number; data: UserReviewsPage | null } | null>(null);

  useEffect(() => {
    if (!open || !userId) return;
    let cancelled = false;
    tradeOffers.reviewsByUser(userId)
      .then(({ data }) => { if (!cancelled) setResult({ userId, data }); })
      .catch(() => { if (!cancelled) setResult({ userId, data: null }); });
    return () => { cancelled = true; };
  }, [open, userId]);

  const loading = open && !!userId && result?.userId !== userId;
  const data = result?.userId === userId ? result.data : null;

  return (
    <BottomSheet open={open} onClose={onClose} title={data ? `Recensioni di @${data.username}` : 'Recensioni'}>
      {loading ? (
        <div className="flex justify-center py-8"><Loader2 className="animate-spin text-primary" /></div>
      ) : !data || data.totalReviews === 0 ? (
        <p className="text-sm text-text-muted text-center py-8">Ancora nessuna recensione.</p>
      ) : (
        <div className="space-y-3">
          <div className="flex items-center gap-3 p-3 rounded-xl bg-surface-dark">
            <span className="text-2xl font-bold">{data.reputationScore.toFixed(1)}</span>
            <div>
              <StarRating value={data.reputationScore} />
              <p className="text-xs text-text-muted">{data.totalReviews} {data.totalReviews === 1 ? 'recensione' : 'recensioni'}</p>
            </div>
          </div>
          {data.reviews.items.map((r) => (
            <div key={r.id} className="p-3 rounded-xl border border-border bg-white space-y-1.5">
              <div className="flex items-center justify-between">
                <span className="text-sm font-semibold">@{r.reviewerUsername}</span>
                <StarRating value={r.rating} size={14} />
              </div>
              {r.comment && <p className="text-sm text-text">{r.comment}</p>}
              <div className="flex flex-wrap gap-1.5 text-[10px]">
                {[
                  { ok: r.cardAsDescribed, label: 'Carte come descritte' },
                  { ok: r.timelyShipping, label: 'Puntuale' },
                  { ok: r.goodCommunication, label: 'Buona comunicazione' },
                ].map(({ ok, label }) => (
                  <span key={label} className={`inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded-full ${ok ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-600'}`}>
                    {ok ? <Check size={10} /> : <X size={10} />} {label}
                  </span>
                ))}
              </div>
              <p className="text-[10px] text-text-muted">{new Date(r.createdAt).toLocaleDateString('it-IT')}</p>
            </div>
          ))}
        </div>
      )}
    </BottomSheet>
  );
}
