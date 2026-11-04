import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowLeft, Loader2, ShieldCheck } from 'lucide-react';
import { Trans, useTranslation } from 'react-i18next';
import { moderation } from '../api';
import { formatDate, formatDateTime } from '../i18n/format';
import type { ModerationReport } from '../types';
import { REPORT_REASON_LABELS } from '../types';
import { apiErrorMessage } from '../utils/apiError';
import { hasPermission } from '../utils/permissions';
import Button from '../components/ui/Button';
import EmptyState from '../components/ui/EmptyState';
import StoresModeration from '../components/stores/StoresModeration';

type Tab = 'open' | 'closed' | 'stores';

/** Coda delle segnalazioni per moderatori (permesso ADMIN.REPORTS.MANAGE). */
export default function ModerationPage() {
  const { t } = useTranslation('admin');
  const navigate = useNavigate();
  const [tab, setTab] = useState<Tab>('open');
  const [items, setItems] = useState<ModerationReport[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<number | null>(null);
  const [notes, setNotes] = useState<Record<number, string>>({});
  const canBan = hasPermission('USERS.BAN');

  const load = useCallback(async () => {
    if (tab === 'stores') return;
    setLoading(true);
    setError(null);
    try {
      const { data } = await moderation.reports(tab as 'open' | 'closed');
      setItems(data.items);
    } catch (err) {
      setError(apiErrorMessage(err, t('moderation.errors.noAccess')));
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [tab, t]);

  useEffect(() => { load(); }, [load]);

  const resolve = async (report: ModerationReport, outcome: 'resolved' | 'dismissed', suspendUser = false) => {
    if (suspendUser && !confirm(t('moderation.confirmSuspend', { username: report.reportedUser.username }))) return;
    setBusyId(report.id);
    try {
      await moderation.resolve(report.id, { outcome, suspendUser, note: notes[report.id]?.trim() || undefined });
      setItems((prev) => prev.filter((r) => r.id !== report.id));
    } catch (err) {
      setError(apiErrorMessage(err, t('moderation.errors.operationFailed')));
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="pb-20">
      <div className="flex items-center gap-3 mb-4">
        <button onClick={() => navigate(-1)} aria-label={t('common:actions.back')} className="w-9 h-9 rounded-full bg-surface-dark flex items-center justify-center">
          <ArrowLeft size={18} />
        </button>
        <h1 className="text-xl font-bold text-text flex-1">{t('moderation.title')}</h1>
      </div>

      <div className="flex rounded-xl border border-border overflow-hidden bg-white mb-4">
        {(['open', 'closed', 'stores'] as Tab[]).map((tb) => (
          <button
            key={tb}
            onClick={() => setTab(tb)}
            className={`flex-1 py-2 text-sm font-medium ${tab === tb ? 'bg-primary text-white' : 'text-text-secondary'}`}
          >
            {tb === 'open' ? t('moderation.tabs.open') : tb === 'closed' ? t('moderation.tabs.closed') : t('moderation.tabs.stores')}
          </button>
        ))}
      </div>

      {error && <div className="mb-3 p-3 rounded-xl bg-red-50 text-danger text-sm">{error}</div>}

      {tab === 'stores' ? (
        <StoresModeration />
      ) : loading ? (
        <div className="flex justify-center py-10"><Loader2 className="animate-spin text-primary" /></div>
      ) : items.length === 0 ? (
        <EmptyState icon={ShieldCheck} title={t('moderation.empty.title')} description={tab === 'open' ? t('moderation.empty.open') : t('moderation.empty.closed')} />
      ) : (
        <ul className="space-y-3">
          {items.map((r) => (
            <li key={r.id} className="p-3 rounded-2xl border border-border bg-white space-y-2">
              <div className="flex items-start justify-between gap-2">
                <div>
                  <p className="text-sm font-semibold">
                    @{r.reportedUser.username}
                    {!r.reportedUser.isActive && <span className="ml-1.5 text-[10px] px-1.5 py-0.5 rounded-full bg-red-100 text-red-700">{t('moderation.suspended')}</span>}
                  </p>
                  <p className="text-[11px] text-text-muted">
                    {t('moderation.reportedBy', { username: r.reporter.username })} · {formatDateTime(r.createdAt, {})}
                  </p>
                </div>
                <span className="text-[10px] px-2 py-0.5 rounded-full bg-amber-100 text-amber-800 shrink-0">
                  {REPORT_REASON_LABELS[r.reason] ?? r.reason}
                </span>
              </div>
              {r.details && <p className="text-sm text-text bg-surface-dark rounded-xl p-2">{r.details}</p>}
              <p className="text-[11px] text-text-secondary">
                {t('moderation.reputation', { value: r.reportedUser.reputationScore.toFixed(1) })} · {t('moderation.tradesCompleted', { count: r.reportedUser.totalTradesCompleted })} ·{' '}
                <Trans t={t} i18nKey="moderation.openReports" count={r.reportedUser.openReports} components={{ strong: <strong /> }} />
                {r.tradeOfferId && <> · {t('moderation.trade', { id: r.tradeOfferId })}</>}
              </p>

              {r.status === 'Open' ? (
                <>
                  <input
                    value={notes[r.id] ?? ''}
                    onChange={(e) => setNotes({ ...notes, [r.id]: e.target.value })}
                    placeholder={t('moderation.notePlaceholder')}
                    maxLength={1000}
                    className="w-full px-3 py-2 rounded-xl border border-border text-xs"
                  />
                  <div className="grid grid-cols-3 gap-1.5">
                    <Button size="sm" variant="outline" disabled={busyId === r.id} onClick={() => resolve(r, 'dismissed')}>{t('moderation.dismiss')}</Button>
                    <Button size="sm" disabled={busyId === r.id} onClick={() => resolve(r, 'resolved')}>{t('moderation.resolve')}</Button>
                    <Button
                      size="sm"
                      variant="danger"
                      disabled={busyId === r.id || !canBan || !r.reportedUser.isActive}
                      title={canBan ? undefined : t('moderation.banPermissionRequired')}
                      onClick={() => resolve(r, 'resolved', true)}
                    >
                      {t('moderation.suspend')}
                    </Button>
                  </div>
                </>
              ) : (
                <p className="text-xs text-text-secondary">
                  {r.status === 'Resolved' ? t('moderation.resolved') : t('moderation.dismissed')}
                  {r.resolvedAt && ` ${t('moderation.closedOn', { date: formatDate(r.resolvedAt, {}) })}`}
                  {r.userSuspended && ` · ${t('moderation.accountSuspended')}`}
                  {r.resolutionNote && ` · ${r.resolutionNote}`}
                </p>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
