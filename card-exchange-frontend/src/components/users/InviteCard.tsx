import { useEffect, useState } from 'react';
import { Gift, Share2 } from 'lucide-react';
import { referrals } from '../../api';
import { shareLink } from '../../utils/referral';

/** Link personale di invito e amici che si sono iscritti. */
export default function InviteCard() {
  const [data, setData] = useState<{ link: string; invitedCount: number; invited: { username: string }[] } | null>(null);
  const [feedback, setFeedback] = useState<string | null>(null);

  useEffect(() => {
    referrals.mine().then(({ data }) => setData(data)).catch(() => setData(null));
  }, []);

  if (!data) return null;

  const share = async () => {
    const result = await shareLink(data.link, 'CardExchange',
      'Scambiamo carte! Iscriviti a CardExchange: trovi chi, vicino a te, ha le carte che cerchi.');
    setFeedback(result === 'copied' ? 'Link copiato negli appunti' : result === 'shared' ? null : 'Copia il link qui sopra');
  };

  return (
    <div className="bg-white rounded-2xl shadow-sm border border-border/50 p-4 mb-4 space-y-2">
      <div className="flex items-center gap-3">
        <span className="w-9 h-9 rounded-full border-2 border-border flex items-center justify-center shrink-0" style={{ color: '#1a3461' }}>
          <Gift size={16} />
        </span>
        <div className="flex-1">
          <p className="text-sm font-medium text-text">Invita amici</p>
          <p className="text-xs text-text-muted">Più collezionisti in zona, più scambi per tutti.</p>
        </div>
      </div>
      <div className="flex items-center gap-2">
        <input readOnly value={data.link} aria-label="Link di invito" onFocus={(e) => e.target.select()}
          className="flex-1 min-w-0 px-3 py-2 rounded-xl border border-border text-xs bg-surface-dark" />
        <button type="button" onClick={share} className="inline-flex items-center gap-1 px-3 py-2 rounded-xl bg-primary text-white text-xs font-semibold">
          <Share2 size={14} /> Condividi
        </button>
      </div>
      {feedback && <p className="text-xs text-text-secondary">{feedback}</p>}
      <p className="text-xs text-text-secondary">
        {data.invitedCount === 0
          ? 'Nessun amico iscritto con il tuo link, per ora.'
          : `${data.invitedCount} ${data.invitedCount === 1 ? 'amico iscritto' : 'amici iscritti'}: ${data.invited.slice(0, 5).map((i) => `@${i.username}`).join(', ')}${data.invitedCount > 5 ? '…' : ''}`}
      </p>
    </div>
  );
}
