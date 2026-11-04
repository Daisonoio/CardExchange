import { useEffect, useRef, useState } from 'react';
import { Ban, Flag, MoreVertical } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { safety } from '../../api';
import { REPORT_REASONS } from '../../types';
import { apiErrorMessage } from '../../utils/apiError';
import BottomSheet from '../ui/BottomSheet';
import Button from '../ui/Button';

/** Menu "⋮" con Blocca/Sblocca e Segnala per un altro utente. */
export default function UserSafetyMenu({ userId, username, tradeOfferId, onBlockedChange }: {
  userId: number;
  username: string;
  tradeOfferId?: number;
  onBlockedChange?: (blocked: boolean) => void;
}) {
  const { t } = useTranslation('users');
  const [menuOpen, setMenuOpen] = useState(false);
  const [blocked, setBlocked] = useState(false);
  const [reportOpen, setReportOpen] = useState(false);
  const [reason, setReason] = useState(0);
  const [details, setDetails] = useState('');
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<string | null>(null);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    safety.blocked().then(({ data }) => setBlocked(data.some((b) => b.userId === userId))).catch(() => {});
  }, [userId]);

  useEffect(() => {
    if (!menuOpen) return;
    const close = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) setMenuOpen(false); };
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, [menuOpen]);

  const toggleBlock = async () => {
    setMenuOpen(false);
    if (!blocked && !confirm(t('safety.blockConfirm', { username }))) return;
    try {
      const { data } = blocked ? await safety.unblock(userId) : await safety.block(userId);
      setBlocked(!blocked);
      onBlockedChange?.(!blocked);
      setFeedback(data.message);
    } catch (err) {
      setFeedback(apiErrorMessage(err, t('safety.actionError')));
    }
  };

  const sendReport = async () => {
    if (!reason) return;
    setBusy(true);
    try {
      const { data } = await safety.report({ reportedUserId: userId, reason, details: details.trim() || undefined, tradeOfferId });
      setFeedback(data.message);
      setReportOpen(false);
      setReason(0); setDetails('');
    } catch (err) {
      setFeedback(apiErrorMessage(err, t('safety.sendError')));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="relative" ref={ref}>
      <button
        type="button"
        onClick={() => setMenuOpen(!menuOpen)}
        aria-label={t('safety.moreActions', { username })}
        className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center hover:bg-gray-200"
      >
        <MoreVertical size={18} />
      </button>
      {menuOpen && (
        <div className="absolute right-0 top-11 z-40 w-52 rounded-xl bg-white shadow-lg border border-border overflow-hidden">
          <button type="button" onClick={() => { setMenuOpen(false); setReportOpen(true); }} className="w-full flex items-center gap-2 px-4 py-3 text-sm hover:bg-surface-dark text-left">
            <Flag size={16} /> {t('safety.report', { username })}
          </button>
          <button type="button" onClick={toggleBlock} className="w-full flex items-center gap-2 px-4 py-3 text-sm text-danger hover:bg-red-50 text-left">
            <Ban size={16} /> {blocked ? t('safety.unblock', { username }) : t('safety.block', { username })}
          </button>
        </div>
      )}
      {feedback && (
        <div role="status" className="fixed left-4 right-4 bottom-24 z-50 mx-auto max-w-md p-3 rounded-xl bg-gray-900 text-white text-sm shadow-lg" onClick={() => setFeedback(null)}>
          {feedback}
        </div>
      )}

      <BottomSheet open={reportOpen} onClose={() => setReportOpen(false)} title={t('safety.report', { username })}>
        <div className="space-y-3">
          <p className="text-xs text-text-secondary">{t('safety.reportInfo')}</p>
          <div className="space-y-1.5">
            {REPORT_REASONS.map((r) => (
              <label key={r.value} className={`flex items-center gap-3 p-3 rounded-xl border text-sm cursor-pointer ${reason === r.value ? 'border-primary bg-primary/5' : 'border-border'}`}>
                <input type="radio" name="report-reason" className="accent-primary" checked={reason === r.value} onChange={() => setReason(r.value)} />
                {r.label}
              </label>
            ))}
          </div>
          <textarea
            value={details}
            onChange={(e) => setDetails(e.target.value)}
            maxLength={1000}
            rows={3}
            placeholder={t('safety.detailsPlaceholder')}
            className="w-full px-4 py-3 rounded-xl border border-border text-sm focus:outline-none focus:ring-2 focus:ring-primary/30 resize-none"
          />
          <Button className="w-full" variant="danger" onClick={sendReport} isLoading={busy} disabled={!reason}>{t('safety.sendReport')}</Button>
        </div>
      </BottomSheet>
    </div>
  );
}
