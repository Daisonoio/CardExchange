import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Check, Loader2, UserPlus } from 'lucide-react';
import { follows, type FollowStatus } from '../../api';

interface FollowButtonProps {
  /** Utente o negozio da seguire */
  target: { userId: number } | { storeId: number };
  /** Mostra sotto il numero di follower */
  showCount?: boolean;
  className?: string;
}

/** Pulsante "Segui" per utenti e negozi: chi segue riceve i loro avvisi "Sono qui". */
export default function FollowButton({ target, showCount = false, className = '' }: FollowButtonProps) {
  const { t } = useTranslation('live');
  const [status, setStatus] = useState<FollowStatus | null>(null);
  const [busy, setBusy] = useState(false);
  const isUser = 'userId' in target;
  const id = isUser ? target.userId : target.storeId;

  useEffect(() => {
    let alive = true;
    (isUser ? follows.userStatus(id) : follows.storeStatus(id))
      .then(({ data }) => { if (alive) setStatus(data); })
      .catch(() => { if (alive) setStatus(null); });
    return () => { alive = false; };
  }, [id, isUser]);

  const toggle = async () => {
    if (!status || busy) return;
    setBusy(true);
    try {
      const call = status.following
        ? (isUser ? follows.unfollowUser(id) : follows.unfollowStore(id))
        : (isUser ? follows.followUser(id) : follows.followStore(id));
      setStatus((await call).data);
    } catch {
      /* stato invariato */
    } finally {
      setBusy(false);
    }
  };

  if (!status) return null;
  const on = status.following;

  return (
    <div className={`flex flex-col items-center shrink-0 ${className}`}>
      <button
        type="button"
        onClick={toggle}
        disabled={busy}
        aria-pressed={on}
        title={on ? t('follow.unfollow') : t('follow.hint')}
        className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-xs font-semibold transition-colors disabled:opacity-60 ${
          on ? 'bg-surface-dark text-text-secondary hover:bg-red-50 hover:text-red-600' : 'bg-primary text-white hover:bg-primary-light'
        }`}
      >
        {busy ? <Loader2 size={14} className="animate-spin" /> : on ? <Check size={14} /> : <UserPlus size={14} />}
        {on ? t('follow.following') : t('follow.follow')}
      </button>
      {showCount && (
        <span className="text-[10px] text-text-muted mt-0.5">{t('follow.followers', { count: status.followers })}</span>
      )}
    </div>
  );
}
