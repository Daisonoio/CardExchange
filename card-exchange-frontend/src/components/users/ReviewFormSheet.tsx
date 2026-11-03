import { useState } from 'react';
import { tradeOffers } from '../../api';
import { apiErrorMessage } from '../../utils/apiError';
import BottomSheet from '../ui/BottomSheet';
import Button from '../ui/Button';
import StarRating from './StarRating';

const CHECKS = [
  { key: 'cardAsDescribed', label: 'Carte come descritte' },
  { key: 'timelyShipping', label: 'Puntuale (appuntamento o spedizione)' },
  { key: 'goodCommunication', label: 'Comunicazione chiara e cortese' },
] as const;

/** Recensione dell'altro utente dopo uno scambio completato. */
export default function ReviewFormSheet({ offerId, otherUsername, open, onClose, onSubmitted }: {
  offerId: number | null;
  otherUsername: string;
  open: boolean;
  onClose: () => void;
  onSubmitted: () => void;
}) {
  const [rating, setRating] = useState(0);
  const [comment, setComment] = useState('');
  const [checks, setChecks] = useState({ cardAsDescribed: true, timelyShipping: true, goodCommunication: true });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    if (!offerId || rating === 0) return;
    setBusy(true);
    setError(null);
    try {
      await tradeOffers.review(offerId, { rating, comment: comment.trim() || undefined, ...checks });
      setRating(0); setComment('');
      onSubmitted();
      onClose();
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossibile salvare la recensione.'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <BottomSheet open={open} onClose={onClose} title={`Com'è andata con @${otherUsername}?`}>
      <div className="space-y-4">
        <div className="flex flex-col items-center gap-1">
          <StarRating value={rating} onChange={setRating} size={32} />
          <p className="text-xs text-text-muted">{rating === 0 ? 'Tocca le stelle per votare' : `${rating} su 5`}</p>
        </div>
        <div className="space-y-1.5">
          {CHECKS.map(({ key, label }) => (
            <label key={key} className="flex items-center justify-between gap-3 p-3 rounded-xl bg-surface-dark text-sm">
              {label}
              <input
                type="checkbox"
                className="w-5 h-5 accent-primary"
                checked={checks[key]}
                onChange={(e) => setChecks({ ...checks, [key]: e.target.checked })}
              />
            </label>
          ))}
        </div>
        <textarea
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          maxLength={1000}
          rows={3}
          placeholder="Un commento per gli altri collezionisti (facoltativo)"
          className="w-full px-4 py-3 rounded-xl border border-border text-sm focus:outline-none focus:ring-2 focus:ring-primary/30 resize-none"
        />
        {error && <p className="text-sm text-danger">{error}</p>}
        <Button className="w-full" onClick={submit} isLoading={busy} disabled={rating === 0}>Pubblica recensione</Button>
      </div>
    </BottomSheet>
  );
}
