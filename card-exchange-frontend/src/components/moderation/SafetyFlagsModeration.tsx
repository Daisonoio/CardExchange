import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AlertTriangle, Loader2, ShieldAlert } from 'lucide-react';
import { safetyModeration, type SafetyFlagItem } from '../../api';
import { formatDateTime } from '../../i18n/format';
import { apiErrorMessage } from '../../utils/apiError';
import { hasPermission } from '../../utils/permissions';
import EmptyState from '../ui/EmptyState';

/** Coda di sicurezza: messaggi bloccati dal filtro e segnalazioni per minori o proposte illegali, prima quelle con minorenni. */
export default function SafetyFlagsModeration() {
  const { t } = useTranslation('admin');
  const [items, setItems] = useState<SafetyFlagItem[] | null>(null);
  const [notes, setNotes] = useState<Record<number, string>>({});
  const [busy, setBusy] = useState<number | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const load = useCallback(() => {
    safetyModeration.list().then(({ data }) => setItems(data)).catch(() => setItems([]));
  }, []);
  useEffect(() => { load(); }, [load]);

  const act = async (f: SafetyFlagItem, action: 'dismiss' | 'confirm' | 'lift', deactivateAccount = false) => {
    if (deactivateAccount && !confirm(t('safety.deactivateConfirm', { username: f.sender.username }))) return;
    setBusy(f.id); setMessage(null);
    try {
      const { data } = await safetyModeration.resolve(f.id, { action, deactivateAccount, note: notes[f.id] });
      setMessage(data.message);
      load();
    } catch (err) {
      setMessage(apiErrorMessage(err, t('safety.error')));
    } finally {
      setBusy(null);
    }
  };

  if (items === null) return <div className="flex justify-center py-10"><Loader2 className="animate-spin text-primary" /></div>;
  const canBan = hasPermission('USERS.BAN');
  const btn = 'px-3 py-1.5 rounded-xl text-xs font-semibold disabled:opacity-50';

  return (
    <div className="space-y-3">
      <p className="text-xs text-text-secondary">{t('safety.intro')}</p>
      {message && <p role="status" className="text-sm p-3 rounded-xl bg-surface-dark">{message}</p>}
      {items.length === 0 ? (
        <EmptyState icon={ShieldAlert} title={t('safety.empty')} />
      ) : (
        <ul className="space-y-3">
          {items.map((f) => (
            <li key={f.id} className={`bg-white rounded-2xl border p-4 space-y-2 ${f.involvesMinor ? 'border-red-300' : 'border-border'}`}>
              <div className="flex items-start justify-between gap-2">
                <div>
                  <span className="text-xs font-bold text-red-700 flex items-center gap-1">
                    {f.involvesMinor && <AlertTriangle size={13} />} {t(`safety.kinds.${f.kind}`)}{f.involvesMinor ? ` · ${t('safety.minor')}` : ''}
                  </span>
                  <span className="block text-[11px] text-text-muted">{formatDateTime(f.createdAt)} · {f.blocked ? t('safety.blocked') : t('safety.notBlocked')}</span>
                </div>
                <span className="text-[11px] text-text-muted text-right">
                  {t('safety.from')} <strong>@{f.sender.username}</strong>{f.recipient?.username ? <> → @{f.recipient.username}</> : null}
                  <br />{t('safety.flags30', { count: f.sender.flags30Days })}
                  {f.sender.chatSuspendedUntil && new Date(f.sender.chatSuspendedUntil) > new Date() && <><br /><span className="text-red-700 font-semibold">{t('safety.chatSuspended')}</span></>}
                  {!f.sender.isActive && <><br /><span className="text-red-700 font-semibold">{t('safety.accountOff')}</span></>}
                </span>
              </div>
              {f.excerpt && <blockquote className="text-sm p-2 rounded-lg bg-surface-dark whitespace-pre-line break-words">{f.excerpt}</blockquote>}
              <input value={notes[f.id] ?? ''} onChange={(e) => setNotes({ ...notes, [f.id]: e.target.value })} maxLength={500}
                placeholder={t('safety.note')} className="w-full px-3 py-2 rounded-xl border border-border text-sm" />
              <div className="flex flex-wrap gap-2">
                <button type="button" disabled={busy === f.id} onClick={() => act(f, 'dismiss')} className={`${btn} bg-surface-dark`}>{t('safety.dismiss')}</button>
                <button type="button" disabled={busy === f.id} onClick={() => act(f, 'confirm')} className={`${btn} bg-amber-100 text-amber-900`}>{t('safety.confirm')}</button>
                {canBan && f.sender.isActive && (
                  <button type="button" disabled={busy === f.id} onClick={() => act(f, 'confirm', true)} className={`${btn} bg-red-600 text-white`}>{t('safety.deactivate')}</button>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
      <p className="text-[11px] text-text-muted">{t('safety.legal')}</p>
    </div>
  );
}
